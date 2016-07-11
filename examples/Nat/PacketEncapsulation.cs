using System;
using PacketDotNet;

namespace Pax.Examples.Nat
{
  /// <summary>
  /// Class that strongly types access to packets and their encapsulated packets.
  /// </summary>
  internal abstract class PacketEncapsulation<TTransport> where TTransport : Packet
  {
    public EthernetPacket LinkPacket { get; }
    public IpPacket NetworkPacket { get; }
    public TTransport TransportPacket { get; }

    public PacketEncapsulation(Packet packet)
    {
      LinkPacket = (EthernetPacket)packet;
      NetworkPacket = (IpPacket)LinkPacket.PayloadPacket;
      TransportPacket = (TTransport)NetworkPacket.PayloadPacket;
    }

    public void UpdateChecksums()
    {
      // Update transport checksum
      UpdateTransportChecksum();

      // Update IPv4 checksum
      if (NetworkPacket is IPv4Packet)
        ((IPv4Packet)NetworkPacket).UpdateIPChecksum();
    }

    public abstract Node<TTransport> GetSourceNode(int incomingPort = -1);

    public abstract Node<TTransport> GetDestinationNode(int incomingPort = -1);

    public abstract void UpdateTransportChecksum();

    public abstract bool SignalsStartOfConnection();
  }

  internal sealed class TcpPacketEncapsulation : PacketEncapsulation<TcpPacket>
  {
    public TcpPacketEncapsulation(Packet packet) : base(packet) { }

    public override Node<TcpPacket> GetSourceNode(int incoming_port = -1)
    {
      return new Node<TcpPacket>(NetworkPacket.SourceAddress, new TcpPort(TransportPacket.SourcePort), incoming_port, LinkPacket.SourceHwAddress);
    }

    public override Node<TcpPacket> GetDestinationNode(int incoming_port = -1)
    {
      return new Node<TcpPacket>(NetworkPacket.DestinationAddress, new TcpPort(TransportPacket.DestinationPort), incoming_port, LinkPacket.DestinationHwAddress);
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

  internal sealed class UdpPacketEncapsulation : PacketEncapsulation<UdpPacket>
  {
    public UdpPacketEncapsulation(Packet packet) : base(packet) { }

    public override Node<UdpPacket> GetSourceNode(int incoming_port = -1)
    {
      return new Node<UdpPacket>(NetworkPacket.SourceAddress, new UdpPort(TransportPacket.SourcePort), incoming_port, LinkPacket.SourceHwAddress);
    }

    public override Node<UdpPacket> GetDestinationNode(int incoming_port = -1)
    {
      return new Node<UdpPacket>(NetworkPacket.DestinationAddress, new UdpPort(TransportPacket.DestinationPort), incoming_port, LinkPacket.DestinationHwAddress);
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

