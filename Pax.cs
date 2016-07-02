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

      PrintIntro();

      PaxConfig.assembly = Assembly.LoadFile(PaxConfig.assembly_filename);

      var devices = CaptureDeviceList.Instance;
      Debug.Assert (devices.Count >= 0);
      if (devices.Count == 0)
      {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("No capture devices found");
        return -1;
      }

      Configure(devices);

      LoadExternalHandlersFromDll();
      
      RegisterHandlers();  

      Console.ResetColor();
      print_heading("Starting");
      Console.ResetColor();

      // FIXME accept a -j parameter to limit number of threads?
      Parallel.ForEach(PaxConfig.deviceMap, device => device.Capture());

      // FIXME am i right in thinking that this location is unreachable, since the threads won't terminate unless the whole process is being terminated.
      return 0;
    }

    private static void PrintIntro()
    {
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
    }
    
    private static void Configure(CaptureDeviceList devices)
    {
      Console.ResetColor();
      print_heading("Configuration");
      Console.ResetColor();

      using (JsonTextReader r = new JsonTextReader(File.OpenText(PaxConfig.config_filename))) {
        JsonSerializer j = new JsonSerializer();
        PaxConfig.configFile = j.Deserialize<ConfigFile>(r);
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
            Environment.Exit(-1);
          } else {
            print_kv (indent + /*FIXME code style sucks*/ indent +
                     "Link layer type: ", PaxConfig.deviceMap[idx].LinkType.ToString());
          }

          idx++;
        }
      }
    }

    private static void LoadExternalHandlersFromDll()
    {
      Console.ResetColor();
      print_heading("Scanning assembly");
      Console.ResetColor();

      // Inspect each type that implements PacketProcessor, trying to instantiate it for use
      foreach (Type type in PaxConfig.assembly.GetExportedTypes()
                                            .Where(typeof(PacketProcessor).IsAssignableFrom))
      {
        // Find which network interfaces this class is handling
        List<int> subscribed = new List<int>();
        PacketProcessor pp = null;
        for (int idx = 0; idx < PaxConfig.no_interfaces; idx++)
        {
          // Does this interface have this type specified as the lead handler?
          if ((!String.IsNullOrEmpty(PaxConfig.interface_lead_handler[idx])) &&
              type.Name == PaxConfig.interface_lead_handler[idx])
          {
            // Only instatiate pp if needed
            if (pp == null)
              pp = InstatiatePacketProcessor(type);
            if (pp == null)
              // If pp is still null, then we couldn't instantiate it.
              break;
            subscribed.Add(idx);
            PaxConfig.interface_lead_handler_obj[idx] = pp;
          }
        }

        // Print which interfaces this type is the handler for
        var tmp = Console.ForegroundColor;
        Console.Write (indent);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write (type);
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write (" <- ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(
           String.Join(", ", subscribed.ConvertAll<string>(ofs => PaxConfig.deviceMap[ofs].Name)));

        // List the Pax interfaces this type implements:
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("  : ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(String.Join(", ", PacketProcessorHelper.GetUsedPaxTypes(type).Select(t => t.Name)));
        Console.ForegroundColor = tmp;
      }
      // FIXME add check to see if there's an interface that references a lead_handler that doesn't appear in the assembly. That should be flagged up to the user, and lead to termination of Pax.
    }

    private static PacketProcessor InstatiatePacketProcessor(Type type)
    {
      #if MOREDEBUG
      Console.WriteLine("Trying to instantiate {0}", type);
      #endif

      // Get the constructor arguments for this type from the config
      IDictionary<string,string> arguments =
        PaxConfig.configFile.handlers?.Where(handler => type.Name.Equals(handler.class_name))
                                      .Select(intf => intf.args)
                                      .SingleOrDefault();
      if (arguments == null) arguments = new Dictionary<string,string>();

      #if MOREDEBUG
      Console.WriteLine("  Arguments:");
      foreach (var pair in arguments)
        Console.WriteLine("    {0} : {1}", pair.Key, pair.Value);
      Console.WriteLine("  Public constructors:");
      foreach (var ctor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
        Console.WriteLine("    {0}", PacketProcessorHelper.ConstructorString(ctor));
      #endif

      // Instantiate the packet processor
      PacketProcessor pp = PacketProcessorHelper.InstantiatePacketProcessor(type, arguments);
      if (pp == null)
        Console.WriteLine("Couldn't instantiate {0}", type.FullName);
      return pp;
    }

    private static void RegisterHandlers()
    {
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
            PaxConfig.interface_lead_handler_obj[idx].packetHandler;
        }
      }
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
