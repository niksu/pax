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
  /// Represents the state of a TCP connection.
  /// </summary>
  internal class TcpState : ITransportState<TcpPacket>
  {
    private readonly TimeSpan TIME_WAIT;

    private TcpDirectionalState InOutConnection = TcpDirectionalState.None;
    private TcpDirectionalState OutInConnection = TcpDirectionalState.None;
    private DateTime? CloseTime = null;

    /// <summary>
    /// Gets a value indicating if the TCP connection from the inside to the outside is closed.
    /// </summary>
    public bool ClosedFromInside { get { return InOutConnection == TcpDirectionalState.None || InOutConnection == TcpDirectionalState.FinAck; } }

    /// <summary>
    /// Gets a value indicating if the TCP connection from the outside to the inside is closed.
    /// </summary>
    public bool ClosedFromOutside { get { return OutInConnection == TcpDirectionalState.None || OutInConnection == TcpDirectionalState.FinAck; } }
    
    public bool InTimeWait { get { return CloseTime.HasValue && DateTime.Now - CloseTime < TIME_WAIT; } }

    /// <summary>
    /// Gets a value indicating if the TCP connections in both directions are closed, determining if the connection can be removed before the timeout elapses.
    /// </summary>
    public bool CanBeClosed { get { return ClosedFromInside && ClosedFromOutside && !InTimeWait; } }

    public TcpState(TimeSpan time_wait)
    {
      TIME_WAIT = time_wait;
    }

    public void UpdateState(TcpPacket packet, bool packetFromInside)
    {
      // NOTE we don't handle RST packets because we don't want to worry about validity,
      //      e.g. is it in the window. Instead we just wait for the traffic to drop to
      //      zero and remove the entry because of lack of activity.
      
      if (packetFromInside)
      {
        TransitionState(ref InOutConnection, packet.Syn, packet.Fin);
        TransitionState(ref OutInConnection, packet.Ack);
      }
      else
      {
        TransitionState(ref OutInConnection, packet.Syn, packet.Fin);
        TransitionState(ref InOutConnection, packet.Ack);
      }
    }

    private void TransitionState(ref TcpDirectionalState state, bool syn, bool fin)
    {
      if (fin)
        state = TcpDirectionalState.Fin;
      else if (state == TcpDirectionalState.None && syn)
        state = TcpDirectionalState.Syn;
    }

    private void TransitionState(ref TcpDirectionalState state, bool ack)
    {
      if (state == TcpDirectionalState.Syn)
        state = TcpDirectionalState.SynAck;
      else if (state == TcpDirectionalState.Fin)
      {
        CloseTime = DateTime.Now;
        state = TcpDirectionalState.FinAck;
      }
    }

    /// <summary>
    /// Possible connection states from the point of view of the NAT.
    /// </summary>
    private enum TcpDirectionalState
    {
      /// <summary> This connection has no state. </summary>
      None,

      /// <summary> The connection has received a Syn, but has not replied with an Ack. </summary>
      Syn,

      /// <summary> The connection has received a Syn and replied with an Ack. </summary>
      SynAck,

      /// <summary> The connection has received a Fin, but has not replied with an Ack. </summary>
      Fin,

      /// <summary> The connection has received a Fin and replied with an Ack. </summary>
      FinAck
    }
  }
}

