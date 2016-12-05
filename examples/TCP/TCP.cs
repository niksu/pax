/*
TCP
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/


using System;
using System.Net;
using System.Net.NetworkInformation;
using PacketDotNet;
using Pax;
using SharpPcap;
using System.Threading;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace Pax_TCP {

  // FIXME specify TCB
  public struct TCB {

  }

  // We have multiple threads of execution, of basically three kinds:
  // * we are called when anything of interest comes from a network interface
  //   with which we've been registered.
  // * the Berkeley socket functions are called from the applications using TCP.
  //   we return to them based on the settings (non/blocking) and protocol
  //   state (e.g., buffer contents, or socket's closed, etc) and heuristics (e.g., wait until buffer is more full).
  // * timers for checking up on things -- retransmitting.
  //   timers are set by timers (e.g., retransmit) and by Berkeley socket functions (write).
  public class TCPuny : PacketMonitor, IBerkeleySocket, IActive {
    ICaptureDevice device = null; // NOTE if this is made into an array, we can model multipath behaviour?
    uint max_conn;
    uint max_backlog;
    IPAddress ip_address;
    PhysicalAddress mac_address;

    int interval = 5000; //FIXME const
    Timer timer; // FIXME we're going to need more than one

    ConcurrentQueue<Packet> out_q = new ConcurrentQueue<Packet>();

    // FIXME instantiate TCB

    public TCPuny (uint max_conn, uint max_backlog, IPAddress ip_address, PhysicalAddress mac_address) {
      this.max_conn = max_conn;
      this.max_backlog = max_backlog;
      // We get our addresses via the constructor.
      // NOTE we learn about "device" via "PreStart".
      this.ip_address = ip_address;
      this.mac_address = mac_address;
    }

    override public ForwardingDecision process_packet (int in_port, ref Packet packet)
    {
      if (packet is EthernetPacket &&
        packet.Encapsulates(typeof(IPv4Packet), typeof(TcpPacket)))
      {
          EthernetPacket eth_p = (EthernetPacket)packet;
          IpPacket ip_p = ((IpPacket)(packet.PayloadPacket));
          TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));

          // FIXME do something with tcp_p
          if (ip_p.DestinationAddress.Equals(ip_address) && tcp_p.DestinationPort == 30) {
            //if (tcp_p.Syn) {
              var dst_mac = eth_p.DestinationHwAddress;
              eth_p.DestinationHwAddress = eth_p.SourceHwAddress;
              eth_p.SourceHwAddress = dst_mac;
              var dst_ip = ip_p.DestinationAddress;
              ip_p.DestinationAddress = ip_p.SourceAddress;
              ip_p.SourceAddress = dst_ip;
              var dst_port = tcp_p.DestinationPort;
              tcp_p.DestinationPort = tcp_p.SourcePort;
              tcp_p.SourcePort = dst_port;

              tcp_p.Rst = true;

              // Rather than sending the packet now, we enqueue it for sending
              // when the timer expires.
//              device.SendPacket(eth_p);
              out_q.Enqueue(eth_p);
            //}
          }
      }

      // FIXME might be more efficient to send RSTs through here rather than
      //       through timer.
      /*
      packet = eth_p;
      return (new ForwardingDecision.SinglePortForward(in_port));
      */
      return ForwardingDecision.Drop.Instance;
    }

    public Result<SockID> socket (Internet_Domain domain, Internet_Type type, Internet_Protocol prot) {
      throw new Exception("TODO");
    }

    public Result<bool> bind (SockID sid, SockAddr_In address) {
      throw new Exception("TODO");
    }

    public Result<bool> listen (SockID sid) {
      throw new Exception("TODO");
    }

    public Result<SockID> accept (SockID sid, out SockAddr_In address) {
      throw new Exception("TODO");
    }

    public Result<int> write (SockID sid, byte[] buf, uint count) {
      throw new Exception("TODO");
    }

    public Result<int> read (SockID sid, byte[] buf, uint count) {
      throw new Exception("TODO");
    }

    public Result<bool> close (SockID sid) {
      throw new Exception("TODO");
    }

    public void PreStart (ICaptureDevice device) {
      Debug.Assert(this.device == null);
      this.device = device;
      Console.WriteLine ("TCPuny configured");
    }

    public void Stop () {
      if (timer != null) {timer.Dispose();}
      Console.WriteLine ("TCPuny stopped");
    }

    public void Start () {
      Console.WriteLine ("TCPuny starting");
      timer = new Timer(Flush, null, 0, interval);
    }

    public void Flush (Object o) {
      Packet p;
      while (out_q.TryDequeue (out p)) {
        device.SendPacket(p);
        Console.Write (".");
      }
    }
  }
}
