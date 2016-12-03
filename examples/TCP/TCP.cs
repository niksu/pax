/*
TCP
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/


using System;
using System.Net;
using PacketDotNet;
using Pax;

namespace Pax_TCP {

  public class TCP : SimplePacketProcessor, IBerkeleySocket {
    // FIXME specify TCB

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

    public Result<SockID> socket (Internet_Domain domain, Internet_Type type, Internet_Protocol prot) {
      throw new Exception("TODO");
    }

    public Result<bool> bind (SockID sid, SockAddr_In address) {
      throw new Exception("TODO");
    }

    public Result<bool> listen (SockID sid, uint backlog) {
      throw new Exception("TODO");
    }

    public Result<SockID> accept (SockID sid, out SockAddr_In address) {
      throw new Exception("TODO");
    }

    public Result<int> write (SockID sid, byte[] buf) {
      throw new Exception("TODO");
    }

    public Result<int> read (SockID sid, out byte[] buf, uint count) {
      throw new Exception("TODO");
    }

    public Result<bool> close (SockID sid) {
      throw new Exception("TODO");
    }
  }
}
