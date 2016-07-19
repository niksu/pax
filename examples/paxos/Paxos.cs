/*
Porting the P4 implementation of Paxos as described in the "Paxos made Switch-y"
paper by Huynh Tu Dang, Marco Canini, Fernando Pedone, and Robert Soule.

Nik Sultana, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using PacketDotNet;
using Pax;

public static class Paxos {
  public static readonly ushort Paxos_Acceptor_Port = 0x8889;
  public static readonly ushort Paxos_Coordinator_Port = 0x8888;
}

// Paxos Coordinator.
// It operates on Paxos packets arriving on in_port. Both Paxos and other
// traffic is forward on to inport+1.
public class Coordinator : SimplePacketProcessor {
  // State maintained by the Coordinator.
  ushort instance_register = 0;

  override public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    if (packet is EthernetPacket)
    {
      if (packet.Encapsulates(typeof(IPv4Packet), typeof(UdpPacket), typeof(Paxos_Packet)))
      {
        IpPacket ip_p = ((IpPacket)(packet.PayloadPacket));
        UdpPacket udp_p = ((UdpPacket)(ip_p.PayloadPacket));
        // FIXME should we check if (udp_p.DestinationPort == Paxos.Paxos_Coordinator_Port)?
        Paxos_Packet paxos_p = ((Paxos_Packet)(udp_p.PayloadPacket));

        instance_register++; // FIXME use atomic increment
        paxos_p.Instance = instance_register;
        udp_p.DestinationPort = Paxos.Paxos_Acceptor_Port;
        //udp_p.UpdateUDPChecksum();
        udp_p.Checksum = 0; // FIXME this follows the P4 implementation, but I'm not yet sure why the UDP checksum is being invalidated.
      }
    }

    // We assume that we receive from in_port and send to in_port+1.
    // NOTE that we implicitly forward all non-Paxos packets onwards,
    //      unmmodified. The downstream processor then decides how to forward
    //      them along the network.
    return (new ForwardingDecision.SinglePortForward(in_port + 1));
  }
}

public class Acceptor : SimplePacketProcessor {
  override public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    //TODO
    return null;
  }
}
