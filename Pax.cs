/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016
Jonny Shipton, Cambridge University Computer Lab, July 2016

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
using Mono.Options;
using System.Threading;

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
      Console.ForegroundColor = ConsoleColor.Black;
      Console.BackgroundColor = ConsoleColor.White;
      Console.Write(s);
      Console.ResetColor();
      Console.WriteLine();
    }

    private const string indent = "  ";

    public static int Main (string[] args) {
      Console.ResetColor();

      // FIXME when we load the DLL and wiring.cfg, check them against each other (e.g., that all handlers exist)
      //       we can resolve all the "links" (by looking at return statements) and draw a wiring diagram. -- this can be a script.

#if DEBUG
      Debug.Listeners.Add(new TextWriterTraceListener(System.Console.Out));
#endif

      OptionSet p = new OptionSet ()
        .Add ("v", _ => PaxConfig.opt_verbose = true)
        .Add ("no-default", _ => PaxConfig.opt_no_default = true);
      args = p.Parse(args).ToArray();

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

      print_heading("Starting");

      // FIXME accept a -j parameter to limit number of threads?
      foreach (var device in PaxConfig.deviceMap)
        device.StartCapture();

      return 0;
    }

    public static string Version {
      get { return FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion; }
    }

    private static void PrintIntro()
    {
      Console.ForegroundColor = ConsoleColor.White;
      Console.Write ("✌ ");
      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.Write ("Pax v{0}", Version);
      Console.ForegroundColor = ConsoleColor.White;
      Console.Write (" ☮ ");
      Console.ForegroundColor = ConsoleColor.DarkBlue;
      Console.Write ("http://www.cl.cam.ac.uk/~ns441/pax");
      Console.ForegroundColor = ConsoleColor.White;
      Console.WriteLine();
      if (PaxConfig.opt_verbose) {
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
      }

      print_kv ("Using configuration file: ", PaxConfig.config_filename);
      print_kv ("Using assembly file: ", PaxConfig.assembly_filename);
    }

    private static void Configure(CaptureDeviceList devices)
    {
      print_heading("Configuration");

      using (JsonTextReader r = new JsonTextReader(File.OpenText(PaxConfig.config_filename))) {
        JsonSerializer j = new JsonSerializer();
        j.DefaultValueHandling = DefaultValueHandling.Populate;
        PaxConfig.configFile = j.Deserialize<ConfigFile>(r);
        PaxConfig_Lite.no_interfaces = PaxConfig.config.Count;
        PaxConfig.deviceMap = new ICaptureDevice[PaxConfig_Lite.no_interfaces];
        PaxConfig.interface_lead_handler = new string[PaxConfig_Lite.no_interfaces];
        PaxConfig.interface_lead_handler_obj = new IPacketProcessor[PaxConfig_Lite.no_interfaces];

        int idx = 0;
        foreach (var i in PaxConfig.config) {
          Debug.Assert (idx < PaxConfig_Lite.no_interfaces);

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
              // Configure this device's read timeout
              device.Open(DeviceMode.Normal, i.read_timeout);

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
      print_heading("Scanning assembly");

      // Inspect each type that implements PacketProcessor, trying to instantiate it for use
      foreach (Type type in PaxConfig.assembly.GetExportedTypes()
                                            .Where(typeof(IPacketProcessor).IsAssignableFrom))
      {
        // Find which network interfaces this class is handling
        List<int> subscribed = new List<int>();
        IPacketProcessor pp = null;
        for (int idx = 0; idx < PaxConfig_Lite.no_interfaces; idx++)
        {
          // Does this interface have this type specified as the lead handler?
          if ((!String.IsNullOrEmpty(PaxConfig.interface_lead_handler[idx])) &&
              type.Name == PaxConfig.interface_lead_handler[idx])
          {
            // Only instantiate pp if needed
            if (pp == null)
              pp = InstantiatePacketProcessor(type);
            if (pp == null)
              // If pp is still null, then we couldn't instantiate it.
              break;
            subscribed.Add(idx);
            PaxConfig.interface_lead_handler_obj[idx] = pp;
          }
        }

        Console.Write (indent);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write (type);

        if (PaxConfig.opt_verbose) {
          // List the Pax interfaces this type implements:
          Console.ForegroundColor = ConsoleColor.Gray;
          Console.Write(" : ");
          Console.ForegroundColor = ConsoleColor.Cyan;
          Console.Write(String.Join(", ", PacketProcessorHelper.GetUsedPaxTypes(type).Select(t => t.Name)));
        }

        // Print which interfaces this type is the handler for
        if (subscribed.Count != 0) {
          Console.ForegroundColor = ConsoleColor.Gray;
          Console.Write (" <- ");
          Console.ForegroundColor = ConsoleColor.Yellow;
          Console.Write(
             String.Join(", ", subscribed.ConvertAll<string>(ofs => PaxConfig.deviceMap[ofs].Name)));
        }

        Console.WriteLine("");
      }
      // FIXME add check to see if there's an interface that references a lead_handler that doesn't appear in the assembly. That should be flagged up to the user, and lead to termination of Pax.
    }

    private static IPacketProcessor InstantiatePacketProcessor(Type type)
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
      IPacketProcessor pp = PacketProcessorHelper.InstantiatePacketProcessor(type, arguments);
      if (pp == null)
        Console.WriteLine("Couldn't instantiate {0}", type.FullName);

      // FIXME could extract this into a separate function to check version
      //       compatibility, as part of a more general extraction of an API
      //       from the Frotend class.
      System.Version version = new System.Version(Frontend.Version);
      if (pp is IVersioned) {
        IVersioned pp_v = pp as IVersioned;
        if (pp_v.expected_major_Pax_version() != version.Major ||
            pp_v.expected_minor_Pax_version() != version.Minor) {

          // FIXME create custom version-related exception.
          throw new Exception("Version incompatibility: could not instantiate " + type.ToString());
        }
      }

      return pp;
    }

    private static void RegisterHandlers()
    {
      Console.ForegroundColor = ConsoleColor.Gray;

      // Set up callbacks.
      Task.Factory.StartNew(() =>
        {
          while (true)
          {
            var key = Console.ReadKey();
            // Shutdown on ^D
            if ((key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.D)
              || (int)key.KeyChar == 4)
            {
              shutdown(null, null);
            }
          }
        });
      // Shutdown on ^C -- FIXME remove handling of ^C or ^D?
      Console.CancelKeyPress += new ConsoleCancelEventHandler(shutdown);
      for (int idx = 0; idx < PaxConfig_Lite.no_interfaces; idx++)
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

          // If we don't have a packet processor for an interface, we assign the Dropper.
          if (!PaxConfig.opt_no_default) {
            PaxConfig.deviceMap[idx].OnPacketArrival += (new Dropper()).packetHandler;
          }
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

          // If the packet processor is "active" then start it.
          if (PaxConfig.interface_lead_handler_obj[idx] is IActive) {
            var o = PaxConfig.interface_lead_handler_obj[idx] as IActive;
            o.PreStart (PaxConfig.deviceMap[idx]);
            Thread t = new Thread (new ThreadStart (o.Start));
            t.Start();
          }
        }
      }
    }

    //Cleanup
    private static void shutdown (object sender, ConsoleCancelEventArgs args)
    {
      for (int idx = 0; idx < PaxConfig_Lite.no_interfaces; idx++)
      {
        // If the packet processor is "active" then stop it now.
        if (PaxConfig.interface_lead_handler_obj[idx] is IActive) {
          ((IActive)PaxConfig.interface_lead_handler_obj[idx]).Stop();
        }

        // Set the capture timeout, as without the program can hang indefinitely.
        // Cause unknown, but setting any timeout seems to fix it. Even with timeout
        //  of 1s, the program shuts down immediately.
        PaxConfig.deviceMap[idx].StopCaptureTimeout = TimeSpan.FromSeconds(1);

        PaxConfig.deviceMap[idx].Close();
      }

      Console.ResetColor();
      Console.WriteLine ("Terminating");
    }

    public static void shutdown () {
      shutdown(null, null);
    }
  }
}
