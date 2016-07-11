/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.

This class implements a NAT (Network Address Translation) middlebox using
Pax. The NAT mediates between two networks, which we call "Outside" and "Inside",
and multiplexes TCP connections originating form Inside to Outside, and allows
Outside to participate in such connections.

                                 ---------
                                 |      [1]-- ...
               (Outside) ... ---[0] NAT [2]-- ... (Inside)
                                 |      ...
                                 |      [n]-- ...
                                 ---------

This NAT only works for TCP connections over IPv4. It maps TCP connections
arriving on non-zero ports, onto the NAT's zero port. It maintains a mapping of
active TCP connections. It adds an entry to this mapping whenever it receives
a TCP SYN packet coming from a non-zero port; it assigns a fresh ephemeral TCP port
on its zero network port, and updates the source IP address to its own. It then
forwards the packet onto its zero port. It removes an entry when it's no longer
needed (because of a FIN-shutdown or RST) or because an activity timer has
expired. As long as it has an entry, it maps TCP segments from non-zero to
zero ports, masquerading the client.

FIXME currently we don't remove entries
NOTE could improve the implementation by having configurable "forwarding ports"
     that enable you to run servers on non-zero network ports.
*/

using System;
using System.Collections.Concurrent;
using PacketDotNet;
using System.Net;
using Pax;
using System.Net.NetworkInformation;
using System.Timers;
using System.Collections.Generic;

namespace Pax.Examples.Nat
{
  public sealed class NAT : SimplePacketProcessor
  {
    public const int Port_Drop = -1;
    public const int Port_Outside = 0;

    private readonly IPAddress my_address;
    private readonly PhysicalAddress next_outside_hop_mac;
    private readonly TimeSpan connection_timeout;
    private readonly Timer gcTimer;

    public NAT (IPAddress my_address, PhysicalAddress next_outside_hop_mac, ushort tcp_start_port, ushort udp_start_port, TimeSpan? connection_timeout = null) {
      this.my_address = my_address;
      this.next_outside_hop_mac = next_outside_hop_mac;
      this.connection_timeout = connection_timeout ?? new TimeSpan(30L * TimeSpan.TicksPerSecond);

      // Instantiate each NAT specialisation
      tcpNat = new SpecialisedNAT<TcpPacket>(my_address, next_outside_hop_mac, new TcpPort(tcp_start_port));
      udpNat = new SpecialisedNAT<UdpPacket>(my_address, next_outside_hop_mac, new UdpPort(udp_start_port));

      // Call the GarbageCollectConnections method regularly
      gcTimer = new Timer(1000);
      gcTimer.Elapsed += GarbageCollectConnections;
      gcTimer.AutoReset = true;
      gcTimer.Start();
    }

    // Use a separate namespace for each transport protocol
    SpecialisedNAT<TcpPacket> tcpNat;
    SpecialisedNAT<UdpPacket> udpNat;

    override public int handler (int in_port, ref Packet packet)
    {
      if (packet is EthernetPacket)
      {
        if (packet.PayloadPacket is IpPacket)
        {
          if (packet.PayloadPacket.PayloadPacket is TcpPacket)
          {
            var tcp = new TcpPacketEncapsulation(packet);
#if DEBUG
            Console.WriteLine("RX TCP {0}:{1} -> {2}:{3} on {4} [{5}{6}{7}{8}]",
              tcp.NetworkPacket.SourceAddress, tcp.TransportPacket.SourcePort, tcp.NetworkPacket.DestinationAddress, tcp.TransportPacket.DestinationPort, in_port,
              tcp.TransportPacket.Syn ? "S" : "", tcp.TransportPacket.Fin ? "F" : "", tcp.TransportPacket.Rst ? "R" : "", tcp.TransportPacket.Ack ? "." : "");
#endif
            return tcpNat.handlePacket(tcp, in_port);
          }
          else if (packet.PayloadPacket.PayloadPacket is UdpPacket)
          {
            var udp = new UdpPacketEncapsulation(packet);
#if DEBUG
            Console.WriteLine("RX UDP {0}:{1} -> {2}:{3} on {4}",
              udp.NetworkPacket.SourceAddress, udp.TransportPacket.SourcePort, udp.NetworkPacket.DestinationAddress, udp.TransportPacket.DestinationPort, in_port);
#endif
            return udpNat.handlePacket(new UdpPacketEncapsulation(packet), in_port);
          }
        }
      }

      // If we reach this point then we can't handle that type of packet
      return Port_Drop;
    }

    private void GarbageCollectConnections(object sender, ElapsedEventArgs e)
    {
      Console.WriteLine("GC");
      tcpNat.GarbageCollectConnections(connection_timeout);
      udpNat.GarbageCollectConnections(connection_timeout);
    }


    private sealed class SpecialisedNAT<T> where T : Packet
    {
      private readonly IPAddress my_address;
      private readonly PhysicalAddress next_outside_hop_mac;
      private ITransportAddress<T> currentMasqueradeAddress;
      private readonly object currentMasqueradeAddressLock = new object();

      // We keep 2 dictionaries, one for queries related to packets crossing from the outside (O) to the inside (I), and
      // the other for the inverse.
      // O --> I: when we get a packet on port Port_Outside, to find out how to rewrite the packet and forward it on which internal port.
      // I --> O: when we get a packet on port != Port_Outside, to find out how to rewrite the packet before forwarding it on port Port_Outside.
      IDictionary<ConnectionKey<T>, NatConnection<T>> NAT_MapToInside = new ConcurrentDictionary<ConnectionKey<T>, NatConnection<T>>();
      IDictionary<ConnectionKey<T>, NatConnection<T>> NAT_MapToOutside = new ConcurrentDictionary<ConnectionKey<T>, NatConnection<T>>();

