using System;
using PacketDotNet;

namespace Pax.Examples.Nat
{
  internal interface ITransportState<T> where T : Packet
  {
    bool CanBeClosed { get; }

    void UpdateState(T packet, bool packetFromInside);
  }

  internal sealed class NoTransportState<T> : ITransportState<T> where T : Packet
  {
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

