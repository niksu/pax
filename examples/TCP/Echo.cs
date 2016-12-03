/*
Echo TCP server
Nik Sultana, Cambridge University Computer Lab, December 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
*/


using System;
using PacketDotNet;
using Pax;

public class Echo_Server {
  SockID my_sock = TCP.socket(...);
  SockAddr_In my_addr = new SockAddr_In(...);
  var r = TCP.bind(my_sock, my_addr);
  if (!r.result) {
    ...
  }
  r = TCP.listen(my_sock, 1/*FIXME const*/);

  while (true) {
    //FIXME check if we have any availability left -- do this here, or in TCP?
    r = TCP.accept(my_sock, my_addr/*FIXME ???*/);

    //FIXME read + write

    //FIXME close
  }
}
