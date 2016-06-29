/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net.NetworkInformation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Reflection;

namespace Pax
{
  public class Frontend {

    private static void usage() {
      Console.WriteLine("Required parameters: config file, and DLL containing packet handlers.");
    }

    private static void print_kv (string k, string v, bool secondary = false) {
      var tmp = Console.ForegroundColor;
      if (secondary)
        Console.ForegroundColor = ConsoleColor.Gray;
      else
        Console.ForegroundColor = ConsoleColor.White;
      Console.Write(k);
      if (secondary)
        Console.ForegroundColor = ConsoleColor.DarkYellow;
      else
        Console.ForegroundColor = ConsoleColor.Yellow;
      Console.WriteLine(v);
      //Console.ResetColor();
      Console.ForegroundColor = tmp;
    }

    private static void print_heading (string s) {
      var tmp_fore = Console.ForegroundColor;
      var tmp_back = Console.BackgroundColor;
      Console.ForegroundColor = ConsoleColor.Black;
      Console.BackgroundColor = ConsoleColor.White;
      Console.Write(s);
      Console.ResetColor();
      Console.ForegroundColor = tmp_fore;
      Console.BackgroundColor = tmp_back;
      Console.WriteLine();
    }

    private const string indent = "  ";

    public static int Main (string[] args) {
      // FIXME when we load the DLL and wiring.cfg, check them against each other (e.g., that all handlers exist)
      //       we can resolve all the "links" (by looking at return statements) and draw a wiring diagram. -- this can be a script.

#if DEBUG
      Debug.Listeners.Add(new TextWriterTraceListener(System.Console.Out));
#endif

      if (args.Length < 2)
      {
        usage();
        return -1;
      }

      // FIXME use getopts to get parameters
      PaxConfig.config_filename = args[0];
      PaxConfig.assembly_filename = args[1];

      // FIXME currently this is redundant, but it will be used after getopts gets used in the future.
      // These two parameters are essential for Pax to function.
      if (String.IsNullOrEmpty(PaxConfig.config_filename) ||
          String.IsNullOrEmpty(PaxConfig.assembly_filename))
      {
        usage();
        return -1;
      }

      // FIXME break up Main() into separate functions. The following block could be put into its own void function, for instance.
      Console.ForegroundColor = ConsoleColor.White;
      Console.Write ("✌ ");
      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.Write ("Pax v{0}", "0.1"/*FIXME const -- get value from AssemblyInfo*/);
      Console.ForegroundColor = ConsoleColor.White;
      Console.Write (" ☮ ");
      Console.ForegroundColor = ConsoleColor.DarkBlue;
      Console.Write ("http://www.cl.cam.ac.uk/~ns441/pax");
      Console.ForegroundColor = ConsoleColor.White;
      Console.WriteLine();
      print_kv  (indent + "running as ",
          (System.Environment.Is64BitProcess ? "64bit" : "32bit") + " process",
          true);
      print_kv  (indent + "on .NET runtime v", System.Environment.Version.ToString(),
                 true);
      print_kv  (indent + "OS is ",
          System.Environment.OSVersion.ToString() +
          " (" +
          (System.Environment.Is64BitOperatingSystem ? "64bit" : "32bit")
          + ")",
          true);
      print_kv  (indent + "OS running for (ticks) ", System.Environment.TickCount.ToString(),
                 true);
      print_kv  (indent + "No. processors available ", System.Environment.ProcessorCount.ToString(),
                 true);
      print_kv ("Using configuration file: ", PaxConfig.config_filename);
      print_kv ("Using assembly file: ", PaxConfig.assembly_filename);

      PaxConfig.assembly = Assembly.LoadFile(PaxConfig.assembly_filename);

      var devices = CaptureDeviceList.Instance;
      Debug.Assert (devices.Count >= 0);
      if (devices.Count == 0)
      {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("No capture devices found");
        return -1;
      }

      Console.ResetColor();
      print_heading("Configuration");
      Console.ResetColor();

      using (JsonTextReader r = new JsonTextReader(File.OpenText(PaxConfig.config_filename))) {
        JsonSerializer j = new JsonSerializer();
        PaxConfig.config = j.Deserialize<List<NetworkInterfaceConfig>>(r);
        PaxConfig.no_interfaces = PaxConfig.config.Count;
        PaxConfig.deviceMap = new ICaptureDevice[PaxConfig.no_interfaces];
        PaxConfig.interface_lead_handler = new string[PaxConfig.no_interfaces];
        PaxConfig.interface_lead_handler_obj = new PacketProcessor[PaxConfig.no_interfaces];

        int idx = 0;
        foreach (var i in PaxConfig.config) {
          Debug.Assert (idx < PaxConfig.no_interfaces);

          PaxConfig.deviceMap[idx] = null;
          //FIXME not using internal_name any more to avoid more lookups. Remove completely?
          //Console.Write(indent + i.internal_name);
          //Console.Write(" -- ");
          Console.ForegroundColor = ConsoleColor.Gray;
          Console.Write(indent + "[");
          Console.ForegroundColor = ConsoleColor.Green;
          Console.Write(idx.ToString());
          Console.ForegroundColor = ConsoleColor.Gray;
          Console.Write("] ");
          Console.ForegroundColor = ConsoleColor.Yellow;
          Console.WriteLine(i.interface_name);

          PaxConfig.interface_lead_handler[idx] = i.lead_handler;

          foreach (var device in devices)
          {
            if (device.Name == i.interface_name)
            {
              PaxConfig.deviceMap[idx] = device;
              PaxConfig.rdeviceMap.Add(device.Name, idx);
              device.Open();

              if (!String.IsNullOrEmpty(i.pcap_filter))
              {
                device.Filter = i.pcap_filter;
                print_kv (indent + /*FIXME code style sucks*/ indent +
                    "Setting filter: ", i.pcap_filter);
              }

              break;
            }
          }

          if (PaxConfig.deviceMap[idx] == null)
          {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("No match for '" + i.interface_name + "'");
            return -1;
          } else {
            print_kv (indent + /*FIXME code style sucks*/ indent +
                     "Link layer type: ", PaxConfig.deviceMap[idx].LinkType.ToString());
          }

          idx++;
        }
      }


      Console.ResetColor();
      print_heading("Scanning assembly");
      Console.ResetColor();

      Predicate<Type> allowedParamType = PacketProcessorHelper.IsAllowedConstructorParameterType;
      Func<Type, string, object> convertArg = PacketProcessorHelper.ConvertConstructorParameter;
      foreach (Type ty in PaxConfig.assembly.GetExportedTypes()
                                            .Where(typeof(PacketProcessor).IsAssignableFrom))
      {
#if MOREDEBUG
        Console.WriteLine("Trying to instantiate {0}", ty);
#endif
        IDictionary<string,string> environment =
          PaxConfig.config.Where(intf => ty.Name.Equals(intf.lead_handler)) // FIXME should non-port specific env be in a separate part of config?
                          .Select(intf => intf.environment)
                          .Where(dict => dict != null)
                          .SelectMany(dict => dict)
                          .ToLookup(pair => pair.Key, pair => pair.Value) // Allow multiple definitions of values
                          .ToDictionary(group => group.Key, group => group.First()); // FIXME resolve multiple definitions
#if MOREDEBUG
        Console.WriteLine("  Environment:");
        foreach (var pair in environment)
          Console.WriteLine("    {0} : {1}", pair.Key, pair.Value);
        Console.WriteLine("  Public constructors:");
        foreach (var ctor in ty.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
        {
          var parameters = ctor.GetParameters()
                               .Select(p => String.Format("{0}: {1}", p.Name, p.ParameterType.FullName));
          Console.WriteLine("    {0}({1})", ty.Name, String.Join(", ", parameters));
        }
#endif
        // Instantiate the packet processor
        PacketProcessor pp = PacketProcessorHelper.InstantiatePacketProcessor(ty, environment);
        if (pp == null)
        {
          Console.WriteLine("Could not instantiate type {0} - no valid constructor found. Please check your config.", ty.FullName);
          continue;
        }

        // Find which network interfaces this class is handling
        List<int> subscribed = new List<int>();

        for (int idx = 0; idx < PaxConfig.no_interfaces; idx++)
        {
          if ((!String.IsNullOrEmpty(PaxConfig.interface_lead_handler[idx])) &&
              ty.Name == PaxConfig.interface_lead_handler[idx])
          {
            subscribed.Add(idx);
            PaxConfig.interface_lead_handler_obj[idx] = pp;
          }
        }

        var tmp = Console.ForegroundColor;
        Console.Write (indent);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write (ty);
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write (" <- ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(
           String.Join(", ", subscribed.ConvertAll<string>(ofs => PaxConfig.deviceMap[ofs].Name)));
        // FIXME could also print type-related info of ty, such as which Pax interfaces it implements.
        Console.ForegroundColor = tmp;
      }
      // FIXME add check to see if there's an interface that references a lead_handler that doesn't appear in the assembly. That should be flagged up to the user, and lead to termination of Pax.

      Console.ForegroundColor = ConsoleColor.Gray;

      // Set up callbacks.
      Console.CancelKeyPress += new ConsoleCancelEventHandler(shutdown);
      for (int idx = 0; idx < PaxConfig.no_interfaces; idx++)
      {
        if (PaxConfig.interface_lead_handler_obj[idx] == null)
        {
          var tmp = Console.ForegroundColor;
          Console.ForegroundColor = ConsoleColor.Red;
          Console.Write("No packet processor for ");
          Console.ForegroundColor = ConsoleColor.Yellow;
          Console.Write(PaxConfig.deviceMap[idx].Name);
          Console.ForegroundColor = ConsoleColor.Gray;
          Console.Write(" (");
          Console.ForegroundColor = ConsoleColor.Green;
          Console.Write(idx);
          Console.ForegroundColor = ConsoleColor.Gray;
          Console.WriteLine(")");
          Console.ForegroundColor = tmp;
        } else {
          var tmp = Console.ForegroundColor;
          Console.ForegroundColor = ConsoleColor.Gray;
          Console.Write("Registered packet processor for ");
          Console.ForegroundColor = ConsoleColor.Yellow;
          Console.Write(PaxConfig.deviceMap[idx].Name);
          Console.ForegroundColor = ConsoleColor.Gray;
          Console.Write(" (");
          Console.ForegroundColor = ConsoleColor.Green;
          Console.Write(idx);
          Console.ForegroundColor = ConsoleColor.Gray;
          Console.WriteLine(")");
          Console.ForegroundColor = tmp;
          PaxConfig.deviceMap[idx].OnPacketArrival +=
            // FIXME can use packet handler directly?
            new PacketArrivalEventHandler(PaxConfig.interface_lead_handler_obj[idx].packetHandler);
        }
      }

      Console.ResetColor();
      print_heading("Starting");
      Console.ResetColor();

      // FIXME accept a -j parameter to limit number of threads?
      Parallel.ForEach(PaxConfig.deviceMap, device => device.Capture());

      // FIXME am i right in thinking that this location is unreachable, since the threads won't terminate unless the whole process is being terminated.
      return 0;
    }

    //Cleanup
    private static void shutdown (object sender, ConsoleCancelEventArgs args)
    {
      for (int idx = 0; idx < PaxConfig.no_interfaces; idx++)
      {
        PaxConfig.deviceMap[idx].Close();
      }

      Console.ResetColor();
      Console.WriteLine ("Terminating");
    }
  }
}
