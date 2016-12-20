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
  //   state (e.g., buffer contents, or socket's closed, etc) and heuristics
  //   (e.g., wait until buffer is more full).
  // * timers for checking up on things -- i.e., retransmitting a segment if it
  //   has not yet been acknowledged.
  //   Timers are set by by the output thread (DispatchOutputSegments) on behalf
  //    of earlier decisions which themselves might have been the result of timer
  //    expiry (e.g., retransmit) or Berkeley socket functions being called
  //    (write).
  public class TCPuny : PacketMonitor, IActiveBerkeleySocket {
    ICaptureDevice device = null; // NOTE if this is made into an array, we can model multipath behaviour?
    uint max_conn;
    uint max_backlog;
    IPAddress ip_address;
    PhysicalAddress mac_address;
    bool running = false; // FIXME having to check this slows things down much?

    uint next_sock_id = 0;

    uint max_InQ_size;
    uint max_timers;
    uint max_tcb_timers;

    // By design we minimise the scope of logic as much as possible, trying to
    // limit it to making small changes to data, and moving information between
    // queues between such specialised data processors.
    ConcurrentQueue<Tuple<TcpPacket, TCB>> in_q = new ConcurrentQueue<Tuple<TcpPacket, TCB>>();
    ConcurrentQueue<Tuple<Packet, TCB>> out_q = new ConcurrentQueue<Tuple<Packet, TCB>>();
    ConcurrentQueue<Tuple<Packet, TCB, TimerCB>> timer_q = new ConcurrentQueue<Tuple<Packet, TCB, TimerCB>>();
    ConcurrentQueue<TCB> conn_q = new ConcurrentQueue<TCB>();

    TCB[] tcbs;
    TimerCB[] timer_cbs;

    // FIXME need to work through logic of how TCPuny is started, as well as how
    //       the user application initialises it.
    public TCPuny (uint max_conn, uint max_backlog,
        // FIXME do we really want ip_address and mac_address given at this
        //       stage? i'd expect ip_address to be given at "bind" step,
        //       and mac_address from the config.
        IPAddress ip_address, PhysicalAddress mac_address,
        uint receive_buffer_size, uint send_buffer_size, uint max_InQ_size,
        uint max_timers, uint max_tcb_timers) {
      this.max_conn = max_conn;
      this.max_backlog = max_backlog;
      // We get our addresses via the constructor.
      // NOTE we learn about "device" via "PreStart".
      this.ip_address = ip_address;
      this.mac_address = mac_address;
      this.max_InQ_size = max_InQ_size;
      this.max_timers = max_timers;
      this.max_tcb_timers = max_tcb_timers;

      TCB.local_address = ip_address;
      TimerCB.timer_q = this.timer_q;

      tcbs = new TCB[max_conn];
      for (int i = 0; i < max_conn; i++) {
        tcbs[i] = new TCB(receive_buffer_size, send_buffer_size, max_tcb_timers);
      }

      timer_cbs = new TimerCB[max_timers];
      for (int i = 0; i < max_timers; i++) {
        timer_cbs[i] = new TimerCB();
      }
    }

    override public ForwardingDecision process_packet (int in_port, ref Packet packet)
    {
      if (running && packet is EthernetPacket &&
        packet.Encapsulates(typeof(IPv4Packet), typeof(TcpPacket)))
      {
        EthernetPacket eth_p = (EthernetPacket)packet;
        IpPacket ip_p = ((IpPacket)(packet.PayloadPacket));
        // FIXME check version and checksum of eth_p and ip_p, as well as size
        //       (declared in IP header).

        if (ip_p.DestinationAddress.Equals(ip_address)) {
          // NOTE we silently drop the segment if the queue's full.
          if (in_q.Count <= max_InQ_size) {
            int tcb_i = TCB.lookup (tcbs, packet);
            TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));

            if (tcb_i >= 0) {
              // Quickly check if this is a reset, in which case we release
              // resources related to the connection.
              if (tcp_p.Rst) {
                tcbs[tcb_i].free();
                return ForwardingDecision.Drop.Instance;
              }
            } else {
              // This must be a new connection since there's no TCB for it.
              if (tcp_p.Syn) {
                tcb_i = TCB.find_free_TCB(tcbs);

                // We're going to discard the ethernet and ip headers, so
                // we'll copy this info to the TCB.
                tcbs[tcb_i].remote_address = ip_p.SourceAddress;
                tcbs[tcb_i].remote_port = tcp_p.SourcePort;

              } else {
                // We ignore the packet if we don't know what to do with it.
                // FIXME Could out_q a RST if no matching TCB exists (for our
                //       address), if the interface is set in "monopoly" mode.

                return ForwardingDecision.Drop.Instance;
              }
            }

            // FIXME check packet checksums before adding it to the queue.
            in_q.Enqueue(new Tuple <TcpPacket, TCB>(tcp_p, tcbs[tcb_i]));
          }
        }
      }

      return ForwardingDecision.Drop.Instance;
    }

    public Result<SockID> socket (Internet_Domain domain, Internet_Type type, Internet_Protocol prot) {
      // Add TCB if there's space.
      int free_TCB;
      free_TCB = TCB.find_free_TCB(tcbs);
      if (free_TCB < 0) {
        return new Result<SockID> (null, Error.ENOSP);
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

      if (tcbs[sid.sockid].tcp_state() != TCP_State.Closed) {
        return new Result<Unit> (Unit.Value, Error.EFAULT);//FIXME is this the right code?
      }

      tcbs[sid.sockid].local_port = address.port;
      // NOTE we don't use address.address here, maybe we should drop that value
      //      since it's redundant?

      return new Result<Unit> (Unit.Value, null);
    }

    public Result<Unit> listen (SockID sid) {
      if (tcbs[sid.sockid].tcp_state() != TCP_State.Closed ||
          tcbs[sid.sockid].local_port == 0) {
        return new Result<Unit> (Unit.Value, Error.EFAULT);//FIXME is this the right code?
      }

      tcbs[sid.sockid].state_to_listen();
      return new Result<Unit> (Unit.Value, null);
    }

    public Result<SockID> accept (SockID sid, out SockAddr_In address) {
      // FIXME this must be blocking, right?
      // FIXME shall we add a parameter over which is can be shutdown, and a
      //       function to call when the shutdown request arrives?

      address = SockAddr_In.none;

      if (tcbs[sid.sockid].tcp_state() != TCP_State.Listen) {
        return new Result<SockID> (null, Error.EFAULT);//FIXME is this the right code?
      }

      int free_TCB;
      TCB tcb;
      while (true) {
        if (conn_q.TryDequeue (out tcb)) {
          lock (this) {
            free_TCB = TCB.find_free_TCB(tcbs);
            if (free_TCB < 0) {
              return new Result<SockID> (null, Error.ENOSP);
            }

            tcbs[free_TCB] = tcb;
          }

          address = new SockAddr_In (tcb.remote_port, tcb.remote_address);
          return new Result<SockID> (new SockID((uint)free_TCB), null);
        }
      }
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
/*
segmentation:
then put into packets
and put them in the send window
put entire send window in out_q
when get ACKs, slide the window
*/
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
      // There's no time to close connections nicely, so we send a RST to
      // all open connections
      for (int i = 0; i < max_conn; i++) {
        lock (tcbs[i]) {
          if (tcbs[i].tcp_state() != TCP_State.Free) {
            // FIXME send RST

            // Set all TCBs to Free (even those in Listen state), and release
            // all resources e.g., those held by timers.
            tcbs[i].free();
          }
        }
      }

      Console.WriteLine ("TCPuny stopped");
    }

    public void Start () {
      Console.WriteLine ("TCPuny starting");
      running = true;
      Thread t = new Thread (new ThreadStart (this.DispatchOutputSegments));
      t.Start();
      t = new Thread (new ThreadStart (this.HandleTimerEvents));
      t.Start();

      Tuple<TcpPacket,TCB> p;
      while (running) {
        while (in_q.TryDequeue (out p)) {
          TcpPacket segment = p.Item1;
          TCB tcb = p.Item2;

          Debug.Assert(tcb.tcp_state() != TCP_State.Free);

          switch (tcb.tcp_state()) {
            case TCP_State.Closed:
            // FIXME think about using conn_q for backlog.
            //       i.e., is it too early to have assigned a TCB?
//if in listening mode, make a tcb and added it to the conn_q
//depending on stuff, perhaps send a rst (on out_q)
              throw new Exception("TODO: Closed");
              break;

            case TCP_State.Listen:
              throw new Exception("TODO: Listen");
              break;

            case TCP_State.SynSent:
              throw new Exception("TODO: SynSent");
              break;

            case TCP_State.SynRcvd:
              throw new Exception("TODO: SynRcvd");
              break;

            case TCP_State.Established:
              throw new Exception("TODO: Established");
/*
slot the segment in the receive window
  (and send ACK. as improvement could delay the ACKs)
put payload in the receive buffer
  if the segment is simply an ACK then nothing gets added to the receive buffer,
  but the TCB might be updated (if ACK isn't dup for example)
*/
              break;

            case TCP_State.FinWait1:
              throw new Exception("TODO: FinWait1");
              break;

            case TCP_State.FinWait2:
              throw new Exception("TODO: FinWait2");
              break;

            case TCP_State.CloseWait:
              throw new Exception("TODO: CloseWait");
              break;

            case TCP_State.LastAck:
              throw new Exception("TODO: LastAck");
              break;

            case TCP_State.Closing:
              throw new Exception("TODO: Closing");
              break;

            case TCP_State.TimeWait:
              throw new Exception("TODO: TimeWait");
              break;

            default:
              throw new Exception("Impossible");
          }
        }
      }
    }

    public void DispatchOutputSegments () {
      Tuple<Packet,TCB> p;
      while (running) {
        while (running && out_q.TryDequeue (out p)) {
          // FIXME start retransmission timer if we're sending a
          //       payload-carrying segment -- unless RST is set!
          device.SendPacket(p.Item1);
        }
      }
    }

    public void HandleTimerEvents () {
      Tuple<Packet, TCB, TimerCB> p;
      while (running) {
        while (running && timer_q.TryDequeue (out p)) {
          Packet packet = p.Item1;
          TCB tcb = p.Item2;
          TimerCB timer = p.Item3;

          // Action involve enqueuing a command on timer_q, that will be
          // executed by a separate thread.
          switch (timer.get_action()) {
            case Action.Retransmit:
              // FIXME increment retransmission count in TCB.

              // A new timer will be started upon retransmission.
              out_q.Enqueue(new Tuple <Packet, TCB>(packet, tcb));
              break;

            case Action.FreeTCB:
              tcb.free();
              break;

            default:
              throw new Exception("Impossible");
          }

          timer.free();
        }
      }
    }
  }
}
