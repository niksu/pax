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
  SockID my_sock = TCP.socket(Internet_Domain.AF_Inet, Internet_Type.Sock_Stream, Internet_Protocol.TCP).value_exc();
  SockAddr_In my_addr = new SockAddr_In(7, IPAddress.Parse("192.168.100.100"));
  private readonly uint backlog = 1024;

  public void start () {
    TCP.bind(my_sock, my_addr).value_exc();
    TCP.listen(my_sock, backlog).value_exc();

    while (true) {
      //FIXME check if we have any availability left -- do this here, or in TCP?
      r = TCP.accept(my_sock, my_addr/*FIXME ???*/);

      //FIXME read + write

      //FIXME close
    }
  }
}
