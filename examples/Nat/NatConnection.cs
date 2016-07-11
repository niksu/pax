using System;
using PacketDotNet;

namespace Pax.Examples.Nat
{
  internal sealed class NatConnection<T> where T : Packet
  {
    public Node<T> InsideNode { get; }
    public Node<T> OutsideNode { get; }
    public Node<T> NatNode { get; }
    public ITransportState<T> State { get; }
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
