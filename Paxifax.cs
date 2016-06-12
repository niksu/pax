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
  }

  // FIXME crude design
  public static class PaxConfig {
    // This must be greater than 1;
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

  }

  public abstract class PacketProcessor {
    abstract public int handler (int in_port, ref Packet packet);

/*TODO
retrieve number of interfaces
forward to more than one interface
manufacture new packet(s)
*/
    public void packetHandler (object sender, CaptureEventArgs e)
    {
      var packet = PacketDotNet.Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
      int in_port = PaxConfig.rdeviceMap[e.Device.Name];

      int out_port = handler (in_port, ref packet);
      if (out_port > -1)
      {
        PaxConfig.deviceMap[out_port].SendPacket(packet);
      }
    }
  }
}
