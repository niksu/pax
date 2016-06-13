/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using PacketDotNet;
using Pax;

public class Hub : MultiInterface_SimplePacketProcessor {
  override public int[] handler (int in_port, ref Packet packet)
  {
    return MultiInterface_SimplePacketProcessor.broadcast(in_port);
  }
}
