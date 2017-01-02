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
    // Read and write pointers for the circular receive_buffer.
    private long rb_read_ptr;
    private long rb_write_ptr;

    // For TCBs derived from Listen TCBs, for the former to point to the latter.
    // This allows us to keep track of the backlog of connections.
    // FIXME check if that's the correct rationale above -- should abandon idea of using conn_q in TCP.cs?
    public TCB parent_tcb;
    public ConcurrentQueue<TCB> conn_q = new ConcurrentQueue<TCB>(); // FIXME bound the size of this queue.

    // FIXME is this value incremented only, or also decayed in time?
    public uint retransmit_count = 0;

    public TimerCB[] timers; // FIXME maximum number of allocated timers per TCB -- add as configuration parameter, and allocate this array at TCB initialisation.

    private void initialise_segment_sequence() {
      // FIXME randomize;
      this.initial_send_sequence = 0;
    }

    // Checks if a segment falls in our receive window.
    public bool is_in_receive_window(TcpPacket tcp_p) {
      // See https://en.wikipedia.org/wiki/Serial_number_arithmetic
      int distance = (int)(tcp_p.SequenceNumber - next_receive);
      bool outcome = false;

#if DEBUG
      Console.WriteLine("is_in_receive_window distance = " + distance);
#endif

      if (distance == 0) {
        outcome = true;
      } else if (distance < 0) {
        outcome = false;
      } else
        // It must be that "distance > 0".
        if (distance < receive_buffer.Length &&
            // The previous line checks that the start of the segment is within the receive window.
            // The next line checks that the end of the segment is within the receive window.
            distance + tcp_p.PayloadData.Length < receive_buffer.Length) {
          outcome = true;
      } else {
        // The segment is too far ahead, outside the receive window.
        outcome = false;
      }

      return outcome;
    }

    // Segment payload is added to receive buffer.
    // NOTE we assume that is_in_receive_window has return 'true' for this
    //      segment.
    public void buffer_in_receive_window(TcpPacket tcp_p) {
      // The segment size cannot exceed the size of the receive buffer. (We'll
      // check how much of that buffer is available next, for storing the
      // segment's payload.)
      Debug.Assert(tcp_p.PayloadData.Length < receive_buffer.Length);

     // FIXME check that we're not overwriting bytes that are between the rb_read_ptr and the rb_write_ptr,
     //       since those should already have been ACKd.

     long start_idx = tcp_p.SequenceNumber % receive_buffer.Length;
     long end_idx = (tcp_p.SequenceNumber + tcp_p.PayloadData.Length) % receive_buffer.Length;

#if DEBUG
     Console.WriteLine("start_idx = " + start_idx);
     Console.WriteLine("end_idx = " + end_idx);
     Console.WriteLine("rb_read_ptr = " + rb_read_ptr);
     Console.WriteLine("rb_write_ptr = " + rb_write_ptr);
#endif

#if DEBUG
      Console.Write("Buffering |");
      for (int i = 0; i < tcp_p.PayloadData.Length; i++) {
        Console.Write(tcp_p.PayloadData[i] + ",");
      }
      Console.WriteLine("|");
#endif

      if (start_idx > end_idx)
      {
        // We wrap, so break the copy into two.
        long segment1_length = receive_buffer.Length - start_idx;
        long segment2_length = tcp_p.PayloadData.Length - segment1_length;
        Debug.Assert(segment1_length + segment2_length == tcp_p.PayloadData.Length);
        Debug.Assert(segment2_length == end_idx);
        Array.Copy (tcp_p.PayloadData, 0, receive_buffer, start_idx, segment1_length);
        Array.Copy (tcp_p.PayloadData, segment1_length, receive_buffer, 0, segment2_length);
      } else {
        // We don't wrap, so the payload's bytes are contiguous in the receive buffer.
        Array.Copy (tcp_p.PayloadData, 0, receive_buffer,
         start_idx, tcp_p.PayloadData.Length);
      }
    }

    // Advances the receive window if possible, and as much as possible.
    // NOTE we assume that buffer_in_receive_window has been called for newly
    //      received segments.
    // NOTE advance_receive_window changes rb_write_ptr, and checks against rb_read_ptr,
    //      whereas the 'read' function in TCP changes rb_read_ptr, and checks against rb_write_ptr.
    public uint advance_receive_window() {
      uint advance = 0; // how much have we advanced, this is returned to the caller.
      // FIXME little protection against wrapping.

      while (receive_buffer[rb_write_ptr] != null) {
        // Continuous non-null bytes in the receive_buffer are made readable to
        // the client.

        if (rb_read_ptr == (rb_write_ptr + 1) % receive_buffer.Length) {
          // The receive buffer is full -- we must wait for the application to
          // read before advancing the window.
          break;
        }

        // FIXME locking?

        rb_write_ptr = (rb_write_ptr + 1) % receive_buffer.Length;
        next_receive++;
        advance++;
      }

#if DEBUG
      Console.WriteLine("Received " + advance + " bytes payload");
#endif
      return advance;
    }

    // Returns the number of bytes read.
    public int blocking_read (byte[] buf, uint count) {
      // FIXME instead of looping could perform some sort of wait for an event
      //       indicating that the buffer's got something for us.
      while (rb_read_ptr == rb_write_ptr) {}

      int idx = 0;

      // FIXME locking?
      while (rb_read_ptr != rb_write_ptr && idx < count) {
        Debug.Assert(receive_buffer[rb_read_ptr] != null);

        buf[idx] = receive_buffer[rb_read_ptr].Value;
        receive_buffer[rb_read_ptr] = null; // Using null to indicate that the slot's available.

        idx++;
        rb_read_ptr = (rb_read_ptr + 1) % receive_buffer.Length;
      }

#if DEBUG
      Console.WriteLine("blocking_read=" + idx);
#endif

      return idx;
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

      initialise_segment_sequence();

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
    }

    public void initialise_receive_sequence(tcpseq initial_receive_sequence) {
      this.initial_receive_sequence = initial_receive_sequence;

      // We add one to the sequence number since SYN increments the sequence number but doesn't actually communicate a byte of data in the payload.
      this.rb_write_ptr = (1 + initial_receive_sequence) % receive_buffer.Length;
      this.rb_read_ptr = this.rb_write_ptr;
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
