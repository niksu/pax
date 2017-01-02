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
    PhysicalAddress my_mac_address;
    PhysicalAddress gateway_mac_address;
    bool running = false; // FIXME having to check this slows things down much?

    uint next_sock_id = 0;

    uint max_InQ_size;
    uint max_timers;
    uint max_tcb_timers;
    bool monopoly = false;
    UInt16 max_window_size;
    uint max_segment_size;

    // By design we minimise the scope of logic as much as possible, trying to
    // limit it to making small changes to data, and moving information between
    // queues between such specialised data processors.
    ConcurrentQueue<Tuple<Packet, TCB>> in_q = new ConcurrentQueue<Tuple<Packet, TCB>>();
    ConcurrentQueue<Tuple<Packet, TCB>> out_q = new ConcurrentQueue<Tuple<Packet, TCB>>();
    ConcurrentQueue<Tuple<Packet, TCB, TimerCB>> timer_q = new ConcurrentQueue<Tuple<Packet, TCB, TimerCB>>();

    TCB[] tcbs;
    TimerCB[] timer_cbs;

    // FIXME need to work through logic of how TCPuny is started, as well as how
    //       the user application initialises it.
    public TCPuny (uint max_conn, uint max_backlog,
        // FIXME do we really want ip_address and mac_address given at this
        //       stage? i'd expect ip_address to be given at "bind" step,
        //       and mac_address from the config.
        IPAddress ip_address, PhysicalAddress my_mac_address, PhysicalAddress
        gateway_mac_address, uint receive_buffer_size, uint send_buffer_size,
        uint max_InQ_size, uint max_timers, uint max_tcb_timers, bool monopoly,
        UInt16 max_window_size, uint max_segment_size) {

      // Check for sensible configuration values.
      if (max_window_size > receive_buffer_size) {
        throw new Exception("Does not make sense: max_window_size > receive_buffer_size");
      }
      if (max_segment_size > max_window_size) {
        throw new Exception("Does not make sense: max_window_size > receive_buffer_size");
      }
      if (max_segment_size > receive_buffer_size) {
        throw new Exception("Does not make sense: max_segment_size > receive_buffer_size");
      }

      this.max_conn = max_conn;
      this.max_backlog = max_backlog;
      // We get our addresses via the constructor.
      // NOTE we learn about "device" via "PreStart".
      this.ip_address = ip_address;
      this.my_mac_address = my_mac_address;
      this.gateway_mac_address = gateway_mac_address;
      this.max_InQ_size = max_InQ_size;
      this.max_timers = max_timers;
      this.max_tcb_timers = max_tcb_timers;
      this.monopoly = monopoly;
      this.max_window_size = max_window_size;
      this.max_segment_size = max_segment_size;

      TCB.local_address = ip_address;
      TimerCB.timer_q = this.timer_q;

      tcbs = new TCB[max_conn];
      for (int i = 0; i < max_conn; i++) {
        tcbs[i] = new TCB((uint)i, receive_buffer_size, send_buffer_size,
            max_tcb_timers, max_segment_size);
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
            int tcb_i = -1; // FIXME const

            lock (tcbs) { // FIXME coarse-grained?
              tcb_i = TCB.lookup (tcbs, packet);
            }

            TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));

            if (tcb_i >= 0) {
              // Quickly check if this is a reset, in which case we release
              // resources related to the connection.
              if (tcp_p.Rst) {
                tcbs[tcb_i].free();
                return ForwardingDecision.Drop.Instance;
              }
            } else {
              // We ignore the packet if we don't know what to do with it.

              if (monopoly) {
                // If the interface is set in "monopoly" mode then we out_q a
                // RST since no matching TCB exists (for our address).
                send_RST(tcp_p.DestinationPort, tcp_p.SourcePort, ip_p.SourceAddress,
                    tcp_p.AcknowledgmentNumber/*FIXME is this the right value?*/,
                    0, false);
              }

              return ForwardingDecision.Drop.Instance;
            }

#if DEBUG
            Console.Write(">");
