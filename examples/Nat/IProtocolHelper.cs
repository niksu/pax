using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Pax.Examples.Nat
{
  internal interface IProtocolHelper<T> where T : Packet
  {
    /// <summary>
    /// Gets an initial state for this Transport-layer protocol.
    /// </summary>
    /// <returns>An initial state.</returns>
    ITransportState<T> InitialState();
  }

  internal sealed class GenericTransportHelper<T> : IProtocolHelper<T> where T : Packet
  {
    public ITransportState<T> InitialState()
    {
      return NoTransportState<T>.Instance;
    }
  }

  internal sealed class TcpHelper : IProtocolHelper<TcpPacket>
  {
    public readonly TimeSpan TIME_WAIT;

    public TcpHelper(TimeSpan? time_wait = null)
    {
      TIME_WAIT = time_wait ?? TimeSpan.FromMinutes(4);
    }

    public ITransportState<TcpPacket> InitialState()
    {
      return new TcpState(TIME_WAIT);
    }
  }
}
