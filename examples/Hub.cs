/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using Pax;

#if !LITE
using PacketDotNet;

public class Hub_PP : MultiInterface_SimplePacketProcessor {
  override public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    // Extract the bytes and call the instance of IAbstract_ByteProcessor
    byte[] bs = packet.Bytes;
// FIXME    return (process_packet (in_port, ref bs));
    return (ForwardingDecision.broadcast(in_port));
    // FIXME simple call Hub.process_packet?
  }
}
#endif

public static class Hub {
  static public void process_packet (byte in_port)
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
  }
}
