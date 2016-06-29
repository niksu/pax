

using System;
using System.Collections.Generic;
using System.Reflection;
using SharpPcap;

namespace Pax
{
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
}
