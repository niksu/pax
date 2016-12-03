/*
Echo TCP server
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/


using System;
using PacketDotNet;
using Pax;
using Pax_TCP;

public class Echo_Server {
  SockID my_sock = TCP.socket(Internet_Domain.AF_Inet, Internet_Type.Sock_Stream, Internet_Protocol.TCP);
  SockAddr_In my_addr = new SockAddr_In(7, IPAddress.Parse("192.168.100.100"));
  var r = TCP.bind(my_sock, my_addr);

  public void start () {
    if (!r.result) {
      throw new Exception(r.ToString());
    }

    r = TCP.listen(my_sock, 1/*FIXME const*/);

    while (true) {
      //FIXME check if we have any availability left -- do this here, or in TCP?
      r = TCP.accept(my_sock, my_addr/*FIXME ???*/);

      //FIXME read + write

      //FIXME close
    }
  }
}
