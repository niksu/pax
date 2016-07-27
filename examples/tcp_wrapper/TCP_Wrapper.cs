/*
Implementing a network-based "TCP wrapper".
Nik Sultana, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/


using System;
using PacketDotNet;
using Pax;

// FIXME rather than print to console it would be nicer to emit a syslog-over-UDP
//       packet to some logging device.

public abstract class TCP_Wrapper : SimplePacketProcessor {

  // Determines whether we should log seeing this packet.
  abstract protected bool predicate (TcpPacket tcp_p);

  // We forward to in_port+1 whatever happens.
  override public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    if (packet is EthernetPacket &&
      packet.Encapsulates(typeof(IPv4Packet), typeof(TcpPacket)))
    {
        IpPacket ip_p = ((IpPacket)(packet.PayloadPacket));
        TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));

        if (predicate(tcp_p))
        {
          Console.WriteLine ("TCPW> " + ip_p.SourceAddress);
        }
    }

    return (new ForwardingDecision.SinglePortForward(in_port + 1));
  }
}
