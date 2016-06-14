/*
Pax : tool support for prototyping packet processors
Nik Sultana, Cambridge University Computer Lab, June 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using PacketDotNet;
using System.Diagnostics;
using Pax;

// We follow a mapping of ports (stored as an array) to map packets between ports.
// I expect Mirror to be chained to other network elements, such as a switch.
public class Mirror : SimplePacketProcessor {
  bool instantiated = false;
  int[] mirror;

  public Mirror () {
    // NOTE Mirror needs to be instantiated by being given a mirror configuration.
    //      With the default constructor's initialisation, we'll only throw an exception if
    //      the mirror is used.
    instantiated = false;
  }

  public Mirror (int[] mirror) {
    instantiated = true;
    Debug.Assert(mirror.Length == PaxConfig.no_interfaces);
    this.mirror = mirror;
  }

  override public int handler (int in_port, ref Packet packet)
  {
    if (!instantiated)
    {
      throw new Exception("Mirror has not been instantiated properly");
    }

    return mirror[in_port];
  }

  public static int[] InitialConfig (int size)
  {
    int[] cfg = new int[size];

    for (int i = 0; i < cfg.Length; i++)
    {
      // The default configuration is for the mirror to do nothing.
      // This won't get in the way of chained elements transforming or forwarding the packet.
      cfg[i] = -1;
    }

    return cfg;
  }
}

/* FIXME this should not be instantiated by Pax -- one idea is to put it into a
         separate assembly and use it in Examples, or have Pax differentiate between
         different sorts of classes.
public static class MirrorExtension {

  public static int[] MirrorPort (this int[] cfg, int from, int to)
  {
    cfg[from] = to;
    return cfg;
  }
}
*/
