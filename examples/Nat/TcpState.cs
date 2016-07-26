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
    /// <summary>
    /// The time for which a TCP connection should be kept open after the remote host terminates it with a Fin.
    /// </summary>
    private readonly TimeSpan TIME_WAIT;

    /// <summary>
    /// The inferred state of the TCP connection from the inside node to the outside node. Related to the TCP state on the outside node.
    /// </summary>
    private TcpDirectionalState InOutConnection = TcpDirectionalState.None;

    /// <summary>
    /// The inferred state of the TCP connection from the outside node to the inside node. Related to the TCP state on the inside node.
    /// </summary>
    private TcpDirectionalState OutInConnection = TcpDirectionalState.None;

    /// <summary>
    /// The time that the connection was closed, if applicable.
    /// </summary>
    private DateTime? CloseTime = null;

    /// <summary>
    /// True if the TCP connection from the inside to the outside is closed.
    /// </summary>
    public bool ClosedFromInside { get { return InOutConnection == TcpDirectionalState.None || InOutConnection == TcpDirectionalState.FinAck; } }

    /// <summary>
    /// True if the TCP connection from the outside to the inside is closed.
    /// </summary>
    public bool ClosedFromOutside { get { return OutInConnection == TcpDirectionalState.None || OutInConnection == TcpDirectionalState.FinAck; } }

    /// <summary>
    /// True if the connection is in the TIME_WAIT state.
    /// </summary>
    public bool InTimeWait { get { return CloseTime.HasValue && DateTime.Now - CloseTime < TIME_WAIT; } }

    /// <summary>
    /// True if the TCP connections in both directions are closed, determining if the connection can be removed before the inactivity timeout elapses.
    /// Will remain false until the TCP connections in both directions are closed and the TIME_WAIT timeout has elapsed.
    /// </summary>
    public bool CanBeClosed { get { return ClosedFromInside && ClosedFromOutside && !InTimeWait; } }

    /// <summary>
    /// Creates a new TcpState in the initial state.
    /// </summary>
    /// <param name="time_wait">The duration of the TIME_WAIT state.</param>
    public TcpState(TimeSpan time_wait)
    {
      TIME_WAIT = time_wait;
    }

    /// <summary>
    /// Updates the state of the connection to reflect the transmission of the packet. In this case (TCP),
    /// it keeps record of which TCP state it thinks the connection is in. It tracks Syns, Acks, and Fins.
    /// This is to track when the connection entry should be removed, so we don't remove it too early
    /// or leave it open indefinitely.
    /// </summary>
    /// <param name="packet">The packet being transmitted</param>
    /// <param name="packetFromInside">True if the packet originated from inside the NAT, else false.</param>
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

    // NOTE We don't check seq numbers are within a window or anything beyond the flags.
    //      This could allow a malicious FIN packet to close down the TCP connection
    //      by causing the entry to be removed from the NAT even though the end host
    //      rejects it for not being in the window.
    private void TransitionState(ref TcpDirectionalState state, bool syn, bool fin)
    {
      if (fin)
        state = TcpDirectionalState.Fin;
      else if (state == TcpDirectionalState.None && syn)
        state = TcpDirectionalState.Syn;
    }

    private void TransitionState(ref TcpDirectionalState state, bool ack)
    {
      if (ack)
      {
        if (state == TcpDirectionalState.Syn)
          state = TcpDirectionalState.SynAck;
        else if (state == TcpDirectionalState.Fin)
        {
          CloseTime = DateTime.Now;
          state = TcpDirectionalState.FinAck;
        }
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

