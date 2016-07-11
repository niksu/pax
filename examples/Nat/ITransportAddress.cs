using System;
using PacketDotNet;

namespace Pax.Examples.Nat
{
  internal interface ITransportAddress<T> : IEquatable<ITransportAddress<T>> where T : Packet
  {
    bool IsSourceOf(T packet);
    void SetAsSourceOf(T packet);
    bool IsDestinationOf(T packet);
    void SetAsDestinationOf(T packet);
    ITransportAddress<T> GetNextMasqueradingAddress(ITransportAddress<T> current);
    ITransportState<T> InitialState();
  }

  internal sealed class NoTransportAddress<T> : ITransportAddress<T>, IEquatable<NoTransportAddress<T>> where T : Packet
  {
    public static NoTransportAddress<T> Instance { get; } = new NoTransportAddress<T>();

    private NoTransportAddress() { }

    public bool IsDestinationOf(T packet)
    {
      // There is no address
      return true;
    }

    public void SetAsDestinationOf(T packet)
    {
      // There is no address to update
    }

    public bool IsSourceOf(T packet)
    {
      // There is no address
      return true;
    }

    public void SetAsSourceOf(T packet)
    {
      // There is no address to update
    }

    public override bool Equals(Object other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      if (other.GetType() != GetType()) return false;
      return this.Equals(other as NoTransportAddress<T>);
    }

    public bool Equals(NoTransportAddress<T> other)
    {
      return !ReferenceEquals(null, other);
    }

    public bool Equals(ITransportAddress<T> other)
    {
      return this.Equals(other as NoTransportAddress<T>);
    }

    public override int GetHashCode()
    {
      // Because we have no state, we want all instances of this class to be equal, so we can hardcode a hash.
      return 1;
    }
    
    public ITransportAddress<T> GetNextMasqueradingAddress(ITransportAddress<T> current)
    {
      return Instance;
    }

    public ITransportState<T> InitialState()
    {
      return NoTransportState<T>.Instance;
    }
  }

  internal class TcpPort : ITransportAddress<TcpPacket>, IEquatable<TcpPort>
  {
    public ushort Port { get; }

    public TcpPort(ushort port)
    {
      Port = port;
    }

    public bool IsSourceOf(TcpPacket packet)
    {
      // Check source port is the same
      return packet.SourcePort == Port;
    }

    public void SetAsSourceOf(TcpPacket packet)
    {
      // Rewrite source port
      packet.SourcePort = Port;
    }

    public bool IsDestinationOf(TcpPacket packet)
    {
      // Check destination port is the same
      return packet.DestinationPort == Port;
    }

    public void SetAsDestinationOf(TcpPacket packet)
    {
      // Rewrite destination port
      packet.DestinationPort = Port;
    }

    public override bool Equals(Object other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      if (other.GetType() != GetType()) return false;
      return this.Equals(other as TcpPort);
    }

    public bool Equals(TcpPort other)
    {
      return !ReferenceEquals(null, other)
        && other.Port == Port;
    }

    public bool Equals(ITransportAddress<TcpPacket> other)
    {
      return this.Equals(other as TcpPort);
    }

    public override int GetHashCode()
    {
      return new { Port }.GetHashCode();
    }

    public ITransportAddress<TcpPacket> GetNextMasqueradingAddress(ITransportAddress<TcpPacket> current)
    {
      ushort port = ((TcpPort)current).Port; // TODO: would be nice to have compile-time safety here (and in UdpPort)
      port++;

      return new TcpPort(port);
    }

    public ITransportState<TcpPacket> InitialState()
    {
      return new TcpState();
    }

    public override string ToString()
    {
      return Port.ToString();
    }
  }

  internal class UdpPort : ITransportAddress<UdpPacket>, IEquatable<UdpPort>
  {
    public ushort Port { get; }

    public UdpPort(ushort port)
    {
      Port = port;
    }

    public bool IsSourceOf(UdpPacket packet)
    {
      // Check source port is the same
      return packet.SourcePort == Port;
    }

    public void SetAsSourceOf(UdpPacket packet)
    {
      // Rewrite source port
      packet.SourcePort = Port;
    }

    public bool IsDestinationOf(UdpPacket packet)
    {
      // Check destination port is the same
      return packet.DestinationPort == Port;
    }

    public void SetAsDestinationOf(UdpPacket packet)
    {
      // Rewrite destination port
      packet.DestinationPort = Port;
    }

    public override bool Equals(Object other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      if (other.GetType() != GetType()) return false;
      return this.Equals(other as UdpPort);
    }

    public bool Equals(UdpPort other)
    {
      return !ReferenceEquals(null, other)
        && other.Port == Port;
    }

    public bool Equals(ITransportAddress<UdpPacket> other)
    {
      return this.Equals(other as UdpPort);
    }

    public override int GetHashCode()
    {
      return new { Port }.GetHashCode();
    }

    public ITransportAddress<UdpPacket> GetNextMasqueradingAddress(ITransportAddress<UdpPacket> current)
    {
      ushort port = ((UdpPort)current).Port;
      port++;

      return new UdpPort(port);
    }

    public ITransportState<UdpPacket> InitialState()
    {
      return NoTransportState<UdpPacket>.Instance;
    }

    public override string ToString()
    {
      return Port.ToString();
    }
  }
}
