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
  /// Provides a typed interface for the current state of a Transport-layer protocol.
  /// </summary>
  /// <typeparam name="T">The type of packet</typeparam>
  internal interface ITransportState<T> where T : Packet
  {
    /// <summary>
    /// Gets a boolean value determining if the connection can be removed before the timeout elapses.
    /// </summary>
    bool CanBeClosed { get; }

    /// <summary>
    /// Updates the state of the connection to reflect the transmission of the packet.
    /// </summary>
    /// <param name="packet">The packet being transmitted</param>
    /// <param name="packetFromInside">True if the packet originated from inside the NAT, else false.</param>
    void UpdateState(T packet, bool packetFromInside);
  }

  internal sealed class NoTransportState<T> : ITransportState<T> where T : Packet
  {
    /// <summary>
    /// A value indicating that there is no connection state for this type of packet.
    /// </summary>
    public static NoTransportState<T> Instance = new NoTransportState<T>();

    private NoTransportState() { }

    public bool CanBeClosed
    {
      get
      {
        // There is no state to let us know we can close before timeout.
        return false;
      }
    }

    public void UpdateState(T packet, bool packetFromInside)
    {
      // No state to update
    }
  }
}

