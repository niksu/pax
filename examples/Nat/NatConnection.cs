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
  /// A class representing a connection through the NAT.
  /// </summary>
  /// <typeparam name="TPacket">The type of packet</typeparam>
  /// <typeparam name="TNode">The type of node</typeparam>
  internal sealed class NatConnection<TPacket,TNode> where TPacket : Packet where TNode : Node
  {
    /// <summary>
    /// The node inside the NAT.
    /// </summary>
    public TNode InsideNode { get; }

    /// <summary>
    /// The node outside the NAT.
    /// </summary>
    public TNode OutsideNode { get; }

    /// <summary>
    /// The node which the connection appears to originate from to the outside node.
    /// </summary>
    public TNode NatNode { get; }

    /// <summary>
    /// The current Transport-layer state of the connection.
    /// </summary>
    public ITransportState<TPacket> State { get; }

    /// <summary>
    /// The last time that a packet for this connection was observed.
    /// </summary>
    public DateTime LastUsed { get; private set; }
    private readonly object LastUsed_Lock = new object();

    public NatConnection(TNode insideNode, TNode outsideNode, TNode natNode, ITransportState<TPacket> initialState)
    {
      if (Object.ReferenceEquals(null, insideNode)) throw new ArgumentNullException(nameof(insideNode));
      if (Object.ReferenceEquals(null, outsideNode)) throw new ArgumentNullException(nameof(outsideNode));
      if (Object.ReferenceEquals(null, natNode)) throw new ArgumentNullException(nameof(natNode));
      if (Object.ReferenceEquals(null, initialState)) throw new ArgumentNullException(nameof(initialState));

      InsideNode = insideNode;
      OutsideNode = outsideNode;
      NatNode = natNode;
      State = initialState;
      LastUsed = DateTime.Now;
    }

    /// <summary>
    /// Notify this object that the NAT has observed a packet for this connection.
    /// </summary>
    /// <param name="packet">The observed packet.</param>
    /// <param name="packetFromInside">True if the packet originated from a node inside the NAT, else false.</param>
    public void ReceivedPacket(PacketEncapsulation<TPacket,TNode> packet, bool packetFromInside)
    {
      // Mark used
      DateTime used = DateTime.Now;
      lock (LastUsed_Lock)
      {
        // Try to update the LastUsed value, keeping the latest value
        if (LastUsed < used)
          LastUsed = used;
      }

      // Update connection state
      State.UpdateState(packet.TransportPacket, packetFromInside);
    }
  }
}
