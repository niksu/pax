/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016
Jonny Shipton, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Collections.Generic;
using PacketDotNet;
using SharpPcap;
using System.Text;
using System.Diagnostics;

// FIXME use javadoc-style comments to describe the API
namespace Pax {

  // A more abstract interface to packet processors.
  public interface IAbstract_PacketProcessor {
    ForwardingDecision process_packet (int in_port, ref Packet packet);
  }

/* FIXME Rather than the sort of specification above, I'd much rather be able to
   subtype derivatives of Abstract_PacketProcessor by specialising the
   ForwardingDecision result of process_packet. One idea is to use the
   following spec (but note that this would add lots of complications
   elsewhere, particularly in the reflection code):

  // NOTE this can be specialised by specialising parameter T (ForwardingDecision)
  //      to specific kinds of decisions. I use this feature below.
  public interface PacketProcessor<T> where T : ForwardingDecision {
    T process_packet (int in_port, ref Packet packet);
  }

  then one code define:

  public abstract class SimplePacketProcessor : PacketProcessor<ForwardingDecision.SinglePortForward> {
   ...
    abstract public ForwardingDecision.SinglePortForward process_packet (int in_port, ref Packet packet);

  i.e., we'd use C#'s type checker instead of the silly "is" checks at runtime.
*/

  public interface IHostbased_PacketProcessor {
    void packetHandler (object sender, CaptureEventArgs e);
  }

  public interface IPacketProcessor : IAbstract_PacketProcessor, IHostbased_PacketProcessor {}

  // A packet monitor does not output anything onto the network, it simply
  // accumulates state based on what it observes happening on the network.
  // It might produce output on side-channels, through side-effects.
  // This could be used for diagnosis, to observe network activity and print
  // digests to the console or log.
  public abstract class PacketMonitor : IPacketProcessor {
    abstract public ForwardingDecision process_packet (int in_port, ref Packet packet);

    public void packetHandler (object sender, CaptureEventArgs e)
    {
      var packet = PacketDotNet.Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
      int in_port = PaxConfig.rdeviceMap[e.Device.Name];

      Debug.Assert(process_packet (in_port, ref packet) is ForwardingDecision.Drop);
#if DEBUG
      // FIXME could append name of the class in the debug message, so we know which
      //       packet processor is being used.
      Debug.Write(PaxConfig.deviceMap[in_port].Name + " -|");
#endif
    }
  }

  // Simple packet processor: it can only transform the given packet and forward it to at most one interface.
  public abstract class SimplePacketProcessor : IPacketProcessor {
    // Return the offset of network interface that "packet" is to be forwarded to.
    abstract public ForwardingDecision process_packet (int in_port, ref Packet packet);

    public void packetHandler (object sender, CaptureEventArgs e)
    {
      var packet = PacketDotNet.Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
      int in_port = PaxConfig.rdeviceMap[e.Device.Name];

      int out_port;
      ForwardingDecision des = process_packet (in_port, ref packet);
      if (des is ForwardingDecision.SinglePortForward)
      {
        out_port = ((ForwardingDecision.SinglePortForward)des).target_port;
      } else {
        throw (new Exception ("Expected SinglePortForward"));
      }

#if DEBUG
      Debug.Write(PaxConfig.deviceMap[in_port].Name + " -1> ");
#endif
      if (out_port > -1)
      {
        var device = PaxConfig.deviceMap[out_port];
        if (packet is EthernetPacket)
          ((EthernetPacket)packet).SourceHwAddress = device.MacAddress;
        device.SendPacket(packet);
#if DEBUG
        Debug.WriteLine(PaxConfig.deviceMap[out_port].Name);
      } else {
        Debug.WriteLine("<dropped>");
#endif
      }
    }
  }

  // Simple packet processor that can forward to multiple interfaces. It is "simple" because
  // it can only transform the given packet, and cannot generate new ones.
  public abstract class MultiInterface_SimplePacketProcessor : IPacketProcessor {
    // Return the offsets of network interfaces that "packet" is to be forwarded to.
    abstract public ForwardingDecision process_packet (int in_port, ref Packet packet);

    public static int[] broadcast (int in_port)
    {
      int[] out_ports = new int[PaxConfig.no_interfaces - 1];
      // We retrieve number of interfaces in use from PaxConfig.
      // Naturally, we exclude in_port from the interfaces we're forwarding to since this is a broadcast.
      int idx = 0;
      for (int ofs = 0; ofs < PaxConfig.no_interfaces; ofs++)
      {
        if (ofs != in_port)
        {
          out_ports[idx] = ofs;
          idx++;
        }
      }
      return out_ports;
    }

    public void packetHandler (object sender, CaptureEventArgs e)
    {
      var packet = PacketDotNet.Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
      int in_port = PaxConfig.rdeviceMap[e.Device.Name];

      int[] out_ports;
      ForwardingDecision des = process_packet (in_port, ref packet);
      if (des is ForwardingDecision.MultiPortForward)
      {
        out_ports = ((ForwardingDecision.MultiPortForward)des).target_ports;
      } else {
        throw (new Exception ("Expected SinglePortForward"));
      }

#if DEBUG
      Debug.Write(PaxConfig.deviceMap[in_port].Name + " -> ");
#endif
      for (int idx = 0; idx < out_ports.Length; idx++)
      {
        PaxConfig.deviceMap[out_ports[idx]].SendPacket(packet);
#if DEBUG
        if (idx < out_ports.Length - 1)
        {
          Debug.Write(PaxConfig.deviceMap[out_ports[idx]].Name + ", ");
        } else {
          Debug.Write(PaxConfig.deviceMap[out_ports[idx]].Name);
        }
#endif
      }

#if DEBUG
      Debug.WriteLine("");
#endif
    }
  }

  public class PacketProcessor_Chain : IPacketProcessor {
    List<IPacketProcessor> chain;

    public PacketProcessor_Chain (List<IPacketProcessor> chain) {
      this.chain = chain;
    }

    public void packetHandler (object sender, CaptureEventArgs e) {
      foreach (IPacketProcessor pp in chain) {
        pp.packetHandler (sender, e);
      }
    }

    public ForwardingDecision process_packet (int in_port, ref Packet packet)
    {
      ForwardingDecision fd = null;

      //We return the ForwardingDecision made by the last element in the chain.
      foreach (IPacketProcessor pp in chain) {
        fd = pp.process_packet (in_port, ref packet);
      }

      return fd;
    }
  }
}
