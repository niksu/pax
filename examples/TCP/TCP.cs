/*
TCP
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/


using System;
using PacketDotNet;
using Pax;

public class TCP : SimplePacketProcessor, IBerkeleySocket {

  override public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    if (packet is EthernetPacket &&
      packet.Encapsulates(typeof(IPv4Packet), typeof(TcpPacket)))
    {
        IpPacket ip_p = ((IpPacket)(packet.PayloadPacket));
        TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));

        // FIXME do something with tcp_p
    }

    // FIXME return nothing?
    return (new ForwardingDecision.SinglePortForward(in_port + 1));
  }

  // FIXME function to send a segment

}
