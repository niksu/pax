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

public class Echo_Server {
  SockID my_sock = TCP.socket(Internet_Domain.AF_Inet, Internet_Type.Sock_Stream, Internet_Protocol.TCP).value_exc();
  SockAddr_In my_addr = new SockAddr_In(7, IPAddress.Parse("192.168.100.100"));
  private readonly uint backlog = 1024;

  public void start () {
    // NOTE we call "value_exc()" to force the check to see if there's an error result.
    TCP.bind(my_sock, my_addr).value_exc();
    TCP.listen(my_sock, backlog).value_exc();

    //FIXME make this multithreaded, using a static thread pool.
    while (true) {
      SockAddr_In client_addr;
      SockID client_sock = TCP.accept(my_sock, out client_addr).value_exc();

      //FIXME read + write

      TCP.close(client_sock).value_exc();
    }
  }
}
