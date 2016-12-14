/*
TCP connection state information.
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net;
using System.Diagnostics;
//using SharpPcap;
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

    public TCP_State state = TCP_State.Free;
    public IPAddress remote_address = null;
    public ushort remote_port = 0;
    public ushort local_port = 0;
    public tcpseq unacked_send = 0; // FIXME add NaN value for some fields?
    public tcpseq next_send = 0;
    public ulong send_window_size = 0; // FIXME is this a sensible value?

    public uint retransmit_count;

    public uint sending_max_seg_size = 0; // FIXME is this a sensible value?

    // FIXME consts -- make buffer sizes into parameters.
    public Packet[] receive_buffer = new Packet[100];
    public Packet[] send_buffer = new Packet[100];

//    seq
//    ack
//    window
//    timer

    // FIXME add nullary constructor that initialises TCB.
    public TCB() {
      Debug.Assert(TCB.local_address != null);
    }

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
        if (tcbs[i].state == TCP_State.Free) {
          return i;
        }
      }

      return -1;
    }
  }
}
