/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

// FIXME use javadoc-style comments to describe the API
namespace Pax {

  // Packet processors.
  public abstract class BytePacket_Processor {
    byte[] packet = new byte[PaxConfig_Lite.MAX_PACKET_SIZE];

    // FIXME using "long" type is too arbitrary?
    abstract public long process_packet (byte in_port);
  }
}
