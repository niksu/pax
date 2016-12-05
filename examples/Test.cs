/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016
Jonny Shipton, Cambridge University Computer Lab, July 2016

This module is mainly intended to collect test cases and examples of instantiating
packet processors, some of which are implemented in other modules.

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
using System.Threading;
using System.Net;

using Pax;

// FIXME there's not indicator that multiple network interfaces might be using this
//       class. this could lead to concurrency-related issues, which might be avoided
//       through locking for instance. perhaps worth signalling that this class is
//       "multithread-ready" by having it inherit from some indicator interface?
// NOTE i use Interlocked for atomic operations, to mitigate concurrency issues.
public class Test1 : SimplePacketProcessor {
  private int count = 0;

  override public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    Console.Write("{0}({1}/{2}) ", in_port, Interlocked.Increment(ref count), PaxConfig_Lite.no_interfaces);
    return (new ForwardingDecision.SinglePortForward(1)); // This behaves like a static switch: it forwards everything to port 1.
  }
}

public class Test2 : SimplePacketProcessor {

  override public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    Console.Write("?");
    return ForwardingDecision.Drop.Instance;
  }
}

public class Test3 : MultiInterface_SimplePacketProcessor {
#if DEBUG
  private int count = 0;
#endif

  override public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
#if DEBUG
    Console.Write("!");
    Console.Write("{0}({1}/{2}) ", in_port, Interlocked.Increment(ref count), PaxConfig_Lite.no_interfaces);
#endif
    int[] target_ports = ForwardingDecision.broadcast_raw(in_port);
    return (new ForwardingDecision.MultiPortForward(target_ports));
  }
}

public class Printer : IPacketProcessor {
  int id = 0;

  // NOTE we always need to have a default constructor, because of how Pax works.
  public Printer () { }

  public Printer (int id) {
    this.id = id;
  }

  public void packetHandler (object sender, CaptureEventArgs e) {
    Console.WriteLine(id);
  }

  public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    return ForwardingDecision.Drop.Instance;
  }
}

// Nested packet processor -- it contains a sequence of chained processors.
public class Nested_Chained_Test : IPacketProcessor {
  IPacketProcessor pp =
    new PacketProcessor_Chain(new List<IPacketProcessor>()
        {
          new Printer(1),
          new Printer(2),
        });

  public void packetHandler (object sender, CaptureEventArgs e) {
    pp.packetHandler (sender, e);
  }

  public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    return (pp.process_packet (in_port, ref packet));
  }
}

public class Nested_Chained_Test2 : IPacketProcessor {
  ForwardingDecision[] mirror_cfg;
  IPacketProcessor pp;

  public Nested_Chained_Test2 () {
    mirror_cfg = Mirror.InitialConfig(PaxConfig_Lite.no_interfaces);
    Debug.Assert(PaxConfig_Lite.no_interfaces >= 3);
    mirror_cfg[0] = new ForwardingDecision.SinglePortForward(2); // Mirror port 0 to port 2

    this.pp =
      new PacketProcessor_Chain(new List<IPacketProcessor>()
          {
  /* FIXME would be tidier to use this
            new Mirror(
              Mirror.InitialConfig(PaxConfig_Lite.no_interfaces).MirrorPort(0, 2)
              ),
  */
            new Mirror(mirror_cfg),
            new LearningSwitch(),
          });
  }


  public void packetHandler (object sender, CaptureEventArgs e) {
    pp.packetHandler (sender, e);
  }

  public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    return (pp.process_packet (in_port, ref packet));
  }
}

/// <summary>
/// Tallyer prints `|[tag]` every time a packet is received, where tag is a port-specific configurable string.
/// The purpose of Tallyer is to demonstrate and test that default constructors can be used for automatic
///  instantiation of PacketProcessors, as well as that port-specific configuration properties can be used.
/// </summary>
public class Tallyer : IPacketProcessor {

  // implicit default constructor

