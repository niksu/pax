/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using Pax;

#if !LITE
using PacketDotNet;
#endif


public
#if LITE
static
#endif
class Hub
#if !LITE
 : MultiInterface_SimplePacketProcessor
#endif
 {
#if !LITE
  override public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
/* NOTE disabled this since this processor doesn't look at packets, only at the in_port.
    // Extract the bytes and call the instance of IAbstract_ByteProcessor
    byte[] bs = packet.Bytes;
*/
/* FIXME ideally we should be able use a single implementation of the logic.
         This is possible, but requires some in-progress development.
         Specifically this conists of a phase that switches between
         the high-level and lower-level encoding of in_port, packet, and forward.
         Then we get two runtime options:
         * the Pax instance can use the Pax and Pax-Lite level logic (the latter
           by using the en/decoding)
         * the Pax_Lite instance can only use the low-level encoding since the
           types for the high-level encoding are not in its universe.

    process_packet (in_port);
    TODO var forward = decode_forward();
    return forward;
*/
    return (ForwardingDecision.broadcast(in_port));
  }
#endif

  public
#if LITE
  static
#endif
  void process_packet (byte in_port)
  {
    long forward = Packet_Buffer.forward;

    for (int ofs = 0; ofs < PaxConfig_Lite.no_interfaces; ofs++)
    {
      forward = forward << 1;
      if (ofs != in_port)
      {
        forward |= 1;
      }
    }

    Packet_Buffer.forward = forward;
    //Packet_Buffer.forward = 0xFAFF; // FIXME currently using a constant for debugging
  }
}
