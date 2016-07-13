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
  /// A class representing a node on the network with an IP address and transport-layer address. It also stores a network interface number and MAC address for the node.
  /// </summary>
  /// <typeparam name="T">The type of packet</typeparam>
  internal sealed class Node<T> : IEquatable<Node<T>> where T : Packet
  {
    /// <summary>
    /// Gets the IP address of the node.
    /// </summary>
    public IPAddress Address { get; } // FIXME we should really use immutable values since we are using a dictionary

    /// <summary>
    /// Gets the transport-layer address of the node.
    /// </summary>
    public ITransportAddress<T> TransportAddress { get; }

    /// <summary>
    /// Gets the number of the network interface this node is connected to.
    /// </summary>
    public int InterfaceNumber { get; }

    /// <summary>
    /// Gets the MAC address of the node, or at least of the next hop to reach that node.
    /// </summary>
    public PhysicalAddress MacAddress { get; }

  	private readonly int hashCode;
#if DEBUG
  	private string asString;
#endif

    public Node(IPAddress address, ITransportAddress<T> transportAddress, int interfaceNumber, PhysicalAddress macAddress)
  	{
      if (ReferenceEquals(null, address)) throw new ArgumentNullException(nameof(address));
      if (ReferenceEquals(null, transportAddress)) throw new ArgumentNullException(nameof(transportAddress));
      if (ReferenceEquals(null, macAddress)) throw new ArgumentNullException(nameof(macAddress));

  		Address = address;
      TransportAddress = transportAddress;
  		InterfaceNumber = interfaceNumber;
  		MacAddress = macAddress;
  		// FIXME computing hash at construction relies on address being immutable (addr.Scope can change)
  		hashCode = new { Address, TransportAddress }.GetHashCode();
#if DEBUG
  		asString = this.ToString();
#endif
  	}

    /// <summary>
    /// Rewrites the packet to appear to originate from this node.
    /// </summary>
    /// <param name="packet">The packet to rewrite</param>
    public void RewritePacketSource(PacketEncapsulation<T> packet)
    {
      // Rewrite the MAC address
      packet.LinkPacket.SourceHwAddress = MacAddress;

      // Rewrite the source IP address
      packet.NetworkPacket.SourceAddress = Address;

      // Rewrite protocol values
      TransportAddress.SetAsSourceOf(packet.TransportPacket);
    }

    /// <summary>
    /// Rewrites the packet so that the destination is this node.
    /// </summary>
    /// <param name="packet"></param>
    public void RewritePacketDestination(PacketEncapsulation<T> packet)
    {
      // Rewrite the MAC address
      packet.LinkPacket.DestinationHwAddress = MacAddress;

      // Rewrite the destination IP address
      packet.NetworkPacket.DestinationAddress = Address;

      // Rewrite protocol values
      TransportAddress.SetAsDestinationOf(packet.TransportPacket);
    }

  	public override bool Equals(Object other)
  	{
  		if (ReferenceEquals(null, other)) return false;
  		if (ReferenceEquals(this, other)) return true;
  		if (other.GetType() != GetType()) return false;
      return this.Equals(other as Node<T>);
  	}

    public bool Equals(Node<T> other)
  	{
      // Note that we don't compare Link addresses
      return !ReferenceEquals(null, other)
        && Address.Equals(other.Address)
        && TransportAddress.Equals(other.TransportAddress);
  	}

  	public override int GetHashCode() { return hashCode; }

  	public override string ToString()
  	{
#if DEBUG
  		if (asString != null)
  			return asString;
  		else
#endif
  			return String.Format("{0}:{1} at {2} on port {3}",
          Address.ToString(), TransportAddress.ToString(), MacAddress.ToString(), InterfaceNumber.ToString());
  	}
  }
}
