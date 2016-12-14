/*
TCP connection state information.
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System.Net;
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

    public static int lookup (TCB[] tcbs, Packet p) {

      return -1;
    }
  }
}