      public SpecialisedNAT(IPAddress my_address, PhysicalAddress next_outside_hop_mac, ITransportAddress<T> initialMasqueradeAddress)
      {
        this.my_address = my_address;
        this.next_outside_hop_mac = next_outside_hop_mac;
        currentMasqueradeAddress = initialMasqueradeAddress;
      }

      public int handlePacket(PacketEncapsulation<T> packet, int in_port)
      {
        // Get the mapped destination port
        int outPort;
        if (in_port == Port_Outside)
          outPort = outside_to_inside(packet);
        else
          outPort = inside_to_outside(packet, in_port);

        return outPort;
      }

      /// <summary>
      /// Rewrite packets coming from the Outside and forward on the relevant Inside network port.
      /// </summary>
      private int outside_to_inside(PacketEncapsulation<T> packet)
      {
        // Retrieve the mapping. If a mapping doesn't exist, then it means that we're not
        // aware of a session to which the packet belongs: so drop the packet.
        var key = new ConnectionKey<T>(packet.GetSourceNode(), packet.GetDestinationNode());
        NatConnection<T> connection;
        if (NAT_MapToInside.TryGetValue(key, out connection))
        {
          var destination = connection.InsideNode;

          // Update any state
          connection.ReceivedPacket(packet, packetFromInside: false);

          // Rewrite the packet
          destination.RewritePacketDestination(packet);

          // Update checksums
          packet.UpdateChecksums();

          // Forward on the mapped network port
          return destination.InterfaceNumber;
        }
        else
        {
          return Port_Drop;
        }
      }

      /// <summary>
      /// Rewrite packets coming from the Inside and forward on the Outside network port.
      /// </summary>
      private int inside_to_outside(PacketEncapsulation<T> packet, int incomingInterfaceNumber)
      {
        // Get the mapping key, providing the interface numbers and mac in case we need to add a mapping
        packet.LinkPacket.DestinationHwAddress = next_outside_hop_mac; // Change MAC to reflect actual destination
        var out_key = new ConnectionKey<T>(packet.GetSourceNode(incomingInterfaceNumber), packet.GetDestinationNode(Port_Outside)); ;

        // FIXME remove existing connection before creating a new one? Utilise existing port?

        NatConnection<T> connection;
        bool mappingExists = NAT_MapToOutside.TryGetValue(out_key, out connection);

        if (!mappingExists)
        {
          if (packet.SignalsStartOfConnection())
          {
            // If new connection, then add a mapping
            CreateMapping(incomingInterfaceNumber, packet, out_key, out connection);
          }
          else
          {
            // Not a SYN, and no existing connection, so drop.
            return NAT.Port_Drop;
          }
        }

        // Update any state
        connection.ReceivedPacket(packet, packetFromInside: false);

        // Rewrite the packet
        connection.NatNode.RewritePacketSource(packet);

        // Update checksums
        packet.UpdateChecksums();

        // Forward on the mapped network port
        return connection.OutsideNode.InterfaceNumber;
      }

      public void GarbageCollectConnections(TimeSpan connectionTimeout)
      {
        bool removedAny = false;
        foreach (var pair in NAT_MapToInside)
        {
          NatConnection<T> connection = pair.Value;
          if (DateTime.Now - connection.LastUsed > connectionTimeout || connection.State.CanBeClosed)
          {
            Console.WriteLine("Removing connection (LU {0}, DIFF {1}, TL {2})",
              connection.LastUsed.ToShortTimeString(),
              (DateTime.Now - connection.LastUsed).ToString(),
              connectionTimeout);

            // Remove this connection from both lookups
            NAT_MapToInside.Remove(pair);
            NAT_MapToOutside.Remove(new ConnectionKey<T>(connection.InsideNode, connection.OutsideNode));
            removedAny = true;
          }
        }

#if DEBUG
        if (removedAny)
          PrintMappings();
#endif

        // FIXME - improve and Kiwi-ize
        // Use a separate timer for each connection, resetting it each time?  <- not feasible for KIWI
        // Keep a double-LL of MRU, updated with CASWP, and iterate from the tail? <- probably fairly lightweight, but still need time as well
      }

      private void CreateMapping(int networkPort, PacketEncapsulation<T> packet, ConnectionKey<T> toOutsideKey, out NatConnection<T> connection)
      {
        // Get the next transport address for masquerading
        ITransportAddress<T> nextTransportAddress;
        lock (currentMasqueradeAddressLock)
        {
          nextTransportAddress = currentMasqueradeAddress;
          currentMasqueradeAddress = toOutsideKey.Source.TransportAddress.GetNextMasqueradingAddress(currentMasqueradeAddress);
        }
        
        Node<T> insideNode = toOutsideKey.Source,
          outsideNode = toOutsideKey.Destination,
          natNode = new Node<T>(my_address, nextTransportAddress, Port_Drop, PaxConfig.deviceMap[Port_Outside].MacAddress);
        
        // Create connection object
        connection = new NatConnection<T>(insideNode, outsideNode, natNode);

        // Add to NAT_MapToOutside
        NAT_MapToOutside[toOutsideKey] = connection;

        // Add to NAT_MapToInside
        var toInsideKey = new ConnectionKey<T>(outsideNode, natNode);
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
      public void PrintMappings()
      {
        string heading = String.Format("[ Mapping tables for {0}s ]", typeof(T).Name);
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
}
