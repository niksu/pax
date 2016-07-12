/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016
Jonny Shipton, Cambridge University Computer Lab, July 2016

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

public class NAT : SimplePacketProcessor {
  public const int Port_Outside = 0;

  private IPAddress my_address;
  private ushort next_port;
  private readonly object portLock = new object();
  private readonly PhysicalAddress next_outside_hop_mac;

  public NAT (IPAddress my_address, ushort next_port, PhysicalAddress next_outside_hop_mac) {
    this.my_address = my_address;
    this.next_port = next_port;
    this.next_outside_hop_mac = next_outside_hop_mac;
  }

  // We keep 2 dictionaries, one for queries related to packets crossing from the outside (O) to the inside (I), and
  // the other for the inverse.
  // O --> I: when we get a packet on port Port_Outside, to find out how to rewrite the packet and forward it on which internal port.
  // I --> O: when we get a packet on port != Port_Outside, to find out how to rewrite the packet before forwarding it on port Port_Outside.
  ConcurrentDictionary<MapToInside_Key,InternalNode> NAT_MapToInside =
    new ConcurrentDictionary<MapToInside_Key,InternalNode>();
  ConcurrentDictionary<MapToOutside_Key,ushort> NAT_MapToOutside =
    new ConcurrentDictionary<MapToOutside_Key,ushort>();

  override public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    if (packet is EthernetPacket)
    {
      if (packet.Encapsulates(typeof(IPv4Packet), typeof(TcpPacket)))
      {
        // Unencapsulate the packets, so we can read and change their fields more easily.
        EthernetPacket p_eth = (EthernetPacket)packet;
        IpPacket p_ip = ((IpPacket)(packet.PayloadPacket));
        TcpPacket p_tcp = ((TcpPacket)(p_ip.PayloadPacket));

#if DEBUG
        Console.WriteLine("RX {0}:{1} -> {2}:{3} on {4} [{5}]",
          p_ip.SourceAddress, p_tcp.SourcePort, p_ip.DestinationAddress, p_tcp.DestinationPort, in_port,
          p_tcp.Syn?"S":"");
#endif

        ForwardingDecision des = ForwardingDecision.Drop.Instance;
        if (in_port == Port_Outside)
          des = outside_to_inside (p_eth, p_ip, p_tcp);
        else
          des = inside_to_outside (p_eth, p_ip, p_tcp, in_port);

#if DEBUG
        Console.WriteLine(" (------ Outside ------)  \t<->\t (------ Inside ------) ");
        foreach (var entry in NAT_MapToInside)
        {
          Console.WriteLine("{0} \t<->\t {1}", entry.Key, entry.Value);
        }
        Console.WriteLine();
        Console.WriteLine(" (------ Inside ------)  \t<->\t (------ Outside ------) ");
        foreach (var entry in NAT_MapToOutside)
        {
          Console.WriteLine("{0} \t<->\t {1}", entry.Key, entry.Value);
        }
#endif

        return des;
      }
    }

