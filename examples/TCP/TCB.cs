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
  public struct TCB {
    public TCP_State state;
    public IPAddress remote_address;
    public short remote_port;
    public short local_port;
    public tcpseq unacked_send;
    public tcpseq next_send;
    public ulong send_window_size;

    public uint retransmit_count;

    public uint sending_max_seg_size;

    public Packet[] receive_buffer;
    public Packet[] send_buffer;

//    seq
//    ack
//    window
//    timer
  }
}
