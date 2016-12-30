/*
TCP connection state information.
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net;
using System.Diagnostics;
using PacketDotNet;
using System.Collections.Concurrent;

using tcpseq = System.UInt32;

namespace Pax_TCP {
  // NOTE we don't include SynSent this this implementation of TCP doesn't
  //      support "active open".
  public enum TCP_State { Free, Closed, Listen, SynRcvd, Established,
    FinWait1, FinWait2, CloseWait, LastAck, Closing, TimeWait }

  // NOTE unlike "usual" TCB i don't store a reference to the network interface
  //      or the local IP address, since that info seems redundant in this
  //      implementation.
  public class TCB {
    public readonly uint index;
    public static IPAddress local_address = null;

    private TCP_State state = TCP_State.Free;
    public IPAddress remote_address = null;
    public ushort remote_port = 0;
    public ushort local_port = 0;
    private uint max_segment_size; // FIXME could split this into two, for receive and send.

    // Send sequence variables
    public tcpseq unacknowledged_send;
    public tcpseq next_send;
    public UInt16 send_window_size; // as advertised to us by peer.
    public tcpseq seq_of_most_recent_window;
    public tcpseq ack_of_most_recent_window;
    public tcpseq initial_send_sequence;
    public tcpseq max_seq_so_far_send;
    public Packet[] send_buffer; // NOTE saves effort over using a byte buffer -- we resend packets directly from the buffer.

    // Receive sequence variables
    public tcpseq next_receive;
    public UInt16 receive_window_size; // as we advertised to peer.
    public tcpseq initial_receive_sequence;
    public byte?[] receive_buffer;

    // For TCBs derived from Listen TCBs, for the former to point to the latter.
    // This allows us to keep track of the backlog of connections.
    // FIXME check if that's the correct rationale above -- should abandon idea of using conn_q in TCP.cs?
    public TCB parent_tcb;
    public ConcurrentQueue<TCB> conn_q = new ConcurrentQueue<TCB>(); // FIXME bound the size of this queue.

    // FIXME is this value incremented only, or also decayed in time?
    public uint retransmit_count = 0;

    public TimerCB[] timers; // FIXME maximum number of allocated timers per TCB -- add as configuration parameter, and allocate this array at TCB initialisation.

    public ConcurrentQueue<byte> received_data_q = new ConcurrentQueue<byte>(); // FIXME bound the size of this queue? or is the size implicit based on the receive window size?

    private void initialise_segment_sequence() {
      // FIXME randomize;
      this.initial_send_sequence = 0;
    }

    public bool is_in_receive_window(TcpPacket p) {
      // See https://en.wikipedia.org/wiki/Serial_number_arithmetic
      int distance = (int)(p.SequenceNumber - next_receive);
      bool outcome = false;

      if (distance == 0) {
        outcome = true;
      } else if (distance < 0) {
        outcome = false;
      } else
        // It must be that "distance > 0".
        if (distance < receive_buffer.Length &&
            // The previous line checks that the start of the segment is within the receive window.
            // The previous line checks that the end of the segment is within the receive window.
            distance + p.PayloadData.Length < receive_buffer.Length) {
          outcome = true;
      } else {
        // The segment is too far ahead, outside the receive window.
        outcome = false;
      }

      return outcome;
    }

    public void buffer_in_receive_window(TcpPacket p) {
      // FIXME implement ring buffer?
//      Array.Copy  
//
    }

    // Advances the receive window if possible, and as much as possible.
    public void advance_receive_window() {
      // FIXME this could use batching to improve efficiency, rather than
      //       copying one byte at a time.
      while (receive_buffer[next_receive] != null) {
        // We copy receive_buffer[next_receive] to the application's read buffer via received_data_q.
        received_data_q.Enqueue(receive_buffer[next_receive].Value);
        receive_buffer[next_receive] = null; // Using null to indicate that the slot's available.
        next_receive++;
      }
    }

    public TCP_State tcp_state() {
      return this.state;
    }

    public void state_to_established() {
      Debug.Assert(this.state == TCP_State.SynRcvd);
      this.state = TCP_State.Established;
    }

    public void state_to_timewait() {
      Debug.Assert(this.state == TCP_State.Closing);
      this.state = TCP_State.TimeWait;
    }

    public void state_to_synrcvd() {
      Debug.Assert(this.state == TCP_State.Closed);
      this.state = TCP_State.SynRcvd;
    }

    public void state_to_listen() {
      Debug.Assert(this.state == TCP_State.Closed);
      this.state = TCP_State.Listen;
    }

    public void acquire() {
      Debug.Assert(this.state == TCP_State.Free);
      this.state = TCP_State.Closed;
    }

    public void free() {
      Debug.Assert(this.state != TCP_State.Free);

      // Free up timer resources.
      // FIXME linear time, not efficient.
      for (int i = 0; i < timers.Length; i++) {
        if (timers[i] != null) {
          timers[i].free();
        }
      }

      this.state = TCP_State.Free;
    }

    public TCB(uint index, uint receive_buffer_size, uint send_buffer_size,
        uint max_tcb_timers, uint max_segment_size) {

      Debug.Assert(TCB.local_address != null);
      Debug.Assert(receive_buffer_size > 0);
      Debug.Assert(send_buffer_size > 0);
      Debug.Assert(max_tcb_timers > 0);

      this.index = index;

      receive_buffer = new byte?[receive_buffer_size];
      for (int i = 0; i < receive_buffer_size; i++) {
        receive_buffer[i] = null;
      }

      send_buffer = new Packet[send_buffer_size];
      for (int i = 0; i < send_buffer_size; i++) {
        send_buffer[i] = null;
      }

      timers = new TimerCB[max_tcb_timers];
      for (int i = 0; i < max_tcb_timers; i++) {
        timers[i] = null;
      }

      this.max_segment_size = max_segment_size;

      initialise_segment_sequence();
    }

    // Demultiplexes a TCP segment to determine the TCB.
    // Negative values indicate that the lookup failed.
    public static int lookup (TCB[] tcbs, Packet packet,
        bool expect_to_find = false) {
      int listener = -1;
      int non_listener = -1;

      EthernetPacket eth_p = (EthernetPacket)packet;
      IpPacket ip_p = ((IpPacket)(packet.PayloadPacket));
      TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));

      for (int i = 0; i < tcbs.Length; i++) {
        if (tcbs[i].state == TCP_State.Free) {
          continue;
        }

        if ((!TCB.local_address.Equals(ip_p.DestinationAddress)) ||
            tcbs[i].local_port != tcp_p.DestinationPort) {
          continue;
        }

        if (tcbs[i].state == TCP_State.Listen) {
          Debug.Assert(TCB.local_address.Equals(ip_p.DestinationAddress) &&
            tcbs[i].local_port == tcp_p.DestinationPort);

          if (listener < 0) {
            listener = i;
          } else {
            throw new Exception("Multiple listeners on same port");
          }
        } else {
          if ((!tcbs[i].remote_address.Equals(ip_p.SourceAddress)) ||
              tcbs[i].remote_port != tcp_p.SourcePort) {
            continue;
          }

          Debug.Assert(tcbs[i].remote_address.Equals(ip_p.SourceAddress) &&
              tcbs[i].remote_port == tcp_p.SourcePort);

          if (non_listener < 0) {
            non_listener = i;
          } else {
            throw new Exception("Multiple non-listeners on same port");
          }
        }
      }

      return (non_listener < 0
              ? (expect_to_find
                 ? -1
                 : listener)
              : non_listener);
    }

    public static int find_free_TCB(TCB[] tcbs) {
      lock (tcbs) { // FIXME coarse-grained?
        // FIXME linear search not efficient.
        for (int i = 0; i < tcbs.Length; i++) { // NOTE assuming that tcbs.Length == max_conn
          // FIXME a more fine-grained locking approach would involve "getting
          //       back to this later" if it's currently locked, to avoid
          //       spuriously running out of TCBs.
          lock (tcbs[i]) {
            if (tcbs[i].state == TCP_State.Free) {
              tcbs[i].acquire();
              return i;
            }
          }
        }
      }

      return -1;
    }
  }
}
