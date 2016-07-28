/*
Pax : tool support for prototyping packet processors
Jonny Shipton, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net;
using System.Net.NetworkInformation;
using PacketDotNet;

namespace Pax.Examples.Nat
{
  /// <summary>
  /// A class representing a node on the network with an IP address.
  /// It also stores a network interface number and MAC address for the node.
  /// </summary>
  /// <remarks>
  /// It is important to note that it is intended for subclasses to add additional addressing information that will distinguish
  /// two Node objects that represent the same actual node on the network, but represent for example two different TCP sockets.
  /// </remarks>
  public class Node : IEquatable<Node>
  {
    /// <summary>
    /// Gets the IP address of the node.
    /// </summary>
    public IPAddress Address { get; }

    /// <summary>
    /// Gets the number of the network interface this node is connected to.
    /// </summary>
    public int InterfaceNumber { get; } // FIXME would it be better to save the ForwardingDecision object?

    /// <summary>
    /// Gets the MAC address of the node, or at least of the next hop to reach that node.
    /// </summary>
    public PhysicalAddress MacAddress { get; }

    private readonly int hashCode;

    /// <summary>
    /// Creates new node.
    /// </summary>
    /// <param name="address">The IP address of the node.</param>
    /// <param name="interfaceNumber">The number of the network interface this node can be reached on.</param>
    /// <param name="macAddress">The MAC address of the node (or at least of the next hop to reach it).</param>
    public Node(IPAddress address, int interfaceNumber, PhysicalAddress macAddress)
    {
      if (ReferenceEquals(null, address)) throw new ArgumentNullException(nameof(address));
      if (ReferenceEquals(null, macAddress)) throw new ArgumentNullException(nameof(macAddress));

      Address = address;
      InterfaceNumber = interfaceNumber;
      MacAddress = macAddress;

      // Pre-compute the hash code
      hashCode = new { Address }.GetHashCode(); // FIXME test to see if the hash code of this object is used
    }

    public override bool Equals(Object other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      if (other.GetType() != GetType()) return false;
      return this.Equals(other as Node);
    }

    public bool Equals(Node other)
    {
     // Note that we don't compare Link addresses or interfaces
     return !ReferenceEquals(null, other)
       && Address.Equals(other.Address);
    }

    public override int GetHashCode() { return hashCode; }

    public override string ToString()
    {
      return String.Format("{0} at {1} on port {2}",
        Address.ToString(), MacAddress.ToString(), InterfaceNumber.ToString());
    }
  }
}
