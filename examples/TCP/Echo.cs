/*
Echo TCP server
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/


using System;
using System.Net;
using PacketDotNet;
using Pax;
using Mono.Options;
using System.Net.NetworkInformation;
using SharpPcap;
using System.Threading;

namespace Pax_TCP {

public class Echo_Server {
  IBerkeleySocket tcp;
  SockID my_sock;
  SockAddr_In my_addr;
  private bool verbose = false;

  public Echo_Server (IBerkeleySocket tcp, ushort port, IPAddress address, bool verbose) {
    this.verbose = verbose;
    this.tcp = tcp;
    my_sock = tcp.socket(Internet_Domain.AF_Inet, Internet_Type.Sock_Stream, Internet_Protocol.TCP).value_exc();
    my_addr = new SockAddr_In(port, address);
  }

  public void start () {
    // NOTE we call "value_exc()" to force the check to see if there's an error result.
    tcp.bind(my_sock, my_addr).value_exc();
    tcp.listen(my_sock).value_exc();

    //FIXME make this multithreaded, using a static thread pool.
    while (true) {
      SockAddr_In client_addr;
      SockID client_sock = tcp.accept(my_sock, out client_addr).value_exc();

      if (verbose) Console.WriteLine("Client added");

      // FIXME use static echo buffer, rather than keep reallocating.
      byte[] buf = new byte[20]; // FIXME const
      bool ended = false;
      int cutoff = 0;
      while (!ended) {
        var v = tcp.read (client_sock, buf, 10/*FIXME const*/); // FIXME check 'read' value to see if connection's been broken.
        if (verbose) Console.WriteLine("Got " + v.ToString());
        if (v.erroneous || v.value_exc() == 0 /* NOTE according to MSDN and SO, it appears that receiving 0 bytes means that the connection has been closed*/) {
          ended = true;
        } else {
          cutoff = v.value_exc();
          // Check 'buf' to see if there's an EOF
          for (int i = 0; i < cutoff; i++) {
            if (buf[i] == 0x04) {
              ended = true;
              cutoff = i;
              break;
            }

            if (verbose) Console.Write(buf[i].ToString() + ",");
          }

          if (verbose) Console.WriteLine();

          tcp.write (client_sock, buf, (uint)cutoff/*FIXME cast*/);
        }
      }

      tcp.close(client_sock).value_exc();
      if (verbose) Console.WriteLine("Client removed");
    }
  }
}

// Entry point when using the non-Pax stack.
public class NonPax_Echo_Server {
  public static int Main (string[] args) {
    IPAddress address = null;
    ushort port = 7; //default Internet port for Echo.
    bool verbose = false;
    uint max_conn = 100;
    uint max_backlog = 1024;

    OptionSet p = new OptionSet ()
      .Add ("v", _ => verbose = true)
      .Add ("address=", (String s) => address = IPAddress.Parse(s))
      .Add ("port=", (ushort pt) => port = pt)
      .Add ("max_conn=", (uint i) => max_conn = i)
      .Add ("max_backlog=", (uint i) => max_backlog = i);
    p.Parse(args).ToArray();

    if (address == null) {
      throw new Exception("Expected the parameter 'address' to be given.");
    }

    // Instantiate the TCP implementation
    IBerkeleySocket tcp = new TCwraP (max_conn, max_backlog);

    Console.WriteLine("Starting Echo server at " + address.ToString() + ":" + port.ToString());
    Console.WriteLine("Max. connections " + max_conn.ToString() + ", max. backlog " + max_backlog.ToString());
    var server = new Echo_Server(tcp, port, address, verbose);
    server.start(); // FIXME start as thread?
    return 0;
  }
}

// Entry point when using a Pax stack. It needs to wire up the listeners for the
// TCP instance, and pass the TCP instance to the application.
// That is, instead of a "main" function, we have a slightly different
// interface.
public class Pax_Echo_Server : PacketMonitor, IActive {
  bool verbose = false;

  // FIXME currently no way of assigning defaults to Pax parameters in a wiring.json file?
  ushort port;
  IPAddress ip_address;
  PhysicalAddress mac_address;
  uint max_conn;
  uint max_backlog;
  uint receive_buffer_size;
  uint send_buffer_size;

  IActiveBerkeleySocket tcp;
  Echo_Server server;

  public Pax_Echo_Server (PhysicalAddress mac_address, IPAddress ip_address, ushort port,
   uint max_conn, uint max_backlog, uint receive_buffer_size, uint send_buffer_size) {
    this.mac_address = mac_address;
    this.ip_address = ip_address;
    this.port = port;
    this.max_conn = max_conn;
    this.max_backlog = max_backlog;
    this.receive_buffer_size = receive_buffer_size;
    this.send_buffer_size = send_buffer_size;
  }

  public void PreStart (ICaptureDevice device) {
    Console.WriteLine("Instantiating TCP");
    Console.WriteLine("Max. connections " + max_conn.ToString() + ", max. backlog " + max_backlog.ToString());
    // Instantiate the TCP implementation
    tcp = new TCPuny (max_conn, max_backlog, ip_address, mac_address, receive_buffer_size, send_buffer_size);
    tcp.PreStart(device);
  }

  public void Start () {
    Console.WriteLine("Starting TCP");
    Thread t = new Thread (new ThreadStart (tcp.Start));
    t.Start();

    Console.WriteLine("Starting Echo server at " + ip_address.ToString() + ":" + port.ToString());
    server = new Echo_Server(tcp, port, ip_address, verbose);
    server.start();
  }

  public void Stop () {
    tcp.Stop();
    Console.WriteLine("Stopped TCP");
  }

  override public ForwardingDecision process_packet (int in_port, ref Packet packet) {
    // NOTE we assume that 'tcp' inherits from PacketMonitor.
    return ((PacketMonitor)tcp).process_packet(in_port, ref packet);
  }
}

}
