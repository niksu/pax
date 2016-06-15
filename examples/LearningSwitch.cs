/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net.NetworkInformation;
using System.Collections.Concurrent;
using PacketDotNet;
using System.Diagnostics;
using Pax;

public class LearningSwitch : MultiInterface_SimplePacketProcessor {
  // NOTE Perhaps ConcurrentDictionary is overkill since:
  // i) In this implementation we never "forget" learnt mappings. So there isn't
  //    a risk of having an entry deleted in between it being checked and used.
  // ii) Distinct ports A and B should never update the same mapping, unless
  //    we're in a forwarding loop.
  ConcurrentDictionary<PhysicalAddress,int> forwarding_table = new ConcurrentDictionary<PhysicalAddress,int>();

  override public int[] handler (int in_port, ref Packet packet)
  {
    int[] out_ports;

    if (packet is PacketDotNet.EthernetPacket)
    {
      EthernetPacket eth = ((PacketDotNet.EthernetPacket)packet);

      int lookup_out_port;
      // Forwarding decision.
      if (forwarding_table.TryGetValue(eth.DestinationHwAddress, out lookup_out_port))
      {
        if (lookup_out_port == in_port)
        {
          var tmp = Console.ForegroundColor;
          Console.ForegroundColor = ConsoleColor.Red;
          Console.WriteLine("Interface {0} was about to emit a frame it has just received.", PaxConfig.deviceMap[in_port].Name);
          Console.ForegroundColor = tmp;
          out_ports = new int[0];
        } else {
          out_ports = new int[1]{lookup_out_port};
        }
      } else {
        out_ports = MultiInterface_SimplePacketProcessor.broadcast(in_port);
      }

      // This might hold the value that's mapped to by eth.SourceHwAddress,
      // if one exists in our forwarding_table.
      int supposed_in_port;
      // Switch learns which port knows about the SourceHwAddress.
      if (forwarding_table.TryGetValue(eth.SourceHwAddress, out supposed_in_port))
      {
        if (supposed_in_port != in_port)
        {
          if (!forwarding_table.TryUpdate(eth.SourceHwAddress, in_port, supposed_in_port))
          {
            Console.WriteLine("Concurrent update of output port for " + eth.SourceHwAddress.ToString());
          }
#if DEBUG
          Debug.WriteLine("Relearned " + eth.SourceHwAddress.ToString() + " <- " + PaxConfig.deviceMap[in_port].Name);
#endif
        }
      } else {
        if (!forwarding_table.TryUpdate(eth.SourceHwAddress, in_port, supposed_in_port))
        {
          Console.WriteLine("Concurrent update of output port for " + eth.SourceHwAddress.ToString());
        }
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
