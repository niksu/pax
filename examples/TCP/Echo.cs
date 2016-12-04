/*
Echo TCP server
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/


using System;
using System.Net;
using PacketDotNet;
using Pax;
using Pax_TCP;
using Mono.Options;

public class Echo_Server {
  IBerkeleySocket tcp =
    // FIXME parametrise protocol implementations.
    // new TCPuny (100, 1024); // Toggle between these two to use different TCP implementations.
    new TCwraP (100, 1024);
  SockID my_sock;
  SockAddr_In my_addr;
  private bool verbose = false;

  // FIXME use static echo buffer, rather than keep reallocating.
  public Echo_Server (uint port, IPAddress address, bool verbose) {
    my_sock = tcp.socket(Internet_Domain.AF_Inet, Internet_Type.Sock_Stream, Internet_Protocol.TCP).value_exc();
    my_addr = new SockAddr_In(port, address);
    this.verbose = verbose;
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

      bool ended = false;
      byte[] buf = new byte[20]; // FIXME const
      int cutoff = 0;
      while (!ended) {
        var v = tcp.read (client_sock, buf, 10/*FIXME const*/); // FIXME check 'read' value to see if connection's been broken.
        if (verbose) Console.WriteLine("Got " + v.ToString());
        if (v.erroneous /* FIXME are these meaningful? || v.value_exc() == 0 || v.value_exc() == -1*/) {
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

// FIXME can run this directly? I think with the current setup it needs to be
//       loaded via Pax?
public class Test_Echo_Server {
  public static int Main (string[] args) {
    IPAddress address = null;
    uint port = 7; //default Internet port for Echo.
    bool verbose = false;

    OptionSet p = new OptionSet ()
      .Add ("v", _ => verbose = true)
      .Add ("address=", (String s) => address = IPAddress.Parse(s))
      .Add ("port=", (uint pt) => port = pt);
    p.Parse(args).ToArray();

    if (address == null) {
      throw new Exception("Expected the parameter 'address' to be given.");
    }

    Console.WriteLine("Starting Echo server at " + address.ToString() + ":" + port.ToString());
    var server = new Echo_Server(port, address, verbose);
    server.start(); // FIXME start as thread?
    return 0;
  }
}
