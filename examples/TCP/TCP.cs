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
    bool running = false; // FIXME having to check this slows things down much?

    uint next_sock_id = 0;

    int interval = 5000; //FIXME const
    Timer timer; // FIXME we're going to need more than one

    ConcurrentQueue<Packet> in_q = new ConcurrentQueue<Packet>();
    ConcurrentQueue<Packet> out_q = new ConcurrentQueue<Packet>();

    TCB[] tcbs;

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

      tcbs = new TCB[max_conn];
      for (int i = 0; i < max_conn; i++) {
        tcbs[i].state = TCP_State.Free;
        tcbs[i].remote_address = null;
        tcbs[i].remote_port = 0;
        tcbs[i].local_port = 0;

        // FIXME add a NaN?
        tcbs[i].unacked_send = 0;
        tcbs[i].next_send = 0;
        tcbs[i].send_window_size = 0; // FIXME what?
        tcbs[i].retransmit_count = 0;
        tcbs[i].sending_max_seg_size = 0; // FIXME what?

        // FIXME consts -- make buffer sizes into parameters.
        tcbs[i].receive_buffer = new Packet[100];
        tcbs[i].send_buffer = new Packet[100];
      }
    }

    override public ForwardingDecision process_packet (int in_port, ref Packet packet)
    {
      if (running && packet is EthernetPacket &&
        packet.Encapsulates(typeof(IPv4Packet), typeof(TcpPacket)))
      {
        EthernetPacket eth_p = (EthernetPacket)packet;
        IpPacket ip_p = ((IpPacket)(packet.PayloadPacket));

        if (ip_p.DestinationAddress.Equals(ip_address)) {
          // FIXME before putting this in in_q could check the TCBs
          //       for whether the packet's relevant to us.
          in_q.Enqueue(eth_p);
        }
      }

      return ForwardingDecision.Drop.Instance;
    }

    private int find_free_TCB() {
      // FIXME linear search not efficient.
      for (int i = 0; i < max_conn; i++) {
        if (tcbs[i].state == TCP_State.Free) {
          return i;
        }
      }

      return -1;
    }

    public Result<SockID> socket (Internet_Domain domain, Internet_Type type, Internet_Protocol prot) {
      // Add TCB if there's space.
      int free_TCB;
      lock (this) {
        free_TCB = find_free_TCB();
        if (free_TCB < 0) {
          return new Result<SockID> (null, Error.ENOSP);
        }

        tcbs[free_TCB].state = TCP_State.Closed;
      }

      SockID sid = new SockID((uint)free_TCB);
      return new Result<SockID> (sid, null);
    }

    public Result<Unit> bind (SockID sid, SockAddr_In address) {
      // FIXME doesn't make sense to initialise TCP to an IP address, to then
      //       only accept to bind connection to that address -- one of the
      //       parameters can be dropped.
      Debug.Assert(ip_address.Equals(address.address));
      // FIXME check that address+port isn't already bound (by this TCP
      //       instance, by consulting the TCBs).

      if (tcbs[sid.sockid].state != TCP_State.Closed) {
        return new Result<Unit> (Unit.Value, Error.EFAULT);//FIXME is this the right code?
      }

      tcbs[sid.sockid].local_port = address.port;
      // NOTE we don't use address.address here, maybe we should drop that value
      //      since it's redundant?

      return new Result<Unit> (Unit.Value, null);
    }

    public Result<Unit> listen (SockID sid) {
      // Lookup TCB etc
      // Set to LISTEN state
//      throw new Exception("TODO");
      return new Result<Unit> (Unit.Value, null);
    }

    public Result<SockID> accept (SockID sid, out SockAddr_In address) {
      // FIXME this must be blocking, right?
      // FIXME shall we add a parameter over which is can be shutdown, and a
      //       function to call when the shutdown request arrives?
//      throw new Exception("TODO");
      // Look up the TCB
while (true) {}
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
      running = false;
      if (timer != null) {timer.Dispose();}
      Console.WriteLine ("TCPuny stopped");
    }

    public void Start () {
      Console.WriteLine ("TCPuny starting");
      running = true;
      timer = new Timer(Flush, null, 0, interval);
      // FIXME start a loop whereby we dequeue packets from in_q and process
      //       them.
      Packet p;
      while (running) {
        while (in_q.TryDequeue (out p)) {
          device.SendPacket(p);
          Console.Write (".");
        }
      }
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
