using System;
using PacketDotNet;

namespace Pax.Examples.Nat
{
  /// <summary>
  /// A class representing a connection through the NAT.
  /// </summary>
  /// <typeparam name="T">The type of packet</typeparam>
  internal sealed class NatConnection<T> where T : Packet
  {
    /// <summary>
    /// The node inside the NAT.
    /// </summary>
    public Node<T> InsideNode { get; }

    /// <summary>
    /// The node outside the NAT.
    /// </summary>
    public Node<T> OutsideNode { get; }

    /// <summary>
    /// The node which the connection appears to originate from to the outside node.
    /// </summary>
    public Node<T> NatNode { get; }

    /// <summary>
    /// The current Transport-layer state of the connection.
    /// </summary>
    public ITransportState<T> State { get; }

    /// <summary>
    /// The last time that a packet for this connection was observed.
    /// </summary>
    public DateTime LastUsed { get; private set; }
    private readonly object LastUsed_Lock = new object();

    public NatConnection(Node<T> insideNode, Node<T> outsideNode, Node<T> natNode)
    {
      if (Object.ReferenceEquals(null, insideNode)) throw new ArgumentNullException(nameof(insideNode));
      if (Object.ReferenceEquals(null, outsideNode)) throw new ArgumentNullException(nameof(outsideNode));
      if (Object.ReferenceEquals(null, natNode)) throw new ArgumentNullException(nameof(natNode));
      if (insideNode.GetType() != outsideNode.GetType()) throw new ArgumentException("Both nodes must have the same type");

      InsideNode = insideNode;
      OutsideNode = outsideNode;
      NatNode = natNode;
      State = insideNode.TransportAddress.InitialState();
      LastUsed = DateTime.Now;
    }

    /// <summary>
    /// Notify this object that the NAT has observed a packet for this connection.
    /// </summary>
    /// <param name="packet">The observed packet.</param>
    /// <param name="packetFromInside">True if the packet originated from a node inside the NAT, else false.</param>
    public void ReceivedPacket(PacketEncapsulation<T> packet, bool packetFromInside)
    {
      // Mark used
      DateTime used = DateTime.Now;
      lock (LastUsed_Lock)
      {
        // Try to update the LastUsed value, keeping the latest value
        if (LastUsed < used)
          LastUsed = used;
      }

      // Update state
      State.UpdateState(packet.TransportPacket, packetFromInside);
    }
  }
}
