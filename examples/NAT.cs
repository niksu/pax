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

public class NAT : SimplePacketProcessor {
  public const int Port_Drop = -1;
  public const int Port_Outside = 0;

  private IPAddress my_address;
  private ushort next_port;

  public NAT () {
    // FIXME raise an exception if this constructor was used, since currently only the other constructor initialises things properly.
  }

  public NAT (IPAddress my_address, ushort next_port) {
    this.my_address = my_address;
    this.next_port = next_port;
  }

  // We keep 2 dictionaries, one for queries related to packets crossing from the outside (O) to the inside (I), and
  // the other for the inverse.
  // O --> I: when we get a packet on port Port_Outside, to find out how to rewrite the packet and forward it on which internal port.
  // I --> O: when we get a packet on port != Port_Outside, to find out how to rewrite the packet before forwarding it on port Port_Outside.
  ConcurrentDictionary<NAT_Entry,NAT_Entry> port_mapping =
    new ConcurrentDictionary<NAT_Entry,NAT_Entry>();
  ConcurrentDictionary<NAT_Entry,NAT_Entry> port_reverse_mapping =
    new ConcurrentDictionary<NAT_Entry,NAT_Entry>();

  override public int handler (int in_port, ref Packet packet)
  {
    // We drop anything other than TCP packets that are encapsulated in IPv4,
    // and in Ethernet.
    if (!(packet is PacketDotNet.EthernetPacket) ||
        !packet.Encapsulates(typeof(IPv4Packet), typeof(TcpPacket)))
    {
      return Port_Drop;
    }

    // Unencapsulate the packets, so we can read and change their fields more easily.
    IpPacket p_ip = ((IpPacket)(packet.PayloadPacket));
    TcpPacket p_tcp = ((PacketDotNet.TcpPacket)(p_ip.PayloadPacket));

    // Prepare the structure with which we'll query our port mapping.
    NAT_Entry from = new NAT_Entry(); //FIXME for increased efficiency could avoid this allocation, and use a thread-local but static one done at the start?
    from.ip_address = p_ip.SourceAddress;
    from.tcp_port = p_tcp.SourcePort;

    int out_port = Port_Drop;
    if (in_port == Port_Outside)
    {
      from.assigned_tcp_port = p_tcp.DestinationPort;
      from.network_port = null;
      out_port = outside_to_inside (p_ip, p_tcp, from);
    } else {
      from.assigned_tcp_port = null;
      from.network_port = in_port;
      out_port = inside_to_outside (p_ip, p_tcp, from);
    }

#if DEBUG
    print_mapping (port_mapping, " (------ Outside ------) ", " (------ Inside ------) ");
    Console.WriteLine();
    print_mapping (port_reverse_mapping, " (------ Inside ------) ", " (------ Outside ------) ");
#endif

    return out_port;
  }

  private int outside_to_inside (IpPacket p_ip, TcpPacket p_tcp, NAT_Entry from)
  {
    // Retrieve the mapping. If a mapping doesn't exist, then it means that we're not
    // aware of a session to which the packet belongs: so drop the packet.
    NAT_Entry to;
    if (port_mapping.TryGetValue(from, out to))
    {
      // Rewrite destination IP address and TCP port, and map to the appropriate Inside port.
      p_ip.DestinationAddress = to.ip_address;
      p_tcp.DestinationPort = to.tcp_port;
      // Update checksums.
      p_tcp.UpdateTCPChecksum();
      ((IPv4Packet)p_ip).UpdateIPChecksum();
      return to.network_port.Value;
    }
    else
    {
      return Port_Drop;
    }
  }

