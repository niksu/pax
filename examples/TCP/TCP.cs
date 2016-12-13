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
  // * timers for checking up on things -- i.e., retransmitting a segment if it
  //   has not yet been acknowledged..
  //   timers are set by timers (e.g., retransmit) and by Berkeley socket functions (write).
  public class TCPuny : PacketMonitor, IActiveBerkeleySocket {
    ICaptureDevice device = null; // NOTE if this is made into an array, we can model multipath behaviour?
    uint max_conn;
    uint max_backlog;
    IPAddress ip_address;
    PhysicalAddress mac_address;

    int interval = 5000; //FIXME const
    Timer timer; // FIXME we're going to need more than one

    ConcurrentQueue<Packet> in_q = new ConcurrentQueue<Packet>();
    ConcurrentQueue<Packet> out_q = new ConcurrentQueue<Packet>();

    // FIXME instantiate TCB

    // FIXME need to work through logic of how TCPuny is started, as well as how
    //       the user application initialises it.
    public TCPuny (uint max_conn, uint max_backlog,
        // FIXME do we really want ip_address and mac_address given at this
        //       stage? i'd expect ip_address to be given at "bind" step,
        //       and mac_address from the config.
        IPAddress ip_address, PhysicalAddress mac_address) {
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

          if (ip_p.DestinationAddress.Equals(ip_address)) {
            in_q.Enqueue(eth_p);
          }

// FIXME clean this up
//          TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));
//
//          if (ip_p.DestinationAddress.Equals(ip_address) && tcp_p.DestinationPort == 30/*FIXME nonsense*/) {
////            //if (tcp_p.Syn) {
////              var dst_mac = eth_p.DestinationHwAddress;
////              eth_p.DestinationHwAddress = eth_p.SourceHwAddress;
////              eth_p.SourceHwAddress = dst_mac;
////              var dst_ip = ip_p.DestinationAddress;
////              ip_p.DestinationAddress = ip_p.SourceAddress;
////              ip_p.SourceAddress = dst_ip;
////              var dst_port = tcp_p.DestinationPort;
////              tcp_p.DestinationPort = tcp_p.SourcePort;
////              tcp_p.SourcePort = dst_port;
////
////              tcp_p.Rst = true;
////
////              // Rather than sending the packet now, we enqueue it for sending
////              // when the timer expires.
//////              device.SendPacket(eth_p);
////              out_q.Enqueue(eth_p);
//            //}
//          }
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
      // Add TCB if there's space.
      throw new Exception("TODO");
    }

    public Result<Unit> bind (SockID sid, SockAddr_In address) {
      // Update TCB (as long as connection isn't live!)
      // Check that we can use address+port?
      throw new Exception("TODO");
    }

    public Result<Unit> listen (SockID sid) {
      // Lookup TCB etc
      // Set to LISTEN state
      throw new Exception("TODO");
    }

    public Result<SockID> accept (SockID sid, out SockAddr_In address) {
      // FIXME this must be blocking, right?
      // FIXME shall we add a parameter over which is can be shutdown, and a
      //       function to call when the shutdown request arrives?
      throw new Exception("TODO");
      // Look up the TCB
    }

    public Result<int> write (SockID sid, byte[] buf, uint count) {
      Debug.Assert(count > 0);
      // Look up the TCB
      // Check whether we're blocking or non-blocking
      // Check how many bytes we can fit into the send buffer, commit to it.
      // Insert that many bytes (updating the index), copying the bytes from 'buf'
      // Return the number of bytes we've inserted.
      // (Block if necessary, otherwise return 0)
      // FIXME use heuristics to avoid sending small packets?
      // FIXME start retransmission timer immediately?
      throw new Exception("TODO");
    }

    public Result<int> read (SockID sid, byte[] buf, uint count) {
      Debug.Assert(count > 0);
      // Look up the TCB
      // Check whether we're blocking or non-blocking
      // Check how many bytes we can send, commit to it.
      // Extract that many bytes (updating the index), copying the bytes to 'buf'
      // Return the number of bytes we've extracted.
      // (Block if necessary, otherwise return 0)
      throw new Exception("TODO");
    }

    public Result<Unit> close (SockID sid) {
      // Look up the TCB
      // Progress it further to a closed state
      // Set timer to remove the record after 2MSL, ultimately
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
      // FIXME start a loop whereby we dequeue packets from in_q and process
      //       them.
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
