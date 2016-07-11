using System;
using PacketDotNet;

namespace Pax.Examples.Nat
{
  internal class TcpState : ITransportState<TcpPacket>
  {
    private TcpDirectionalState InOutConnection = TcpDirectionalState.None;
    private TcpDirectionalState OutInConnection = TcpDirectionalState.None;
    public bool ClosedFromInside { get { return InOutConnection == TcpDirectionalState.None || InOutConnection == TcpDirectionalState.FinAck; } }
    public bool ClosedFromOutside { get { return OutInConnection == TcpDirectionalState.None || OutInConnection == TcpDirectionalState.FinAck; } }
    public bool CanBeClosed { get { return ClosedFromInside && ClosedFromOutside; } }

    public void UpdateState(TcpPacket packet, bool packetFromInside)
    {
      if (packetFromInside)
      {
        InOutConnection = TransitionState(InOutConnection, packet.Syn, packet.Fin);
        OutInConnection = TransitionState(OutInConnection, packet.Ack);
      }
      else
      {
        OutInConnection = TransitionState(OutInConnection, packet.Syn, packet.Fin);
        InOutConnection = TransitionState(InOutConnection, packet.Ack);
      }
    }

    private static TcpDirectionalState TransitionState(TcpDirectionalState state, bool syn, bool fin)
    {
      if (fin)
        return TcpDirectionalState.Fin;
      else if (state == TcpDirectionalState.None && syn)
        return TcpDirectionalState.Syn;
      else
        return state;
    }

    private static TcpDirectionalState TransitionState(TcpDirectionalState state, bool ack)
    {
      if (state == TcpDirectionalState.Syn)
        return TcpDirectionalState.SynAck;
      else if (state == TcpDirectionalState.Fin)
        return TcpDirectionalState.FinAck;
      else
        return state;
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

