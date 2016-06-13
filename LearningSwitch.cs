/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using PacketDotNet;
using System.Diagnostics;
using Pax;

public class LearningSwitch : MultiInterface_SimplePacketProcessor {
  // FIXME synchronise on this!
  Dictionary<PhysicalAddress,int> forwarding_table = new Dictionary<PhysicalAddress,int>();

  override public int[] handler (int in_port, ref Packet packet)
  {
    int[] out_ports;

    if (packet is PacketDotNet.EthernetPacket)
    {
      EthernetPacket eth = ((PacketDotNet.EthernetPacket)packet);

      // Forwarding decision.
      if (forwarding_table.ContainsKey(eth.DestinationHwAddress))
      {
        int out_port = forwarding_table[eth.DestinationHwAddress];

        if (out_port == in_port)
        {
          var tmp = Console.ForegroundColor;
          Console.ForegroundColor = ConsoleColor.Red;
          Console.WriteLine("Interface {0} was about to emit a frame it has just received.", PaxConfig.deviceMap[in_port].Name);
          Console.ForegroundColor = tmp;
          out_ports = new int[0];
        } else {
          out_ports = new int[1]{out_port};
        }
      } else {
        out_ports = MultiInterface_SimplePacketProcessor.broadcast(in_port);
      }

      // Switch learns which port knows about the SourceHwAddress.
      if (forwarding_table.ContainsKey(eth.SourceHwAddress))
      {
        if (forwarding_table[eth.SourceHwAddress] != in_port)
        {
          forwarding_table[eth.SourceHwAddress] = in_port;
#if DEBUG
          Debug.WriteLine("Relearned " + eth.SourceHwAddress.ToString() + " <- " + PaxConfig.deviceMap[in_port].Name);
#endif
        }
      } else {
        forwarding_table[eth.SourceHwAddress] = in_port;
#if DEBUG
        Debug.WriteLine("Learned " + eth.SourceHwAddress.ToString() + " <- " + PaxConfig.deviceMap[in_port].Name);
#endif
      }

      // FIXME We don't have a "forgetting policy". We could periodically empty our forwarding_table.
    } else {
      // Drop if the packet's not an Ethernet frame.
      out_ports = new int[0];
    }

    return out_ports;
  }
}

