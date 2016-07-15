/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016
Jonny Shipton, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.

This class implements a NAT (Network Address Translation) middlebox using
Pax. The NAT mediates between two networks, which we call "Outside" and "Inside",
and multiplexes TCP and UDP connections originating from Inside to Outside, and allows
Outside to participate in such connections.

                                 ---------
                                 |      [1]-- ...
               (Outside) ... ---[0] NAT [2]-- ... (Inside)
                                 |      ...
                                 |      [n]-- ...
                                 ---------

It maps TCP connections arriving on non-zero ports, onto the NAT's zero port.
It maintains a mapping of active TCP connections. It adds an entry to this mapping
whenever it receives a TCP SYN packet coming from a non-zero port; it assigns a fresh
ephemeral TCP port on its zero network port, and updates the source IP address to its
own. It then forwards the packet onto its zero port. It removes an entry when it's no
longer needed (because of a FIN-shutdown and timeout) or because an activity timer has
expired. As long as it has an entry, it maps TCP segments from non-zero to zero ports,
masquerading the client.
Currently RST packets do not cause entries to be removed. Instead, the inactivity
resulting from the connection being closed eventually triggers the removal.
UDP connections are treated in a similar manner, but any UDP packet from Inside can
cause an entry to be added where there wasn't one before.

NOTE could improve the implementation by having configurable "forwarding ports"
     that enable you to run servers on non-zero network ports.
*/

using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Timers;
using PacketDotNet;

namespace Pax.Examples.Nat
{
  /// <summary>
  /// A packet processor that performs Network Address Translation (a NAT).
  /// </summary>
  public sealed class NAT : SimplePacketProcessor
  {
    /// A value indicating the packet should be dropped.
    public const int Port_Drop = -1;

    private readonly Timer gcTimer;

    // Use a separate namespace for each transport protocol
    private TcpNAT tcpNat;
    private UdpNAT udpNat;

    /// <summary>
    /// Creates a new NAT packet processor that handles TCP and UDP packets.
    /// </summary>
    /// <param name="my_address">The public IP address of the NAT</param>
    /// <param name="next_outside_hop_mac">The MAC address of the next hop on the outside-facing port.</param>
    /// <param name="tcp_inactivity_timeout">The time that should elapse before a TCP connection with no activity is removed.</param>
    /// <param name="tcp_time_wait_duration">The time that should elapse before a closed TCP connection is removed.</param>
    /// <param name="tcp_start_port">The start of the range of TCP ports to use (inclusive).</param>
    /// <param name="tcp_end_port">The end of the range of TCP ports to use (inclusive).</param>
    /// <param name="udp_inactivity_timeout">The time that should elapse before a UDP connection with no activity is removed.</param>
    /// <param name="udp_start_port">The start of the range of UDP ports to use (inclusive).</param>
    /// <param name="udp_end_port">The end of the range of UDP ports to use (inclusive).</param>
    public NAT (IPAddress my_address, PhysicalAddress next_outside_hop_mac,
      TimeSpan tcp_inactivity_timeout, TimeSpan tcp_time_wait_duration, ushort tcp_start_port, ushort tcp_end_port,
      TimeSpan udp_inactivity_timeout, ushort udp_start_port, ushort udp_end_port)
    {
      // Instantiate each NAT specialisation
      tcpNat = new TcpNAT(my_address, next_outside_hop_mac, tcp_inactivity_timeout, tcp_time_wait_duration, tcp_start_port, tcp_end_port);
      udpNat = new UdpNAT(my_address, next_outside_hop_mac, udp_inactivity_timeout, udp_start_port, udp_end_port);

      // Call the GarbageCollectConnections method regularly
      gcTimer = new Timer(1000); // FIXME should the GC frequency be configurable?
      gcTimer.Elapsed += GarbageCollectConnections;
      gcTimer.AutoReset = true;
      gcTimer.Start();
    }

    /// <summary>
    /// Handle an observed packet. Determines the type of packet and passes it to the relevant specialised handler.
    /// </summary>
    /// <param name="incomingNetworkInterface">The number representing network interface the packet arrived on.</param>
    /// <param name="packet">The observed packet.</param>
    /// <returns>A <see cref="ForwardingDecision"/> for the packet.</returns>
    override public ForwardingDecision process_packet(int incomingNetworkInterface, ref Packet packet)
    {
      if (packet is EthernetPacket)
      {
        if (packet.PayloadPacket is IpPacket)
        {
          // Case on the type of the transport-layer packet:
          Packet transportLayerPacket = packet.PayloadPacket.PayloadPacket;
          if (transportLayerPacket is TcpPacket)
          {
            var tcp = new TcpPacketEncapsulation(packet);
#if DEBUG
            Console.WriteLine("RX TCP {0}:{1} -> {2}:{3} on {4} [{5}{6}{7}{8}]",
              tcp.NetworkPacket.SourceAddress, tcp.TransportPacket.SourcePort, tcp.NetworkPacket.DestinationAddress, tcp.TransportPacket.DestinationPort, incomingNetworkInterface,
              tcp.TransportPacket.Syn ? "S" : "", tcp.TransportPacket.Fin ? "F" : "", tcp.TransportPacket.Rst ? "R" : "", tcp.TransportPacket.Ack ? "." : "");
#endif
            return tcpNat.handlePacket(tcp, incomingNetworkInterface);
          }
          else if (transportLayerPacket is UdpPacket)
          {
            var udp = new UdpPacketEncapsulation(packet);
#if DEBUG
            Console.WriteLine("RX UDP {0}:{1} -> {2}:{3} on {4}",
              udp.NetworkPacket.SourceAddress, udp.TransportPacket.SourcePort, udp.NetworkPacket.DestinationAddress, udp.TransportPacket.DestinationPort, incomingNetworkInterface);
#endif
            return udpNat.handlePacket(new UdpPacketEncapsulation(packet), incomingNetworkInterface);
          }
#if DEBUG
          else
          {
            Console.WriteLine("RX ? {0}", transportLayerPacket.GetType().Name);
          }
#endif
        }
      }

      // If we reach this point then we can't handle that type of packet
      return new ForwardingDecision.SinglePortForward(Port_Drop);
    }

    /// <summary>
    /// Remove any eligible connections from each of the specialised NATs.
    /// </summary>
    private void GarbageCollectConnections(object sender, ElapsedEventArgs e)
    {
#if DEBUG
      Console.WriteLine("GC");
#endif
      tcpNat.GarbageCollectConnections();
      udpNat.GarbageCollectConnections();
    }
  }
}
