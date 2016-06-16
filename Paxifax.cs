/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net.NetworkInformation;
using System.Collections;
using System.Collections.Generic;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;

namespace Pax {

  // FIXME use javadoc-style comments to describe the API
  public class NetworkInterfaceConfig
  {
    // FIXME I no longer use this -- perhaps can erase.
    public string internal_name {get; set;}
    // A pcap expression used to filter traffic that we see coming from this interface.
    public string pcap_filter {get; set;}
    // Either the system name (e.g., "eth0", etc), or an indicator of a file as follows:
    // ">filename" writes to a pcap file
    // "<filename" reads from a pcap file
    // "<filename1;<filename2" reads from two pcap files in order TODO currently we can only have one of each file
    // ">filename;<filename1" writes to a pcap file and reads from another.
    // FIXME currently file input/output is not supported.
    public string interface_name {get; set;}
    // The function that is called when traffic arrives on this interface.
    public string lead_handler {get; set;}

    public IDictionary<string, string> environment {get; set;}
  }

  // FIXME crude design
  public static class PaxConfig {
    // This is the number of interfaces in the configuration file.
    // This must be greater than 1.
    // Note that no_interfaces may be larger than the number of interfaces to which a packet processor has
    // been attached (i.e., interfaces that have a "lead_handler" defined in the configuration).
    // But this is fine, because there might be interface for which we don't want to process
    // their incoming packets, but we want to be able to forward packets to them nonetheless.
    public static int no_interfaces;

    // Array "maps" from device offset to the device object.
    public static ICaptureDevice[] deviceMap;
    // Map from device name (e.g., "eth1") to device offset.
    public static Dictionary<string, int> rdeviceMap = new Dictionary<string, int>();
    // Map from device offset to the name of its handler.
    public static string[] interface_lead_handler;
    public static PacketProcessor[] interface_lead_handler_obj;

    // FIXME better to link to function (rather than have indirection) to speed things up at runtime.
    // The file containing the catalogue of network interfaces.
    public static string config_filename;
    // The assembly containing the packet handlers that the user wishess to use.
    public static string assembly_filename;
    public static Assembly assembly;

    public static List<NetworkInterfaceConfig> config;

    public static string resolve_config_parameter (int port_no, string key) {
      NetworkInterfaceConfig port_conf;
      try {
        port_conf = config[port_no];
      } catch (ArgumentOutOfRangeException e) {
        throw (new Exception ("resolve_config_parameter: port_no > config size, since " + port_no.ToString() + " > " +
              config.Count.ToString()));
      }

      if (port_conf.environment == null)
      {
        throw (new Exception ("resolve_config_parameter: 'environment' has not been defined " +
              "for port_no " + port_no.ToString() + ". Configuration is incomplete."));
      }

      if (port_conf.environment.ContainsKey(key)) {
        return port_conf.environment[key];
      } else {
        throw (new Exception ("resolve_config_parameter: could not find key '" + key + "'" +
              " (in 'environment') for port_no " + port_no.ToString() + ". Configuration is incomplete."));
      }
    }

    public static bool can_resolve_config_parameter (int port_no, string key) {
      NetworkInterfaceConfig port_conf;
      if (port_no >= config.Count)
      {
        return false;
      }

      port_conf = config[port_no];
      if (port_conf.environment == null)
      {
        return false;
      }

      return (port_conf.environment.ContainsKey(key));
    }
  }

  public interface PacketProcessor {
    void packetHandler (object sender, CaptureEventArgs e);
  }

  // Simple packet processor: it can only transform the given packet and forward it to at most one interface.
  public abstract class SimplePacketProcessor : PacketProcessor {
    // Return the offset of network interface that "packet" is to be forwarded to.
    abstract public int handler (int in_port, ref Packet packet);

    public void packetHandler (object sender, CaptureEventArgs e)
    {
      var packet = PacketDotNet.Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
      int in_port = PaxConfig.rdeviceMap[e.Device.Name];

      int out_port = handler (in_port, ref packet);
#if DEBUG
      Debug.Write(PaxConfig.deviceMap[in_port].Name + " -1> ");
#endif
      if (out_port > -1)
      {
        PaxConfig.deviceMap[out_port].SendPacket(packet);
#if DEBUG
        Debug.WriteLine(PaxConfig.deviceMap[out_port].Name);
      } else {
        Debug.WriteLine("");
#endif
      }
    }
  }

  // Simple packet processor that can forward to multiple interfaces. It is "simple" because
  // it can only transform the given packet, and cannot generate new ones.
  public abstract class MultiInterface_SimplePacketProcessor : PacketProcessor {
    // Return the offsets of network interfaces that "packet" is to be forwarded to.
    abstract public int[] handler (int in_port, ref Packet packet);

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

      int[] out_ports = handler (in_port, ref packet);
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

  public class PacketProcessor_Chain : PacketProcessor {
    List<PacketProcessor> chain;

    public PacketProcessor_Chain (List<PacketProcessor> chain) {
      this.chain = chain;
    }

    public void packetHandler (object sender, CaptureEventArgs e) {
      foreach (PacketProcessor pp in chain) {
        pp.packetHandler (sender, e);
      }
    }
  }

  public static class PacketEncap {
    public static bool Encapsulates (this Packet p, params Type[] encs) {
      if (encs.Length > 0)
      {
        if (p.PayloadPacket == null)
        {
          // "p" doesn't encapsulate whatever it is that "encs" asks it to,
          // since "p" doesn't encapsulate anything.
          return false;
        } else {
          if (encs[0].IsAssignableFrom(p.PayloadPacket.GetType())) {
            return p.PayloadPacket.Encapsulates(encs.Skip(1).ToArray());
          } else {
            return false;
          }
        }
      } else {
        return true;
      }
    }
  }
}