    // Drop packets that aren't handled
    return ForwardingDecision.Drop.Instance;
  }

  /// <summary>
  /// Rewrite packets coming from the Outside and forward on the relevant Inside network port.
  /// </summary>
  private ForwardingDecision outside_to_inside (EthernetPacket p_eth, IpPacket p_ip, TcpPacket p_tcp)
  {
    // p_ip.DestinationAddress should be my_address
    if (!p_ip.DestinationAddress.Equals(my_address))
      return ForwardingDecision.Drop.Instance;

    // Retrieve the mapping. If a mapping doesn't exist, then it means that we're not
    // aware of a session to which the packet belongs: so drop the packet.
    InternalNode destination;
    var key = new MapToInside_Key(p_ip.SourceAddress, p_tcp.SourcePort, p_tcp.DestinationPort);
    if (NAT_MapToInside.TryGetValue(key, out destination))
    {
      // Rewrite destination IP address and TCP port, and map to the appropriate Inside port.
      p_ip.DestinationAddress = destination.Address;
      p_tcp.DestinationPort = destination.Port;
      // Update checksums.
      p_tcp.UpdateTCPChecksum();
      ((IPv4Packet)p_ip).UpdateIPChecksum();
      // Update destination MAC address
      p_eth.DestinationHwAddress = destination.MacAddress;
      // Forward on the mapped network interface
      return (new ForwardingDecision.SinglePortForward(destination.InterfaceNumber));
    }
    else
    {
      return ForwardingDecision.Drop.Instance;
    }
  }

  /// <summary>
  /// Rewrite packets coming from the Inside and forward on the Outside network port.
  /// </summary>
  private ForwardingDecision inside_to_outside (EthernetPacket p_eth, IpPacket p_ip, TcpPacket p_tcp, int incomingPort)
  {
    var out_key = new MapToOutside_Key(p_ip.SourceAddress, p_tcp.SourcePort,
                                       p_ip.DestinationAddress, p_tcp.DestinationPort);
    ushort masqueradingPort;
    if (p_tcp.Syn)
    {
      // If a TCP SYN, then add a mapping for the new connection.
      masqueradingPort = GetNewMasqueradingTcpPort();
      // Add to NAT_MapToOutside
      NAT_MapToOutside[out_key] = masqueradingPort;
      // Add to NAT_MapToInside
      var in_key = new MapToInside_Key(p_ip.DestinationAddress, p_tcp.DestinationPort, masqueradingPort);
      var internalNode = new InternalNode(p_ip.SourceAddress, p_tcp.SourcePort, incomingPort, p_eth.SourceHwAddress);
      NAT_MapToInside[in_key] = internalNode;
      Console.WriteLine("Added mapping");
    }
    else if (!NAT_MapToOutside.TryGetValue(out_key, out masqueradingPort))
    {
      // Not a SYN, and no existing connection, so drop.
      return ForwardingDecision.Drop.Instance;
    }

    // Rewrite the packet.
    p_tcp.SourcePort = masqueradingPort;
    p_ip.SourceAddress = my_address;
    // Update checksums. NOTE IP updated before TCP
    ((IPv4Packet)p_ip).UpdateIPChecksum();
    p_tcp.UpdateTCPChecksum();
    // Update destination MAC address
    p_eth.DestinationHwAddress = next_outside_hop_mac;
    return (new ForwardingDecision.SinglePortForward(Port_Outside));
  }

  private ushort GetNewMasqueradingTcpPort()
  {
    lock(portLock)
    {
      // NOTE can't use Interlocked.Increment since the type of next_port is short not int.
      return next_port++;
    }
  }

  sealed class InternalNode : IEquatable<InternalNode>
  {
    private readonly IPAddress _Address; // FIXME we should really use immutable values since we are using a dictionary
    private readonly ushort _Port;
    private readonly int _InterfaceNumber;
    private readonly PhysicalAddress _MacAddress;
    public IPAddress Address { get { return _Address; } }
    public ushort Port { get { return _Port; } }
    public int InterfaceNumber { get { return _InterfaceNumber; } }
    public PhysicalAddress MacAddress { get { return _MacAddress; } }
    private readonly int hashCode;
#if DEBUG
    private string asString;
#endif

    public InternalNode(IPAddress address, ushort port, int interfaceNumber, PhysicalAddress macAddress)
    {
      if (ReferenceEquals(null, address)) throw new ArgumentNullException("address");
      if (ReferenceEquals(null, macAddress)) throw new ArgumentNullException("macAddress");

      _Address = address;
      _Port = port;
      _InterfaceNumber = interfaceNumber;
      _MacAddress = macAddress;
      // FIXME computing hash at construction relies on address being immutable (addr.Scope can change)
      hashCode = new { Address, Port, InterfaceNumber }.GetHashCode();
#if DEBUG
      asString = this.ToString();
#endif
    }

    public static bool operator ==(InternalNode a, InternalNode b)
    {
      return !ReferenceEquals(null, a) && a.Equals(b);
    }
    public static bool operator !=(InternalNode a, InternalNode b)
    {
      return !(a == b);
    }
    public override bool Equals(Object other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      if (other.GetType() != GetType()) return false;
      return this.Equals(other as InternalNode);
    }
    public bool Equals(InternalNode other)
    {
      return !ReferenceEquals(null, other)
        && Address.Equals(other.Address)
        && Port.Equals(other.Port)
        && InterfaceNumber.Equals(other.InterfaceNumber)
        && MacAddress.Equals(other.MacAddress);
    }
    public override int GetHashCode() { return hashCode; }
    public override string ToString()
    {
#if DEBUG
      if (asString != null)
        return asString;
      else
#endif
      return String.Format("{0}:{1} at {2} on port {3}",
        Address.ToString(), Port.ToString(), MacAddress.ToString(), InterfaceNumber.ToString());
    }
  }
  sealed class MapToInside_Key : IEquatable<MapToInside_Key>
  {
    private readonly IPAddress _SourceAddress; // FIXME we should really use immutable values since we are using a dictionary
    private readonly ushort _SourcePort, _ArrivalPort;
    public IPAddress SourceAddress { get { return _SourceAddress; } }
    public ushort SourcePort { get { return _SourcePort; } }
    public ushort ArrivalPort { get { return _ArrivalPort; } }
    private readonly int hashCode;
#if DEBUG
    private string asString;
#endif

    public MapToInside_Key(IPAddress sourceAddress, ushort sourcePort, ushort arrivalPort)
    {
      if (ReferenceEquals(null, sourceAddress)) throw new ArgumentNullException("sourceAddress");

      _SourceAddress = sourceAddress;
      _SourcePort = sourcePort;
      _ArrivalPort = arrivalPort;
      // FIXME computing hash at construction relies on address being immutable (addr.Scope can change)
      hashCode = new { SourceAddress, SourcePort, ArrivalPort }.GetHashCode();
#if DEBUG
      asString = this.ToString();
#endif
    }

    public static bool operator ==(MapToInside_Key a, MapToInside_Key b)
    {
      return !ReferenceEquals(null, a) && a.Equals(b);
    }
    public static bool operator !=(MapToInside_Key a, MapToInside_Key b)
    {
      return !(a == b);
    }
    public override bool Equals(Object other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      if (other.GetType() != GetType()) return false;
      return this.Equals(other as MapToInside_Key);
    }
    public bool Equals(MapToInside_Key other)
    {
      return !ReferenceEquals(null, other)
        && SourceAddress.Equals(other.SourceAddress)
        && SourcePort.Equals(other.SourcePort)
        && ArrivalPort.Equals(other.ArrivalPort);
    }
    public override int GetHashCode() { return hashCode; }
    public override string ToString()
    {
#if DEBUG
      if (asString != null)
        return asString;
      else
#endif
      return SourceAddress.ToString() + ":" + SourcePort.ToString() + " to :" + ArrivalPort.ToString();
    }
  }
  sealed class MapToOutside_Key : IEquatable<MapToOutside_Key>
  {
    // FIXME we should really use immutable values for IPAddress since we are using a dictionary
    private readonly IPAddress _SourceAddress, _DestinationAddress;
    private readonly ushort _SourcePort, _DestinationPort;
    public IPAddress SourceAddress { get { return _SourceAddress; } }
    public ushort SourcePort { get { return _SourcePort; } }
    public IPAddress DestinationAddress { get { return _DestinationAddress; } }
    public ushort DestinationPort { get { return _DestinationPort; } }
    private readonly int hashCode;
#if DEBUG
    private string asString;
#endif

    public MapToOutside_Key(IPAddress sourceAddress, ushort sourcePort,
                            IPAddress destinationAddress, ushort destinationPort)
    {
      if (ReferenceEquals(null, sourceAddress)) throw new ArgumentNullException("sourceAddress");
      if (ReferenceEquals(null, destinationAddress)) throw new ArgumentNullException("destinationAddress");

      _SourceAddress = sourceAddress;
      _SourcePort = sourcePort;
      _DestinationAddress = destinationAddress;
      _DestinationPort = destinationPort;
      // FIXME computing hash at construction relies on address being immutable (addr.Scope can change)
      hashCode = new { SourceAddress, SourcePort, DestinationAddress, DestinationPort }.GetHashCode();
#if DEBUG
      asString = this.ToString();
#endif
    }

    public static bool operator ==(MapToOutside_Key a, MapToOutside_Key b)
    {
      return !ReferenceEquals(null, a) && a.Equals(b);
    }
    public static bool operator !=(MapToOutside_Key a, MapToOutside_Key b)
    {
      return !(a == b);
    }
    public override bool Equals(Object other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      if (other.GetType() != GetType()) return false;
      return this.Equals(other as MapToOutside_Key);
    }
    public bool Equals(MapToOutside_Key other)
    {
      return !ReferenceEquals(null, other)
        && SourceAddress.Equals(other.SourceAddress)
        && SourcePort.Equals(other.SourcePort)
        && DestinationAddress.Equals(other.DestinationAddress)
        && DestinationPort.Equals(other.DestinationPort);
    }
    public override int GetHashCode() { return hashCode; }
    public override string ToString()
    {
#if DEBUG
      if (asString != null)
        return asString;
      else
#endif
      return SourceAddress.ToString() + ":" + SourcePort.ToString() + " to "
        + DestinationAddress.ToString() + ":" + DestinationPort.ToString();
    }
  }
}
