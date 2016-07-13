/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using PacketDotNet;
using Pax;

public class Hub : MultiInterface_SimplePacketProcessor {
  override public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    int[] out_ports = MultiInterface_SimplePacketProcessor.broadcast(in_port);
    return (new ForwardingDecision.MultiPortForward(out_ports));
  }
}
