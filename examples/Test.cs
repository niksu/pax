/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

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

  override public int handler (int in_port, ref Packet packet)
  {
//    Console.Write("!");
    Console.Write("{0}({1}/{2}) ", in_port, Interlocked.Increment(ref count), PaxConfig.no_interfaces);
    return 1; // This behaves like a static switch: it forwards everything to port 1.
  }
}

public class Test2 : SimplePacketProcessor {

  override public int handler (int in_port, ref Packet packet)
  {
    Console.Write("?");
    return -1; // i.e., drop packet, since it's not being forwarded to any interface.
  }
}

public class Test3 : MultiInterface_SimplePacketProcessor {
  private int count = 0;

  override public int[] handler (int in_port, ref Packet packet)
  {
//    Console.Write("!");
//    Console.Write("{0}({1}/{2}) ", in_port, count++, PaxConfig.no_interfaces);
    return (MultiInterface_SimplePacketProcessor.broadcast(in_port));
  }
}

public class Printer : PacketProcessor {
  int id = 0;

  // NOTE we always need to have a default constructor, because of how Pax works.
  public Printer () { }

  public Printer (int id) {
    this.id = id;
  }

  public void packetHandler (object sender, CaptureEventArgs e) {
    Console.WriteLine(id);
  }
}

// Nested packet processor -- it contains a sequence of chained processors.
public class Nested_Chained_Test : PacketProcessor {
  PacketProcessor pp =
    new PacketProcessor_Chain(new List<PacketProcessor>()
        {
          new Printer(1),
          new Printer(2),
        });

  public void packetHandler (object sender, CaptureEventArgs e) {
    pp.packetHandler (sender, e);
  }
}

public class Nested_Chained_Test2 : PacketProcessor {
  int[] mirror_cfg;
  PacketProcessor pp;

  public Nested_Chained_Test2 () {
    mirror_cfg = Mirror.InitialConfig(PaxConfig.no_interfaces);
    Debug.Assert(PaxConfig.no_interfaces >= 3);
    mirror_cfg[0] = 2; // Mirror port 0 to port 2

    PacketProcessor pp =
      new PacketProcessor_Chain(new List<PacketProcessor>()
          {
  /* FIXME would be tidier to use this
            new Mirror(
              Mirror.InitialConfig(PaxConfig.no_interfaces).MirrorPort(0, 2)
              ),
  */
            new Mirror(mirror_cfg),
            new LearningSwitch(),
          });
  }


  public void packetHandler (object sender, CaptureEventArgs e) {
    pp.packetHandler (sender, e);
  }
}

public class Nested_NAT : PacketProcessor {
  // NOTE we use 0 below since we're interested in the information
  //      related to the outside-facing port, which the NAT designates as being port 0.
  const int outside_port = 0;
  PacketProcessor pp = null;

  public Nested_NAT () {
    if (PaxConfig.can_resolve_config_parameter (outside_port, "my_address") &&
        PaxConfig.can_resolve_config_parameter (outside_port, "next_port"))
    {
      pp = new NAT (IPAddress.Parse(PaxConfig.resolve_config_parameter (outside_port, "my_address")),
          UInt16.Parse(PaxConfig.resolve_config_parameter (outside_port, "next_port")));
    } else {
      pp = null;
    }
  }

  public void packetHandler (object sender, CaptureEventArgs e) {
    if (pp != null) {
      pp.packetHandler (sender, e);
    } else {
      throw (new Exception ("The NAT nested in NestedNAT was not initialised."));
    }
  }
}
