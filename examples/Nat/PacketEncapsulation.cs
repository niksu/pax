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
  internal abstract class PacketEncapsulation<TTransport> where TTransport : Packet
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
    /// Gets the source node of this packet (where it appears to originate from), including the transport-layer addressing information.
    /// </summary>
    /// <param name="incomingNetworkInterface">The network interface this packet arrived on.</param>
    /// <returns>A <see cref="Node"/> representing the source of the packet.</returns>
    public abstract Node<TTransport> GetSourceNode(int incomingNetworkInterface = -1);

    /// <summary>
    /// Gets the destination node of this packet, including the transport-layer addressing information.
    /// </summary>
    /// <param name="incomingNetworkInterface">The network interface this packet arrived on.</param>
    /// <returns>A <see cref="Node"/> representing the source of the packet.</returns>
    public abstract Node<TTransport> GetDestinationNode(int incomingNetworkInterface = -1);

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
  internal sealed class TcpPacketEncapsulation : PacketEncapsulation<TcpPacket>
  {
    public TcpPacketEncapsulation(Packet packet) : base(packet) { }

    public override Node<TcpPacket> GetSourceNode(int incomingNetworkInterface = -1)
    {
      return new Node<TcpPacket>(NetworkPacket.SourceAddress, new TcpPort(TransportPacket.SourcePort), incomingNetworkInterface, LinkPacket.SourceHwAddress);
    }

    public override Node<TcpPacket> GetDestinationNode(int incomingNetworkInterface = -1)
    {
      return new Node<TcpPacket>(NetworkPacket.DestinationAddress, new TcpPort(TransportPacket.DestinationPort), incomingNetworkInterface, LinkPacket.DestinationHwAddress);
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
  internal sealed class UdpPacketEncapsulation : PacketEncapsulation<UdpPacket>
  {
    public UdpPacketEncapsulation(Packet packet) : base(packet) { }

    public override Node<UdpPacket> GetSourceNode(int incomingNetworkInterface = -1)
    {
      return new Node<UdpPacket>(NetworkPacket.SourceAddress, new UdpPort(TransportPacket.SourcePort), incomingNetworkInterface, LinkPacket.SourceHwAddress);
    }

    public override Node<UdpPacket> GetDestinationNode(int incomingNetworkInterface = -1)
    {
      return new Node<UdpPacket>(NetworkPacket.DestinationAddress, new UdpPort(TransportPacket.DestinationPort), incomingNetworkInterface, LinkPacket.DestinationHwAddress);
    }

    public override void UpdateTransportChecksum()
    {
      // No checksums to update
    }

    public override bool SignalsStartOfConnection()
    {
      return true; // Any udp packet could be the start of a 'connection'
    }
  }
}