  private int inside_to_outside (IpPacket p_ip, TcpPacket p_tcp, NAT_Entry from)
  {
    // In this case, we're querying the dictionary with "reversed" keying,
    // since we want to find out how the destination IP address and destination
    // TCP port need to be rewritten before forwarding to the outside.

    NAT_Entry to;
    if (p_tcp.Syn)
    {
      // If a TCP SYN, then add a mapping.

      // We first delete any previous mapping that had the same key.
      if (port_reverse_mapping.TryGetValue(from, out to))
      {
        // We don't really care about the boolean result returned by TryRemove,
        // since if "true" then the key-value pair was removed (great), but
        // if "false" then the key-value pair must have been already removed (great).
        port_mapping.TryRemove(port_reverse_mapping[from], out to);
        port_reverse_mapping.TryRemove(from, out to);
      } // FIXME in case TryGetValue returns false, we could simply do TryUpdate or TryAdd instead of the TryRemove and TryAdd.

      to = new NAT_Entry();
      to.ip_address = p_ip.DestinationAddress;
      to.tcp_port = p_tcp.DestinationPort;

      // Generate data for the mapping
      lock(my_address)
      {
        // NOTE can't use Interlocked.Increment since the type of next_port is short not int.
        to.assigned_tcp_port = next_port++;
      }

      if (!port_mapping.TryAdd(to, from))
      {
        Console.WriteLine("Concurrent update of port_mapping[to] where 'to'={0}", to);
      }
      if (!port_reverse_mapping.TryAdd(from, to))
      {
        Console.WriteLine("Concurrent update of port_reverse_mapping[from] where 'from'={0}", from);
      }

      // Rewrite the packet.
      p_tcp.SourcePort = to.assigned_tcp_port.Value;
      p_ip.SourceAddress = my_address;
      // Update checksums.
      p_tcp.UpdateTCPChecksum();
      ((IPv4Packet)p_ip).UpdateIPChecksum();
      return Port_Outside;
    } else {
      // Not a SYN, so this must be part of an ongoing connection.
      if (!port_reverse_mapping.TryGetValue(from, out to))
      {
        // if we don't have a mapping for it then drop.
        return Port_Drop;
      } else {
        //  otherwise apply the mapping (replacing source IP address and TCP port) and forward to the Outside port.
        p_ip.SourceAddress = to.ip_address;
        p_tcp.SourcePort = to.tcp_port;
        // Update checksums.
        p_tcp.UpdateTCPChecksum();
        ((IPv4Packet)p_ip).UpdateIPChecksum();
        return Port_Outside;
      }
    }
  }

#if DEBUG
  private void print_mapping (ConcurrentDictionary<NAT_Entry,NAT_Entry> mapping, string h1, string h2) {
    Console.WriteLine("{0} \t<->\t {1}", h1, h2);
    foreach (var entry in mapping)
    {
      Console.WriteLine("{0} \t<->\t {1}", entry.Key, entry.Value);
    }
  }
#endif

  // Entries in the mapping that the NAT keeps to track ongoing TCP connections.
  // A NAT_Entry can represent the "internal" or "external" entity between which the NAT mediates.
  class NAT_Entry : IEquatable<NAT_Entry> {
    public IPAddress ip_address {get; set;}
    public ushort tcp_port {get; set;}
    // "assigned_tcp_port" is null when NAT_Entry represents the internal host,
    // since it tells us a "public" value in the relationship. It is the TCP
    // port on the NAT's public interface with which the external side
    // communicates.
    public ushort? assigned_tcp_port {get; set;}
    // This is null when NAT_Entry represents the outside host, and should never
    // be Port_Outside when NAT_Entry represents the inside host.
    public int? network_port {get; set;}

    public override bool Equals (Object other_obj) {
      if (other_obj is NAT_Entry)
      {
        NAT_Entry other = other_obj as NAT_Entry;

        bool eq_ip = this.ip_address.Equals(other.ip_address);
        bool eq_port = this.tcp_port.Equals(other.tcp_port);
        bool eq_atp = this.assigned_tcp_port.Equals(other.assigned_tcp_port);
        bool eq_np = this.network_port.Equals(other.network_port);
        return (eq_ip && eq_port && eq_atp && eq_np);
      } else {
        throw (new Exception ("A NAT_Entry may only be compared with another NAT_Entry."));
      }
    }

    public bool Equals (NAT_Entry other) {
      // For IEquatable<T> use the overridden method from Object.
      return this.Equals((Object)other);
    }

    public static bool operator== (NAT_Entry x, NAT_Entry y) {
      return (x.Equals(y));
    }

    public static bool operator!= (NAT_Entry x, NAT_Entry y) {
      return (!x.Equals(y));
    }

    public override int GetHashCode() {
      // FIXME this is a bit heavy to compute
      return this.ToString().GetHashCode();
    }

    public override string ToString() {
      return ("(" +
          ip_address.ToString() +
          ":" + tcp_port.ToString() +
          " " + (assigned_tcp_port.HasValue ? Convert.ToString(assigned_tcp_port.Value) : ".") +
          "|" + (network_port.HasValue ? Convert.ToString(network_port.Value) : ".") +
          ")");
    }
  }
}