#endif

            // FIXME check packet checksums before adding it to the queue.
            in_q.Enqueue(new Tuple <Packet, TCB>(packet, tcbs[tcb_i]));
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
        if (tcbs[sid.sockid].conn_q.TryDequeue (out tcb)) {
          address = new SockAddr_In (tcb.remote_port, tcb.remote_address);
          return new Result<SockID> (new SockID(tcb.index), null);
        }
      }
    }

    public Result<int> write (SockID sid, byte[] buf, uint count) {
      Debug.Assert(count > 0);
      Debug.Assert(count <= buf.Length);

      if ((tcbs[sid.sockid].tcp_state() != TCP_State.Established) &&
          (tcbs[sid.sockid].tcp_state() != TCP_State.CloseWait)) {
        // We shouldn't write if the other side won't read.
        return new Result<int> (-1, Error.EFAULT);//FIXME is this the right code?
      }

      // Look up the TCB
      // Check whether we're blocking or non-blocking
      // Check how many bytes we can fit into the send buffer, commit to it.
      // Insert that many bytes (updating the index), copying the bytes from 'buf'
      // Return the number of bytes we've inserted.
      // (Block if necessary, otherwise return 0)

      // FIXME blocking mode is assumed, and non-blocking mode isn't supported yet.
      // FIXME use heuristics to avoid sending small packets?

      // NOTE retransmission timer isn't started immediately, rather it's
      //      started at the time of transmission.

      if (count > this.max_segment_size) {
        // FIXME currently i assume that the program's send requests fit within
        //       a single segment, and don't currently implement segmentation.
        throw new Exception("TODO: break up into segments");
      }

/*
segmentation:
then put into packets
and put them in the send window
put entire send window in out_q
when get ACKs, slide the window
*/

#if DEBUG
      Console.Write("Writing |");
      for (int i = 0; i < count; i++) {
        Console.Write(buf[i] + ",");
      }
      Console.WriteLine("|");
#endif

      // FIXME use send_buffer, rather than generating and queueing packets
      //       directly, to make sure we don't queue too many packets (and
      //       respect window size).

      // FIXME use static allocation, rather than creating new ones.
      byte[] payload = new byte[count];
      // FIXME avoid copying
      Array.Copy(buf, 0, payload, 0, count);

      send_payload_ACK(tcbs[sid.sockid].local_port, tcbs[sid.sockid].remote_port,
          tcbs[sid.sockid].remote_address,
          tcbs[sid.sockid].next_send,
          tcbs[sid.sockid].next_receive,
          tcbs[sid.sockid].receive_window_size,
          payload);

      tcbs[sid.sockid].next_send += count;

      return new Result<int> ((int)count, null);
    }

    public Result<int> read (SockID sid, byte[] buf, uint count) {
      Debug.Assert(count > 0);

      // FIXME should return whatever's in the buffer before checking connection state?
      if ((tcbs[sid.sockid].tcp_state() != TCP_State.Established) &&
          (tcbs[sid.sockid].tcp_state() != TCP_State.FinWait1) &&
          (tcbs[sid.sockid].tcp_state() != TCP_State.FinWait2) &&
          (tcbs[sid.sockid].tcp_state() != TCP_State.CloseWait)) {
        // We should continue reading until the very end.
        return new Result<int> (-1, Error.EFAULT);//FIXME is this the right code?
      }

      // Look up the TCB
      // Check whether we're blocking or non-blocking
      // Check how many bytes we can send, commit to it.
      // Extract that many bytes (updating the index), copying the bytes to 'buf'
      // Return the number of bytes we've extracted.
      // (Block if necessary, otherwise return 0)

      // FIXME check whether we should be non-blocking.
      int result = tcbs[sid.sockid].blocking_read(buf, count);
      Debug.Assert(result >= 0);
      return new Result<int> (result, null);
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
          if ((tcbs[i].tcp_state() != TCP_State.Free) &&
              (tcbs[i].tcp_state() != TCP_State.Listen)) {
            send_RST(tcbs[i].local_port, tcbs[i].remote_port, tcbs[i].remote_address,
                tcbs[i].next_send++,
                tcbs[i].next_receive, true);

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

      Tuple<Packet,TCB> p;
      while (running) {
        while (in_q.TryDequeue (out p)) {
          Packet packet = p.Item1;
          TCB tcb = p.Item2;

          EthernetPacket eth_p = (EthernetPacket)packet;
          IpPacket ip_p = ((IpPacket)(packet.PayloadPacket));
          TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));

          Debug.Assert(tcb.tcp_state() != TCP_State.Free);

          switch (tcb.tcp_state()) {
            case TCP_State.Closed:
              throw new Exception("Impossible");
              break;

            case TCP_State.Listen:
              if (tcp_p.Syn && !tcp_p.Ack) {
                Debug.Assert(tcb.tcp_state() == TCP_State.Listen);

                int tcb_i = TCB.lookup (tcbs, packet, true);
                if (tcb_i >= 0) {
                  // FIXME not efficient approach!
                  // If a TCB already exists then we put this back on the queue
                  // and have it reference the TCB we found.
                  in_q.Enqueue(new Tuple <Packet, TCB>(packet, tcbs[tcb_i]));
                  break;
                }

                tcb_i = TCB.find_free_TCB(tcbs);

                // FIXME discard the ethernet and ip headers since we copy this info to the TCB?
                tcbs[tcb_i].remote_address = ip_p.SourceAddress;
                tcbs[tcb_i].remote_port = tcp_p.SourcePort;

                tcbs[tcb_i].parent_tcb = tcb;
                tcbs[tcb_i].local_port = tcb.local_port;

                // FIXME start a timer (so-called connection-establishment
                //       timer) to remove this record if we don't hear anything
                //       more from the other peer.
                tcbs[tcb_i].state_to_synrcvd();

                tcbs[tcb_i].seq_of_most_recent_window = tcp_p.SequenceNumber;
                tcbs[tcb_i].initialise_receive_sequence(tcp_p.SequenceNumber);
                tcbs[tcb_i].next_receive = tcp_p.SequenceNumber + 1;
                tcbs[tcb_i].send_window_size = tcp_p.WindowSize;
                tcbs[tcb_i].receive_window_size = max_window_size; // FIXME might want to start with smaller window.

                tcbs[tcb_i].next_send = tcbs[tcb_i].initial_send_sequence;

                send_SYNACK(tcbs[tcb_i].local_port, tcbs[tcb_i].remote_port,
                    tcbs[tcb_i].remote_address,
                    tcbs[tcb_i].next_send,
                    tcbs[tcb_i].next_receive,
                    tcbs[tcb_i].receive_window_size);

                // We increment the sequence number, having sent a SYN.
                tcbs[tcb_i].next_send++;
#if DEBUG
                Console.WriteLine("Initiating connection, TCB " + tcb_i + " from " + tcb.index);
#endif
              } else {
                // We don't check If the interface is set in "monopoly" mode to
                // send a RST since this TCP instance is listening on this port,
                // so we expect to at least have control over this port (but not
                // over the whole interface -- i.e., we leave other ports
                // alone).
                send_RST(tcp_p.DestinationPort, tcp_p.SourcePort, ip_p.SourceAddress,
                    tcp_p.AcknowledgmentNumber,
                    tcp_p.SequenceNumber + 1, true);
                tcb.free();

                // I think we can be this aggressive (sending RST), since it
                // can't be that segments got reordered such that data segments
                // arrived before the completion of the handshake, since those
                // segments wouldn't know which ack numbers to carry.
              }

              break;

            case TCP_State.SynRcvd:
#if DEBUG
              Console.WriteLine("SynRcvd: TCB " + tcb.index);
#endif

              if (tcp_p.Rst) {
                Debug.Assert(tcb != null);
                tcb.free();
              } else if (tcp_p.Fin) {
                // Kill the connection -- illegal transition.
                send_RST(tcp_p.DestinationPort, tcp_p.SourcePort, ip_p.SourceAddress,
                    tcp_p.AcknowledgmentNumber/*FIXME is this the right value?*/,
                    0, false);
                tcb.free();
              } else if (tcp_p.Syn) {
                // FIXME handle SYNACK retransmission if we get a SYN in this state.
                //       reset SYNACK timer.
                throw new Exception("Retransmitted SYN?");
              } else if (tcp_p.Ack) {
#if DEBUG
                Console.WriteLine("SynRcvd: ACK for TCB " + tcb.index);
#endif
                if (!tcb.is_in_receive_window(tcp_p)) {
                  // Ignore segments that fall outside the receive window.
#if DEBUG
                  Console.WriteLine("Received segment was outside the window");
#endif
                  break;
                }

                // Update the receive window.
                tcb.buffer_in_receive_window(tcp_p);
                if (tcb.advance_receive_window() > 0) {
                  // Send ACK for any payload we've jut received.
                  send_ACK(tcb.local_port, tcb.remote_port,
                      tcb.remote_address,
                      tcb.next_send,
                      tcb.next_receive,
                      tcb.receive_window_size);
                }

#if DEBUG
                Console.WriteLine("SynRcvd -> Established");
#endif
                // Update the connection metadata.
                tcb.send_window_size = tcp_p.WindowSize;
                tcb.seq_of_most_recent_window = tcp_p.SequenceNumber;

                tcb.state_to_established();

                // Communicate to "accept" that it can proceed.
                tcb.parent_tcb.conn_q.Enqueue(tcb);
              }

              break;

            case TCP_State.Established:
#if DEBUG
              Console.WriteLine("Established: segment for TCB " + tcb.index);
#endif
              if (!tcb.is_in_receive_window(tcp_p)) {
                // Ignore segments that fall outside the receive window.
#if DEBUG
                Console.WriteLine("Received segment was outside the window");
#endif
                break;
              }

              // Update the receive window.
              tcb.buffer_in_receive_window(tcp_p);
              if (tcb.advance_receive_window() > 0) {
                // Send ACK for any payload we've jut received.
                send_ACK(tcb.local_port, tcb.remote_port,
                    tcb.remote_address,
                    tcb.next_send,
                    tcb.next_receive,
                    tcb.receive_window_size);
              }

              // Update the connection metadata.
              tcb.send_window_size = tcp_p.WindowSize; // FIXME decrement this according to how much of the window is still available?
              tcb.seq_of_most_recent_window = tcp_p.SequenceNumber;
              tcb.ack_of_most_recent_window = tcp_p.AcknowledgmentNumber;
              // tcb.next_receive is set by advance_receive_window()
              // FIXME if ACK is set (and it should be) then update our send-related metadata, to advance the send window if bytes have been ACKd.

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
              if (tcp_p.Rst) {
                tcb.free();
              } else if (tcp_p.Syn) {
                send_RST(tcp_p.DestinationPort, tcp_p.SourcePort, ip_p.SourceAddress,
                    tcp_p.AcknowledgmentNumber/*FIXME is this the right value?*/,
                    0, false);
                tcb.free();
              } else {
                // FIXME ack any data received?
                // FIXME check that FIN has been ACK'd, and if so then
                //       call tcb.state_to_timewait();
              }
              break;

            case TCP_State.TimeWait:
              if (tcp_p.Rst) {
                tcb.free();
              } else if (tcp_p.Syn) {
                send_RST(tcp_p.DestinationPort, tcp_p.SourcePort, ip_p.SourceAddress,
                    tcp_p.AcknowledgmentNumber/*FIXME is this the right value?*/,
                    0, false);
                tcb.free();
              } else {
                // FIXME resend acknowledgement?
                // FIXME reschedule 2MSL deletion of the TCB
              }
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
          EthernetPacket eth_p = (EthernetPacket)p.Item1;
          IpPacket ip_p = ((IpPacket)(p.Item1.PayloadPacket));
          TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));

          if (!tcp_p.Rst && tcp_p.PayloadData != null && tcp_p.PayloadData.Length > 0) {
            // FIXME start retransmission timer if we're not sending an empty
            //       payload that isn't flagged as RST.
          }

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

    // FIXME should have memory pre-allocated for packet generation.
    private Packet raw_packet(ushort src_port, ushort dst_port, IPAddress dst_ip,
        UInt16 receive_window_size) {
      var tcp_p = new TcpPacket(src_port, dst_port);
      var ip_p = new IPv4Packet(ip_address, dst_ip);
      var eth_p = new EthernetPacket(my_mac_address, gateway_mac_address, EthernetPacketType.None);
      eth_p.PayloadPacket = ip_p;
      ip_p.PayloadPacket = tcp_p;

      tcp_p.WindowSize = receive_window_size;

      return eth_p;
    }

    private void send_packet(Packet packet) {
      EthernetPacket eth_p = (EthernetPacket)packet;
      IPv4Packet ip_p = ((IPv4Packet)(packet.PayloadPacket));
      TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));

      tcp_p.UpdateCalculatedValues();
      ip_p.UpdateCalculatedValues();
      eth_p.UpdateCalculatedValues();

      tcp_p.UpdateTCPChecksum();
      ip_p.UpdateIPChecksum();
      eth_p.UpdateCalculatedValues();

      out_q.Enqueue(new Tuple <Packet, TCB>(eth_p, null));
    }

    private void send_RST(ushort src_port, ushort dst_port, IPAddress dst_ip, uint seq_no, uint ack_no, bool set_ack) {
      // FIXME is there a nice way of unpacking packets?
      Packet packet = raw_packet(src_port, dst_port, dst_ip, 0);
      EthernetPacket eth_p = (EthernetPacket)packet;
      IpPacket ip_p = ((IpPacket)(packet.PayloadPacket));
      TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));

      tcp_p.Rst = true;
      tcp_p.SequenceNumber = seq_no;
      tcp_p.AcknowledgmentNumber = ack_no;
      tcp_p.Ack = set_ack;

      send_packet(packet);
    }

    private void send_SYNACK(ushort src_port, ushort dst_port, IPAddress dst_ip,
        uint seq_no, uint ack_no, UInt16 receive_window_size) {
      Packet packet = raw_packet(src_port, dst_port, dst_ip, receive_window_size);
      EthernetPacket eth_p = (EthernetPacket)packet;
      IpPacket ip_p = ((IpPacket)(packet.PayloadPacket));
      TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));

      tcp_p.SequenceNumber = seq_no;
      tcp_p.Syn = true;
      tcp_p.AcknowledgmentNumber = ack_no;
      tcp_p.Ack = true;

      send_packet(packet);
    }

    private void send_ACK(ushort src_port, ushort dst_port, IPAddress dst_ip,
        uint seq_no, uint ack_no, UInt16 receive_window_size) {
      Packet packet = raw_packet(src_port, dst_port, dst_ip, receive_window_size);
      EthernetPacket eth_p = (EthernetPacket)packet;
      IpPacket ip_p = ((IpPacket)(packet.PayloadPacket));
      TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));

      tcp_p.SequenceNumber = seq_no;
      tcp_p.AcknowledgmentNumber = ack_no;
      tcp_p.Ack = true;

      send_packet(packet);
    }

    // FIXME looks like i can factor these 'send_' functions?
    private void send_payload_ACK(ushort src_port, ushort dst_port, IPAddress dst_ip,
        uint seq_no, uint ack_no, UInt16 receive_window_size, byte[] payload) {
      Packet packet = raw_packet(src_port, dst_port, dst_ip, receive_window_size);
      EthernetPacket eth_p = (EthernetPacket)packet;
      IpPacket ip_p = ((IpPacket)(packet.PayloadPacket));
      TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));

      tcp_p.SequenceNumber = seq_no;
      tcp_p.AcknowledgmentNumber = ack_no;
      tcp_p.Ack = true;

      if (payload != null) {
        tcp_p.PayloadData = payload;
      }

      send_packet(packet);
    }
  }
}
