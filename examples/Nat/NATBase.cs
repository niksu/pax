/*
Pax : tool support for prototyping packet processors
Jonny Shipton, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using PacketDotNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace Pax.Examples.Nat
{
  /// <summary>
  /// Base implementation of a protocol-specific NAT.
  /// </summary>
  /// <typeparam name="TPacket">The type of packet.</typeparam>
  /// <typeparam name="TNode">The type of node. See the description of <see cref="Node"/> for more details.</typeparam>
  /// <typeparam name="TEncapsulation">The type of the packet encapsulation.</typeparam>
  public abstract class NATBase<TPacket,TNode,TEncapsulation> where TPacket : Packet where TNode : Node where TEncapsulation : PacketEncapsulation<TPacket,TNode>
  {
    /// <summary>
    /// A network interface number indicating the packet should be dropped.
    /// </summary>
    public const int Port_Drop = -1;

    /// <summary>
    /// A <see cref="ForwardingDecision.SinglePortForward"/> indicating the packet should be dropped.
    /// </summary>
    private readonly ForwardingDecision.SinglePortForward Drop = new ForwardingDecision.SinglePortForward(Port_Drop);

    /// <summary>
    /// A value representing the network interface that faces outside.
    /// </summary>
    public const int Port_Outside = 0;

    /// <summary>
    /// The outside facing IP address of the NAT.
    /// </summary>
    protected readonly IPAddress OutsideFacingAddress;

    /// <summary>
    /// The MAC address of the next hop on the outside port.
    /// </summary>
    protected readonly PhysicalAddress NextOutsideHopMacAddress;

    /// <summary>
    /// The time that an inactive connection entry must be kept before the entry can be removed.
    /// </summary>
    protected readonly TimeSpan InactivityTimeout;

    // We keep 2 dictionaries, one for queries related to packets crossing from the outside (O) to the inside (I), and
    // the other for the inverse.
    // O --> I: when we get a packet on port Port_Outside, to find out how to rewrite the packet and forward it on which internal port.
    // I --> O: when we get a packet on port != Port_Outside, to find out how to rewrite the packet before forwarding it on port Port_Outside.
    private IDictionary<ConnectionKey, NatConnection<TPacket,TNode>> NAT_MapToInside = new ConcurrentDictionary<ConnectionKey, NatConnection<TPacket,TNode>>();
    private IDictionary<ConnectionKey, NatConnection<TPacket,TNode>> NAT_MapToOutside = new ConcurrentDictionary<ConnectionKey, NatConnection<TPacket,TNode>>();

    /// <param name="outsideFacingAddress">The public IP address of the NAT</param>
    /// <param name="nextOutsideHopMacAddress">The MAC address of the next hop on the outside-facing port.</param>
    /// <param name="inactivityTimeout">The time that an inactive connection entry must be kept before the entry can be removed. E.g. the TCP USER TIMEOUT duration.</param>
    protected NATBase(IPAddress outsideFacingAddress, PhysicalAddress nextOutsideHopMacAddress, TimeSpan inactivityTimeout)
    {
      OutsideFacingAddress = outsideFacingAddress;
      NextOutsideHopMacAddress = nextOutsideHopMacAddress;
      InactivityTimeout = inactivityTimeout;
    }

    /// <summary>
    /// Handles an observed packet of this type.
    /// </summary>
    /// <param name="packet">The observed packet.</param>
    /// <param name="incomingNetworkInterface">The number of the network interface the packet arrived on.</param>
    /// <returns>A <see cref="ForwardingDecision"/> for the packet.</returns>
    public ForwardingDecision handlePacket(TEncapsulation packet, int incomingNetworkInterface)
    {
      // Get the forwarding decision
      ForwardingDecision forwardingDecision;
      if (incomingNetworkInterface == Port_Outside)
        forwardingDecision = OutsideToInside(packet);
      else
        forwardingDecision = InsideToOutside(packet, incomingNetworkInterface);

      return forwardingDecision;
    }

    /// <summary>
    /// Rewrite packets coming from the Outside and forward on the relevant Inside network port.
    /// </summary>
    /// <param name="packet">The incoming packet.</param>
    private ForwardingDecision OutsideToInside(TEncapsulation packet)
    {
      // Retrieve the mapping. If a mapping doesn't exist, then it means that we're not
      // aware of a session to which the packet belongs: so drop the packet.
      var key = new ConnectionKey(packet.GetSourceNode(), packet.GetDestinationNode());
      NatConnection<TPacket,TNode> connection;
      if (NAT_MapToInside.TryGetValue(key, out connection))
      {
        var destination = connection.InsideNode;

        // Update any connection state, including resetting the inactivity timer
        connection.ReceivedPacket(packet, packetFromInside: false);

        // Rewrite the packet destination
        packet.SetDestination(destination);

        // Update checksums
        packet.UpdateChecksums();

        // Forward on the mapped network port
        return new ForwardingDecision.SinglePortForward(destination.InterfaceNumber);
      }
      else
      {
        return Drop;
      }
    }

    /// <summary>
    /// Rewrite packets coming from the Inside and forward on the Outside network port.
    /// </summary>
    /// <param name="packet">The outgoing packet.</param>
    private ForwardingDecision InsideToOutside(TEncapsulation packet, int incomingInterfaceNumber)
    {
      // Get the mapping key, providing the interface numbers and mac in case we need to add a mapping
      packet.LinkPacket.DestinationHwAddress = NextOutsideHopMacAddress; // Change MAC to reflect actual destination
      TNode insideNode = packet.GetSourceNode(incomingInterfaceNumber),
        outsideNode = packet.GetDestinationNode(Port_Outside);
      var out_key = new ConnectionKey(insideNode, outsideNode);

      NatConnection<TPacket,TNode> connection;
      bool mappingExists = NAT_MapToOutside.TryGetValue(out_key, out connection);

      if (!mappingExists)
      {
        if (packet.SignalsStartOfConnection())
        {
          // If new connection, then add a mapping
          CreateMapping(incomingInterfaceNumber, insideNode, outsideNode, out connection);
        }
        else
        {
          // Not a new connection and no existing connection, so drop.
          return Drop;
        }
      }

      // Update any connection state, including resetting the inactivity timer
      connection.ReceivedPacket(packet, packetFromInside: true);

      // Rewrite the packet to appear to originate from the NAT
      packet.SetSource(connection.NatNode);

      // Update checksums
      packet.UpdateChecksums();

      // Forward on the mapped network port
      return new ForwardingDecision.SinglePortForward(connection.OutsideNode.InterfaceNumber);
    }

    /// <summary>
    /// Remove connections that have timed out or are closed.
    /// </summary>
    public void GarbageCollectConnections()
    {
      bool removedAny = false;
      DateTime now = DateTime.Now;
      foreach (var pair in NAT_MapToInside)
      {
        NatConnection<TPacket,TNode> connection = pair.Value;
        bool removeEntry = false;
        if (now - connection.LastUsed > InactivityTimeout)
        {
          removeEntry = true;
#if DEBUG
          Console.WriteLine("Removing inactive connection (LastUsed {0}, Diff {1}, InactivityTimeout {2})",
            connection.LastUsed.ToShortTimeString(),
            (now - connection.LastUsed).ToString(),
            InactivityTimeout);
#endif
        }
        else if (connection.State.CanBeClosed)
        {
          removeEntry = true;
#if DEBUG
          Console.WriteLine("Removing closed connection (LastUsed {0}, Diff {1}, InactivityTimeout {2})",
            connection.LastUsed.ToShortTimeString(),
            (now - connection.LastUsed).ToString(),
            InactivityTimeout);
#endif
        }

        if (removeEntry)
        {
          // Remove this connection from both lookups
          NAT_MapToInside.Remove(pair);
          NAT_MapToOutside.Remove(new ConnectionKey(connection.InsideNode, connection.OutsideNode));
#if DEBUG
          removedAny = true;
#endif
        }
      }

#if DEBUG
      if (removedAny)
        PrintMappings();
#endif

      // FIXME - currently O(n); improve performance for sparse traffic.
      // Could use a regularly invoked GC, and keep the connections in one of two pools:
      //   - For connections with lower traffic, keep a double-LL of MRU, moving the connection to the head when used,
      //     and start at the LRU end when iterating. Needs to be concurrent.
      //     Cost is per packet, and maintains an ordering, meaning we only need to check those that have expired plus one extra.
      //   - For connections with higher traffic, it would be less expensive to leave unsorted and iterate in O(n).
      // However, this would add complexity in terms of tracking which pool each connection should be in.
    }

    /// <summary>
    /// Creates a new <see cref="TNode"/> that can act as the masquerading address for a new connection. E.g. an unused TCP socket on the NAT host.
    /// Note that as all the information required to instantiate a <see cref="Node"/> is provided, in practice the main purpose of this method
    /// is to (for example) provide a source port for a new entry in the NAT for a new TCP connection.
    /// </summary>
    /// <param name="ipAddress">The IP address to use.</param>
    /// <param name="interfaceNumber">The interface number to use.</param>
    /// <param name="macAddress">The MAC address to use.</param>
    protected abstract TNode CreateMasqueradeNode(IPAddress ipAddress, int interfaceNumber, PhysicalAddress macAddress);

    /// <summary>
    /// Gets an initial state object for a new connection. E.g. <see cref="NoTransportState{TPacket}"/>.
    /// </summary>
    protected abstract ITransportState<TPacket> GetInitialStateForNewConnection();

    /// <summary>
    /// Creates and adds a new connection mapping to the NAT.
    /// </summary>
    /// <param name="networkPort">The network interface number of the inside node.</param>
    /// <param name="insideNode">The inside node.</param>
    /// <param name="outsideNode">The outside node.</param>
    /// <param name="connection">The <see cref="NatConnection{TPacket, TNode}"/> that is created.</param>
    private void CreateMapping(int networkPort, TNode insideNode, TNode outsideNode, out NatConnection<TPacket,TNode> connection)
    {
      // Get the node which the outside node will see as the source. E.g. the NAT with a new TCP port
      var natNode = CreateMasqueradeNode(OutsideFacingAddress, Port_Drop, PaxConfig.deviceMap[Port_Outside].MacAddress);

      // Create connection object
      connection = new NatConnection<TPacket,TNode>(insideNode, outsideNode, natNode, GetInitialStateForNewConnection());

      // Add to NAT_MapToOutside
      var toOutsideKey = new ConnectionKey(insideNode, outsideNode);
      NAT_MapToOutside[toOutsideKey] = connection;

      // Add to NAT_MapToInside
      var toInsideKey = new ConnectionKey(outsideNode, natNode);
      NAT_MapToInside[toInsideKey] = connection;

#if DEBUG
      Console.WriteLine("Added mapping");
      Console.WriteLine("Inside: {0}", insideNode);
      Console.WriteLine("Outside: {0}", outsideNode);
      Console.WriteLine("Nat: {0}", natNode);
      PrintMappings();
#endif
    }

#if DEBUG
    /// <summary>
    /// Print the mapping tables to the console.
    /// </summary>
    public virtual void PrintMappings()
    {
      string heading = String.Format("[ Mapping tables for {0}s ]", typeof(TPacket).Name);
      string headingLine = new String('=', heading.Length);
      Console.WriteLine(headingLine);
      Console.WriteLine(heading);
      Console.WriteLine(headingLine);

      Console.WriteLine(" (------ Outside ------)  \t<->\t (------ Inside ------) ");
      foreach (var entry in NAT_MapToInside)
      {
        Console.WriteLine("{0} \t<->\t {1}", entry.Key, entry.Value.InsideNode);
      }
      Console.WriteLine();
      Console.WriteLine(" (------ Inside ------)  \t<->\t (------ Outside ------) ");
      foreach (var entry in NAT_MapToOutside)
      {
        Console.WriteLine("{0} \t<->\t {1}", entry.Key, entry.Value.OutsideNode);
      }
    }
#endif
  }
}
