/*
Pax : tool support for prototyping packet processors
Jonny Shipton, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace Pax.Examples.Nat
{
  /// <summary>
  /// Represents a network node, targeted at a specific port. E.g. a TCP or UDP socket.
  /// </summary>
  public class NodeWithPort : Node, IEquatable<NodeWithPort>
  {
    /// <summary>
    /// The port number. E.g. the TCP port number.
    /// </summary>
    public ushort Port { get; }

    /// <summary>
    /// Creates new node which has a port number. E.g. a TCP socket.
    /// </summary>
    /// <param name="address">The IP address of the node.</param>
    /// <param name="port">The port number of the node. E.g. the TCP port.</param>
    /// <param name="interfaceNumber">The number of the network interface this node can be reached on.</param>
    /// <param name="macAddress">The MAC address of the node (or at least of the next hop to reach it).</param>
    public NodeWithPort(IPAddress address, ushort port, int interfaceNumber, PhysicalAddress macAddress)
      : base(address, interfaceNumber, macAddress)
    {
      Port = port;
    }

    public override bool Equals(Object other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      if (other.GetType() != GetType()) return false;
      return this.Equals(other as NodeWithPort);
    }

    public bool Equals(NodeWithPort other)
    {
      // Note that we don't compare Link addresses or interfaces
      return base.Equals((Node)other) // FIXME would be nice to have the compiler guarantee that other isn't null - Contracts?
        && Port.Equals(other.Port);
    }

    public override int GetHashCode()
    {
      return base.GetHashCode() ^ Port;
    }

    public override string ToString()
    {
      return String.Format("{0}:{1} at {2} on port {3}",
        Address.ToString(), Port.ToString(), MacAddress.ToString(), InterfaceNumber.ToString());
    }
  }
}
