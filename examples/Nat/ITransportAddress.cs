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
  /// Provides typed access to Transport layer addresses, such as TCP ports.
  /// </summary>
  /// <typeparam name="T">The type of packet.</typeparam>
  internal interface ITransportAddress<T> : IEquatable<ITransportAddress<T>> where T : Packet
  {
    /// <summary>
    /// Determines if the packet claims to originate from this Transport-layer address.
    /// </summary>
    /// <param name="packet">The packet to check</param>
    /// <returns>True if the packet claims to come from this address.</returns>
    bool IsSourceOf(T packet);

    /// <summary>
    /// Rewrites the Transport-layer source address of the packet to this address.
    /// </summary>
    /// <param name="packet">The packet to rewrite</param>
    void SetAsSourceOf(T packet);

    /// <summary>
    /// Determines if the packet is intended for this Transport-layer address.
    /// </summary>
    /// <param name="packet">The packet to check</param>
    /// <returns>True if the packet is intended for this address.</returns>
    bool IsDestinationOf(T packet);

    /// <summary>
    /// Rewrites the Transport-layer destination address of the packet to this address.
    /// </summary>
    /// <param name="packet">The packet to rewrite</param>
    void SetAsDestinationOf(T packet);

    /// <summary>
    /// Gets the next potentially free Transport-layer address for use as the masqueraded address by the NAT.
    /// </summary>
    /// <param name="current">The current address</param>
    /// <returns>A potential address for use by the NAT.</returns>
    ITransportAddress<T> GetNextMasqueradingAddress(ITransportAddress<T> current);
  }

  internal sealed class NoTransportAddress<T> : ITransportAddress<T>, IEquatable<NoTransportAddress<T>> where T : Packet
  {
    /// <summary>
    /// A value indicating that there is no Transport-layer addressing for this type of packet.
    /// </summary>
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
  }

  /// <summary>
  /// A class representing the addressing of TCP: TCP ports.
  /// </summary>
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

    public override string ToString()
    {
      return Port.ToString();
    }
  }

  /// <summary>
  /// A class representing the addressing of UDP: UDP ports.
  /// </summary>
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
