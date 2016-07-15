using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.NetworkInformation;
using System.Net;

namespace Pax.Examples.Nat
{
  /// <summary>
  /// A class that handles TCP packets and performs Port-Address Translation.
  /// </summary>
  public sealed class TcpNAT : NATBase<TcpPacket, NodeWithPort, TcpPacketEncapsulation>
  {
    /// <summary>
    /// The duration of the TCP TIME-WAIT TIMEOUT. See RFC 793.
    /// </summary>
    private readonly TimeSpan TIME_WAIT;

    /// <summary>
    /// The start of the range of TCP ports to use (inclusive).
    /// </summary>
    private readonly ushort StartPort;

    /// <summary>
    /// The end of the range of TCP ports to use (inclusive).
    /// </summary>
    private readonly ushort EndPort; // FIXME we don't use EndPort yet

    private ushort nextPort;
    private object nextPortLock = new object();

    /// <summary>
    /// Creates a NAT for handling TCP packets.
    /// </summary>
    /// <param name="outsideFacingAddress">The public IP address of the NAT</param>
    /// <param name="nextOutsideHopMacAddress">The MAC address of the next hop on the outside-facing port.</param>
    /// <param name="inactivityTimeout">The duration of the TCP USER TIMEOUT (inactivity timeout). See RFC 5482.</param>
    /// <param name="time_wait">The duration of the TCP TIME-WAIT TIMEOUT. See RFC 793.</param>
    /// <param name="startPort">The start of the range of TCP ports to use (inclusive).</param>
    /// <param name="endPort">The end of the range of TCP ports to use (inclusive).</param>
    public TcpNAT(IPAddress outsideFacingIPAddress, PhysicalAddress nextOutsideHopMacAddress,
      TimeSpan inactivityTimeout, TimeSpan time_wait, ushort startPort, ushort endPort)
      : base(outsideFacingIPAddress, nextOutsideHopMacAddress, inactivityTimeout)
    {
      TIME_WAIT = time_wait;
      StartPort = startPort;
      EndPort = endPort;

      nextPort = startPort;
    }

    protected override NodeWithPort CreateMasqueradeNode(IPAddress ipAddress, int interfaceNumber, PhysicalAddress macAddress)
    {
      // Get a free port
      ushort port;
      lock(nextPortLock)
      {
        port = nextPort;
        nextPort++; // FIXME naive port assignment; overflow; can reassign ports that are in use
      }

      return new NodeWithPort(ipAddress, port, interfaceNumber, macAddress);
    }

    protected override ITransportState<TcpPacket> GetInitialStateForNewConnection()
    {
      return new TcpState(TIME_WAIT);
    }
  }
}
