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
using System.Threading;

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
