/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

// FIXME use javadoc-style comments to describe the API
namespace Pax {

  // Packet will be retreivable from here, where it will
  // be deposited by another party (which depends on where
  // the packet processor is executing).
  public static class Packet_Buffer {
    public static byte[] packet = new byte[PaxConfig_Lite.MAX_PACKET_SIZE];
    public static long forward = 0; // FIXME might not be right place for this
  }

  // Packet processors.
  public interface IBytePacket_Processor {
    // FIXME using "long" type is too arbitrary?
    void process_packet (byte in_port);
  }
}
