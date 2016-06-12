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

using Pax;

public class Test1 : SimplePacketProcessor {
  private int count = 0;

  override public int handler (int in_port, ref Packet packet)
  {
//    Console.Write("!");
    Console.Write("{0}({1}/{2}) ", in_port, count++, PaxConfig.no_interfaces);
    return 1;
  }
}

public class Test2 : SimplePacketProcessor {

  override public int handler (int in_port, ref Packet packet)
  {
    Console.Write("?");
    return -1;
  }
}
