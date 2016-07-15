/*
Pax : tool support for prototyping packet processors
Jonny Shipton, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using PacketDotNet;

namespace Pax.Examples.Nat
{
  /// <summary>
  /// Class that provides strongly typed access to packets and their encapsulated packets.
  /// </summary>
  /// <typeparam name="TTransport">The type of the transport-layer packet</typeparam>
  /// <typeparam name="TNode">The type of node.</typeparam>
  public abstract class PacketEncapsulation<TTransport,TNode> where TTransport : Packet where TNode : Node
  {
    /// <summary>
    /// The link-layer packet.
    /// </summary>
    public EthernetPacket LinkPacket { get; }

    /// <summary>
    /// The network-layer packet.
    /// </summary>
    public IpPacket NetworkPacket { get; }

    /// <summary>
    /// The transport-layer packet.
    /// </summary>
    public TTransport TransportPacket { get; }

    /// <summary>
    /// Casts the packet and it's payload packets to the correct types.
    /// </summary>
    /// <param name="packet"></param>
    public PacketEncapsulation(Packet packet)
    {
      LinkPacket = (EthernetPacket)packet;
      NetworkPacket = (IpPacket)LinkPacket.PayloadPacket;
      TransportPacket = (TTransport)NetworkPacket.PayloadPacket;
    }

    /// <summary>
    /// Updates the checksums of the packets for each layer.
    /// </summary>
    public void UpdateChecksums()
    {
      // Update transport checksum
      UpdateTransportChecksum();

      // Update IPv4 checksum
      if (NetworkPacket is IPv4Packet)
        ((IPv4Packet)NetworkPacket).UpdateIPChecksum();
    }

    /// <summary>
    /// Rewrites the packet's source.
    /// </summary>
    /// <param name="node">The source node.</param>
    public void SetSource(TNode node)
    {
      // Rewrite the MAC address
      LinkPacket.SourceHwAddress = node.MacAddress;

      // Rewrite the source IP address
      NetworkPacket.SourceAddress = node.Address;

      // Allow subclasses to set the transport layer protocol values
      OnSetSource(node);
    }

    /// <summary>
    /// Rewrites the packet's destination.
    /// </summary>
    /// <param name="node">The destination node.</param>
    public void SetDestination(TNode node)
    {
      // Rewrite the MAC address
      LinkPacket.DestinationHwAddress = node.MacAddress;

      // Rewrite the destination IP address
      NetworkPacket.DestinationAddress = node.Address;

      // Allow subclasses to set the transport layer protocol values
      OnSetDestination(node);
    }

    /// <summary>
    /// Gets the source node of this packet (where it appears to originate from).
    /// </summary>
    /// <param name="incomingNetworkInterface">The network interface this packet arrived on.</param>
    /// <returns>A <see cref="TNode"/> representing the source of the packet.</returns>
    public abstract TNode GetSourceNode(int incomingNetworkInterface = -1);

    /// <summary>
    /// Gets the destination node of this packet.
    /// </summary>
    /// <param name="incomingNetworkInterface">The network interface this packet arrived on.</param>
    /// <returns>A <see cref="TNode"/> representing the source of the packet.</returns>
    public abstract TNode GetDestinationNode(int incomingNetworkInterface = -1);

    /// <summary>
    /// This method is called whenever the SetSource method is called.
    /// Override this method to update the protocol specific source values of the packet.
    /// </summary>
    /// <param name="node">The source node.</param>
    protected abstract void OnSetSource(TNode node); // FIXME better name?

    /// <summary>
    /// This method is called whenever the SetDestination method is called.
    /// Override this method to update the protocol specific destination values of the packet.
    /// </summary>
    /// <param name="node">The destination node.</param>
    protected abstract void OnSetDestination(TNode node);

    /// <summary>
    /// Updates the checksum of the transport-layer packet.
    /// </summary>
    public abstract void UpdateTransportChecksum();

    /// <summary>
    /// Gets a value indicating if the packet could signal the start of a connection. E.g. TCP Syn packet.
    /// </summary>
    /// <returns>True if the packet could signal the start of a connection.</returns>
    public abstract bool SignalsStartOfConnection();
  }

  /// <summary>
  /// Provides strongly typed access to encapsulated TCP packets.
  /// </summary>
  public sealed class TcpPacketEncapsulation : PacketEncapsulation<TcpPacket,NodeWithPort>
  {
    public TcpPacketEncapsulation(Packet packet) : base(packet) { }

    public override NodeWithPort GetSourceNode(int incomingNetworkInterface = -1)
    {
      return new NodeWithPort(NetworkPacket.SourceAddress, TransportPacket.SourcePort, incomingNetworkInterface, LinkPacket.SourceHwAddress);
    }

    public override NodeWithPort GetDestinationNode(int incomingNetworkInterface = -1)
    {
      return new NodeWithPort(NetworkPacket.DestinationAddress, TransportPacket.DestinationPort, incomingNetworkInterface, LinkPacket.DestinationHwAddress);
    }

    protected override void OnSetSource(NodeWithPort node)
    {
      // Set the source port
      TransportPacket.SourcePort = node.Port;
    }

    protected override void OnSetDestination(NodeWithPort node)
    {
      // Set the destination port
      TransportPacket.DestinationPort = node.Port;
    }

    public override void UpdateTransportChecksum()
    {
      // Update TCP checksum
      TransportPacket.UpdateTCPChecksum();
    }

    public override bool SignalsStartOfConnection()
    {
      return TransportPacket.Syn; // Only Syn packets start connections
    }
  }

  /// <summary>
  /// Provides strongly typed access to encapsulated UDP packets.
  /// </summary>
  public sealed class UdpPacketEncapsulation : PacketEncapsulation<UdpPacket,NodeWithPort>
  {
    public UdpPacketEncapsulation(Packet packet) : base(packet) { }

    public override NodeWithPort GetSourceNode(int incomingNetworkInterface = -1)
    {
      return new NodeWithPort(NetworkPacket.SourceAddress, TransportPacket.SourcePort, incomingNetworkInterface, LinkPacket.SourceHwAddress);
    }

    public override NodeWithPort GetDestinationNode(int incomingNetworkInterface = -1)
    {
      return new NodeWithPort(NetworkPacket.DestinationAddress, TransportPacket.DestinationPort, incomingNetworkInterface, LinkPacket.DestinationHwAddress);
    }

    protected override void OnSetSource(NodeWithPort node)
    {
      // Set the source port
      TransportPacket.SourcePort = node.Port;
    }

    protected override void OnSetDestination(NodeWithPort node)
    {
      // Set the destination port
      TransportPacket.DestinationPort = node.Port;
    }

    public override void UpdateTransportChecksum()
    {
      TransportPacket.UpdateUDPChecksum();
    }

    public override bool SignalsStartOfConnection()
    {
      // Any UDP packet could be the start of a 'connection'
      return true;
    }
  }
}

