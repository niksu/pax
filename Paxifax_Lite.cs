/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

// FIXME use javadoc-style comments to describe the API
namespace Pax {

  // Packet processors.
  public interface IAbstract_ByteProcessor {
    // FIXME using "long" type is too arbitrary?
    // FIXME should in_port be "uint"?
    long process_packet (int in_port, ref byte[] packet);
  }
}
