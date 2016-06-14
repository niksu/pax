/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net.NetworkInformation;
using System.Collections.Concurrent;
using PacketDotNet;
using System.Diagnostics;
using System.Net;
using Pax;

/*
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
public class NAT : SimplePacketProcessor {
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
    // be 0 when NAT_Entry represents the inside host.
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
      // Use the method from IEquatable<T>.
      return this.Equals((Object)other);
    }

  }

  IPAddress my_address;
  ushort next_port;

  public NAT (IPAddress my_address, ushort next_port) {
    this.my_address = my_address;
    this.next_port = next_port;
  }

  // We keep 2 dictionaries, one for queries related to packets crossing from the outside (O) to the inside (I), and
  // the other for the inverse.
  // O --> I: when we get a packet on port 0, to find out how to rewrite the packet and forward it on which internal port.
  // I --> O: when we get a packet on port != 0, to find out how to rewrite the packet before forwarding it on port 0.
  ConcurrentDictionary<NAT_Entry,NAT_Entry> port_mapping =
    new ConcurrentDictionary<NAT_Entry,NAT_Entry>();
  ConcurrentDictionary<NAT_Entry,NAT_Entry> port_reverse_mapping =
    new ConcurrentDictionary<NAT_Entry,NAT_Entry>();

  private int outside_to_inside (IpPacket p_ip, TcpPacket p_tcp, NAT_Entry from, int in_port, ref Packet packet)
  {
    from.assigned_tcp_port = p_tcp.DestinationPort;
    from.network_port = null;

    // Retrieve the mapping. If a mapping doesn't exist, then drop the packet.
    // Rewrite destination IP address and TCP port, and map to the (non-zero) port.
    if (port_mapping.ContainsKey(from))
    {
      NAT_Entry to = port_mapping[from];
      p_ip.DestinationAddress = to.ip_address;
      p_tcp.DestinationPort = to.tcp_port;
      // Update packet.
      p_ip.PayloadPacket = p_tcp;
      packet.PayloadPacket = p_ip;
      return to.network_port.Value;
    }
    else
    {
      return -1;//FIXME const
    }
  }

  private int inside_to_outside (IpPacket p_ip, TcpPacket p_tcp, NAT_Entry from, int in_port, ref Packet packet)
  {
    // In this case, we're querying the dictionary with "reversed" keying,
    // since we want to find out how the destination IP address and destination
    // TCP port need to be rewritten before forwarding to the outside.

    from.assigned_tcp_port = null;
    from.network_port = in_port;

    if (p_tcp.Syn)
    {
      // If a TCP SYN, then add a mapping.
      // NOTE we simply overwrite any previous mapping that had the same key.

      NAT_Entry to = new NAT_Entry();
      to.ip_address = p_ip.DestinationAddress;
      to.tcp_port = p_tcp.DestinationPort;

      // Generate data for the mapping
      lock(my_address)
      {
        to.assigned_tcp_port = next_port++;;
      }

      port_mapping[to] = from;
      port_reverse_mapping[from] = to;

      // Rewrite the packet.
      p_tcp.SourcePort = to.assigned_tcp_port.Value;
      p_ip.SourceAddress = my_address;
      // Update packet.
      p_ip.PayloadPacket = p_tcp;
      packet.PayloadPacket = p_ip;
      return 0; //FIXME const
    } else {
      // Not a SYN, so this must be part of an ongoing connection.
      if (!port_reverse_mapping.ContainsKey(from))
      {
        // if we don't have a mapping for it then drop.
        return -1;//FIXME const
      } else {
        //  otherwise apply the mapping (replacing source IP address and TCP port) and forward to the zero port.
        NAT_Entry to = port_reverse_mapping[from];
        p_ip.SourceAddress = to.ip_address;
        p_tcp.SourcePort = to.tcp_port;
        // Update packet.
        p_ip.PayloadPacket = p_tcp;
        packet.PayloadPacket = p_ip;
        return 0; //FIXME const
      }
    }
  }

  override public int handler (int in_port, ref Packet packet)
  {
    int out_port = -1;

    // We drop anything other than TCP packets
    if (!(packet is PacketDotNet.EthernetPacket))
    {
      return -1;//FIXME const
    }

    IpPacket p_ip = ((IpPacket)(packet.PayloadPacket));
    TcpPacket p_tcp = ((PacketDotNet.TcpPacket)(p_ip.PayloadPacket));

    NAT_Entry k = new NAT_Entry(); //FIXME for increased efficiency could avoid this allocation, and use a thread-local but static one done at the start?
    k.ip_address = p_ip.SourceAddress;
    k.tcp_port = p_tcp.SourcePort;

    if (in_port == 0/*FIXME const*/)
    {
      out_port = outside_to_inside (p_ip, p_tcp, k, in_port, ref packet);
    } else {
      out_port = inside_to_outside (p_ip, p_tcp, k, in_port, ref packet);
    }

    return out_port;
  }
}