  public void packetHandler (object sender, CaptureEventArgs e) {
    int port = Array.IndexOf(PaxConfig.deviceMap, e.Device);
    string tag = "";
    if (PaxConfig.can_resolve_config_parameter(port, "tag"))
      tag = PaxConfig.resolve_config_parameter(port, "tag");

    Console.WriteLine("|" + tag);
  }

  public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    return ForwardingDecision.Drop.Instance;
  }
}

/// <summary>
/// Dinger just writes `*ding*` every time a packet is received.
/// The purpose of Dinger is to demonstrate and test the types that can be used as constructor parameters.
/// </summary>
public class Dinger : IPacketProcessor {

  // Constructor taking lots of interesting arguments
  public Dinger(
        Boolean _bool, Byte _byte, SByte _sbyte, UInt16 _ushort, Int16 _short,
        UInt32 _uint, Int32 _int, UInt64 _ulong, Int64 _long, Decimal _decimal,
        Single _float, Double _double, String _string, DateTime _DateTime,
        TimeSpan _TimeSpan, System.Net.IPAddress _IPAddress, PhysicalAddress _PhysicalAddress)
  {
    // Just check the class types for null in the interest of brevity
    if (_string == null) Console.WriteLine("_string == null");
    if (_IPAddress == null) Console.WriteLine("_IPAddress == null");
    if (_PhysicalAddress == null) Console.WriteLine("_PhysicalAddress == null");
  }

  public void packetHandler (object sender, CaptureEventArgs e) {
    Console.WriteLine("*ding*");
  }

  public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    return ForwardingDecision.Drop.Instance;
  }
}

public class Generator : IActive, IPacketProcessor {
  ICaptureDevice device = null; // NOTE if this is made into an array, we can model multipath behaviour?
  Timer timer;

  ushort src_port;
  ushort dst_port;
  IPAddress src_ip;
  IPAddress dst_ip;
  PhysicalAddress src_mac;
  PhysicalAddress dst_mac;
  int interval;

  public Generator (int interval, UInt16 src_port, UInt16 dst_port, IPAddress src_ip,
   IPAddress dst_ip, PhysicalAddress src_mac, PhysicalAddress dst_mac) {
    this.interval = interval;
    this.src_port = src_port;
    this.dst_port = dst_port;
    this.src_ip = src_ip;
    this.dst_ip = dst_ip;
    this.src_mac = src_mac;
    this.dst_mac = dst_mac;
  }

  public void packetHandler (object sender, CaptureEventArgs e) {
    // FIXME how to indicate if we don't want to register a handler?
  }
  public ForwardingDecision process_packet (int in_port, ref Packet packet) {
    // FIXME how to indicate if we don't want to register a handler?
    return null;
  }

  private Packet GeneratePacket() {
    // FIXME in this case we always generate the same packet, but keep
    //       regenerating it -- this can be made more efficient!
    var tcp_p = new TcpPacket(src_port, dst_port);
    var ip_p = new IPv4Packet(src_ip, dst_ip);
    var eth_p = new EthernetPacket(src_mac, dst_mac, EthernetPacketType.None);
    eth_p.PayloadPacket = ip_p;
    ip_p.PayloadPacket = tcp_p;
    return eth_p;
  }

  public void PreStart (ICaptureDevice device) {
    Debug.Assert(this.device == null);
    this.device = device;
    Console.WriteLine ("Generator configured");
  }

  public void Stop () {
    Console.WriteLine ();
    // NOTE we'd use something like "lock (this) {active = false;}" if we used a
    //      running "while" rather than timers.
    if (timer != null) {timer.Dispose();}
    Console.WriteLine ("Generator stopped");
  }

  public void Start () {
    Console.WriteLine ("Generator starting");
    timer = new Timer(Pulse, null, 0, interval);
  }

  public void Pulse (Object o) {
    Console.Write (".");
    device.SendPacket(GeneratePacket());
  }
}
