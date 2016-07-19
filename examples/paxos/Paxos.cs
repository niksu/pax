/*
Porting the P4 implementation of Paxos as described in the "Paxos made Switch-y"
paper by Huynh Tu Dang, Marco Canini, Fernando Pedone, and Robert Soule.

Nik Sultana, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using PacketDotNet;
using Pax;

using round_size_t = System.UInt16;
using datapath_size_t = System.UInt16;
using value_size_t = Value_Type;


enum Phase {Paxos_1A = 0, Paxos_1B, Paxos_2A, Paxos_2B};

public static class Paxos {
  public static readonly ushort Paxos_Acceptor_Port = 0x8889;
  public static readonly ushort Paxos_Coordinator_Port = 0x8888;
  public static readonly int Instance_Count = 65536;
  public static ushort Learner_Port = 0; //The port on which the Learner listens.

  private const int default_port = 0; //Pax port that we'd expect to be involved in this packet processor.
  static Paxos() {
    if (PaxConfig.can_resolve_config_parameter(default_port, "learner_port"))
    {
      Learner_Port = UInt16.Parse(PaxConfig.resolve_config_parameter(default_port, "learner_port"));
    } else {
      throw (new Exception ("Undefined parameter: learner_port"));
    }
  }
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
        udp_p.Checksum = 0; // NOTE this follows the P4 implementation, but normally
                            //      i'd use "udp_p.UpdateUDPChecksum();"
      }
    }

    // We assume that we receive from in_port and send to in_port+1.
    // NOTE that we implicitly forward all non-Paxos packets onwards,
    //      unmmodified. The downstream processor then decides how to forward
    //      them along the network.
    return (new ForwardingDecision.SinglePortForward(in_port + 1));
  }
}

public class ingress_metadata_t {
  public round_size_t round = 0;
  public bool set_drop = false;
}

public class Acceptor : SimplePacketProcessor {
  private datapath_size_t datapath_id = 0;
  private round_size_t[] rounds_register = new round_size_t[Paxos.Instance_Count];
  private round_size_t[] vrounds_register = new round_size_t[Paxos.Instance_Count];
  private value_size_t[] value_register = new value_size_t[Paxos.Instance_Count];

  private void read_round (out ingress_metadata_t local_metadata, Paxos_Packet paxos_p)
  {
    local_metadata = new ingress_metadata_t();
    local_metadata.round = rounds_register[paxos_p.Instance];
    local_metadata.set_drop = true;
  }

  private void acceptor (ref UdpPacket udp_p, ref Paxos_Packet paxos_p)
  {
    switch (paxos_p.MsgType) {
      case ((ushort)Phase.Paxos_1A):
        paxos_p.MsgType = (ushort)Phase.Paxos_1B;
        paxos_p.Voted_Round = vrounds_register[paxos_p.Instance];
        paxos_p.Value = value_register[paxos_p.Instance];
        paxos_p.Accept_ID = datapath_id;
        rounds_register[paxos_p.Instance] = paxos_p.Voted_Round;
        udp_p.DestinationPort = Paxos.Learner_Port;
        udp_p.Checksum = 0; // NOTE As above, following the P4 implementation.
        break;

      case ((ushort)Phase.Paxos_2A):
        paxos_p.MsgType = (ushort)Phase.Paxos_2B;
        rounds_register[paxos_p.Instance] = paxos_p.Round;
        vrounds_register[paxos_p.Instance] = paxos_p.Round;
        value_register[paxos_p.Instance] = paxos_p.Value;
        paxos_p.Accept_ID = datapath_id;
        udp_p.DestinationPort = Paxos.Learner_Port;
        udp_p.Checksum = 0; // NOTE As above (above), following the P4 implementation.
        break;

      default:
        throw (new Exception ("Unknown Paxos phase"));
        break;
    }
  }

  override public ForwardingDecision process_packet (int in_port, ref Packet packet)
  {
    //Check if the packet is of the form we're interested in.
    if (packet is EthernetPacket)
    {
      if (packet.Encapsulates(typeof(IPv4Packet), typeof(UdpPacket), typeof(Paxos_Packet)))
      {
        IpPacket ip_p = ((IpPacket)(packet.PayloadPacket));
        UdpPacket udp_p = ((UdpPacket)(ip_p.PayloadPacket));
        // FIXME should we check if (udp_p.DestinationPort == Paxos.Paxos_Acceptor_Port)?
        Paxos_Packet paxos_p = ((Paxos_Packet)(udp_p.PayloadPacket));

        ingress_metadata_t local_metadata;
        read_round(out local_metadata, paxos_p);
        if (local_metadata.round <= paxos_p.Round) {
          acceptor(ref udp_p, ref paxos_p);
        }
      }
    }

    // We follow the same forwarding policy as the Coordinator.
    return (new ForwardingDecision.SinglePortForward(in_port + 1));
  }
}
