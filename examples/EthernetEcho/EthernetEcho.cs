/*
Simple byte-based (i.e., without parsing the packet) packet processor.
Nik Sultana, February 2017

Organised into three threads:
1) receiver queues a packet for the processor
2) a processor inverts addresses and queues the packets for the sender
3) the sender sends off the packet

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/

using System;
using SharpPcap;
using System.Diagnostics;
using System.Threading;
using System.Collections.Concurrent;

using Pax;

public class EthernetEcho : ByteBased_PacketProcessor, IActive {
  string system_name = "EthernetEcho";
  Timer process_timer;
  Timer send_timer;

  int processor_interval;
  int sender_interval;

  ConcurrentQueue<Tuple<byte[], int>> in_q = new ConcurrentQueue<Tuple<byte[], int>>();
  ConcurrentQueue<Tuple<byte[], int>> out_q = new ConcurrentQueue<Tuple<byte[], int>>();

  public EthernetEcho (int processor_interval, int sender_interval) {
    this.processor_interval = processor_interval;
    this.sender_interval = sender_interval;
  }

  override public void process_packet (int in_port, byte[] packet) {
    in_q.Enqueue(new Tuple<byte[], int>(packet, in_port));
  }

  public void PreStart (ICaptureDevice device) {
    // FIXME could gather a list of applicable devices (from whom we can receive
    //       packets and to whom we can forward).
    //Debug.Assert(this.device == null);
    Debug.Assert(device != null);
    Console.WriteLine (system_name + " configured");
  }

  public void Stop () {
    Console.WriteLine ();
    // NOTE we'd use something like "lock (this) {active = false;}" if we used a
    //      running "while" rather than timers.
    if (process_timer != null) {process_timer.Dispose();}
    if (send_timer != null) {send_timer.Dispose();}
    Console.WriteLine (system_name + " stopped");
  }

  public void Start () {
    Console.WriteLine (system_name + " starting");
    process_timer = new Timer(Processor, null, 0, processor_interval);
    send_timer = new Timer(Sender, null, 0, sender_interval);
  }

  public void Processor (Object o) {
    Tuple<byte[], int> dMd;
    byte[] tmp = new byte[6];
    while (in_q.TryDequeue (out dMd)) {
      // FIXME assuming that LL is Ethernet

      // Swap src and dst addresses.
      for (int i = 0; i < 6; i++) {
        tmp[i] = dMd.Item1[i];
      }
      for (int i = 0; i < 6; i++) {
        dMd.Item1[i] = dMd.Item1[i + 6];
      }
      for (int i = 0; i < 6; i++) {
        dMd.Item1[i + 6] = tmp[i];
      }

#if DEBUG
      Console.Write(".");
#endif

      out_q.Enqueue(new Tuple<byte[], int>(dMd.Item1, dMd.Item2));
    }
  }

  public void Sender (Object o) {
    Tuple<byte[], int> dMd;
    while (out_q.TryDequeue (out dMd)) {
      send_packet (dMd.Item2, dMd.Item1, dMd.Item1.Length);
    }
  }
}
