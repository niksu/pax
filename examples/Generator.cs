/*
Primitive packet-generator example.
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using System.Net.NetworkInformation;
using PacketDotNet;
using SharpPcap;
using System.Diagnostics;
using System.Threading;
using System.Net;

using Pax;


public class Generator : IActive, IPacketProcessor {
  ICaptureDevice device = null; // NOTE if this is made into an array, we can model multipath behaviour?
  Timer timer;

  ushort src_port;
  ushort dst_port;
  IPAddress src_ip;
  IPAddress dst_ip;
  PhysicalAddress src_mac;
  PhysicalAddress dst_mac;
  int interval;

  public Generator (int interval, UInt16 src_port, UInt16 dst_port, IPAddress src_ip,
   IPAddress dst_ip, PhysicalAddress src_mac, PhysicalAddress dst_mac) {
    this.interval = interval;
    this.src_port = src_port;
    this.dst_port = dst_port;
    this.src_ip = src_ip;
    this.dst_ip = dst_ip;
    this.src_mac = src_mac;
    this.dst_mac = dst_mac;
  }

  public void packetHandler (object sender, CaptureEventArgs e) {
    // FIXME how to indicate if we don't want to register a handler?
  }
  public ForwardingDecision process_packet (int in_port, ref Packet packet) {
    // FIXME how to indicate if we don't want to register a handler?
    return null;
  }

  private Packet GeneratePacket() {
    // FIXME in this case we always generate the same packet, but keep
    //       regenerating it -- this can be made more efficient!
    var tcp_p = new TcpPacket(src_port, dst_port);
    var ip_p = new IPv4Packet(src_ip, dst_ip);
    var eth_p = new EthernetPacket(src_mac, dst_mac, EthernetPacketType.None);
    eth_p.PayloadPacket = ip_p;
    ip_p.PayloadPacket = tcp_p;
    return eth_p;
  }

  public void PreStart (ICaptureDevice device) {
    Debug.Assert(this.device == null);
    this.device = device;
    Console.WriteLine ("Generator configured");
  }

  public void Stop () {
    Console.WriteLine ();
    // NOTE we'd use something like "lock (this) {active = false;}" if we used a
    //      running "while" rather than timers.
    if (timer != null) {timer.Dispose();}
    Console.WriteLine ("Generator stopped");
  }

  public void Start () {
    Console.WriteLine ("Generator starting");
    timer = new Timer(Pulse, null, 0, interval);
  }

  public void Pulse (Object o) {
    Console.Write (".");
    device.SendPacket(GeneratePacket());
  }
}
