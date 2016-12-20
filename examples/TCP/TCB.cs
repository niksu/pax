/*
TCP connection state information.
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net;
using System.Diagnostics;
using PacketDotNet;

using tcpseq = System.UInt32;

namespace Pax_TCP {
  public enum TCP_State { Free, Closed, Listen, SynSent, SynRcvd, Established,
    FinWait1, FinWait2, CloseWait, LastAck, Closing, TimeWait }

  // NOTE unlike "usual" TCB i don't store a reference to the network interface
  //      or the local IP address, since that info seems redundant in this
  //      implementation.
  public class TCB {
    public static IPAddress local_address = null;

    private TCP_State state = TCP_State.Free;
    public IPAddress remote_address = null;
    public ushort remote_port = 0;
    public ushort local_port = 0;
    public tcpseq unacked_send = 0; // FIXME add NaN value for some fields?
    public tcpseq next_send = 0;
    public ulong send_window_size = 0; // FIXME is this a sensible value?

    public uint retransmit_count;

    public uint sending_max_seg_size = 0; // FIXME is this a sensible value?

    public Packet[] receive_buffer;
    public Packet[] send_buffer;

    public TimerCB[] timers; // FIXME maximum number of allocated timers per TCB -- add as configuration parameter, and allocate this array at TCB initialisation.

//    seq
//    ack
//    window
//    timer

    public TCP_State tcp_state() {
      return this.state;
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

    public TCB(uint receive_buffer_size, uint send_buffer_size, uint max_tcb_timers) {
      Debug.Assert(TCB.local_address != null);
      Debug.Assert(receive_buffer_size > 0);
      Debug.Assert(send_buffer_size > 0);
      Debug.Assert(max_tcb_timers > 0);
      receive_buffer = new Packet[receive_buffer_size];
      send_buffer = new Packet[send_buffer_size];
      timers = new TimerCB[max_tcb_timers];
    }

    // Demultiplexes a TCP segment to determine the TCB.
    // Negative values indicate that the lookup failed.
    public static int lookup (TCB[] tcbs, Packet packet) {
      int listener = -1;
      int non_listener = -1;

      EthernetPacket eth_p = (EthernetPacket)packet;
      IpPacket ip_p = ((IpPacket)(packet.PayloadPacket));
      TcpPacket tcp_p = ((TcpPacket)(ip_p.PayloadPacket));

      for (int i = 0; i < tcbs.Length; i++) {
        if (tcbs[i].state == TCP_State.Free) {
          continue;
        }

        if (! TCB.local_address.Equals(ip_p.DestinationAddress) ||
            tcp_p.DestinationPort != tcbs[i].local_port) {
          continue;
        }

        if (tcbs[i].state == TCP_State.Listen) {
          if (listener < 0) {
            listener = i;
          } else {
            throw new Exception("Multiple listeners on same port");
          }
        } else {

          if (! tcbs[i].remote_address.Equals(ip_p.SourceAddress) ||
              tcp_p.SourcePort != tcbs[i].remote_port) {
            continue;
          }

          if (non_listener < 0) {
            non_listener = i;
          } else {
            throw new Exception("Multiple non-listeners on same port");
          }
        }
      }

      return (non_listener < 0 ? listener : non_listener);
    }

    public static int find_free_TCB(TCB[] tcbs) {
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

      return -1;
    }
  }
}
