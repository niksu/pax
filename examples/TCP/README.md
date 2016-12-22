# TCPuny: a C# implementation of TCP

TCPuny exposes a Berkeley-style socket interface
([IBerkeleySocket.cs](IBerkeleySocket.cs)) for applications to send and receive
byte streams over an unreliably network, handling the following on the
application's behalf:
* loss (through retransmission)
* delivery of duplicates
* out-of-order delivery

Other than implementing TCP in C#, the goal of this work is to be explicit
about the resources available to C#, to allocate those resources up-front, and
to operate strictly within the resources available.


# Design (TODO)
Diagram showing the different entities: packet processing, active parts, user=program.
  TCP thread [TCPuny.cs](TCPuny.cs)
   supported by others [TCB.cs](TCB.cs), [TimerCB.cs](TimerCB.cs).
  Sockets interface [IBerkeleySocket.cs](IBerkeleySocket.cs)
  Program thread (client, server, peer, ...)

Three sorts of timer events:
* connection-establishment: expiry after SYN (remove TCB)
* retransmission
* 2MSL to remove TCB


Factored the implementation:
  - protocol parsing done by the time we see the packets
  - interface with the program's thread. use Berkeley interface, since more
    familiar to programmers.
  - TCP machine, running in the TCP thread.
      have multiple threads, and queues between them.
      lookups
        what data structures to maintain
      timers -- using Timer queue. also point to what Jonny did in NAT.
      interact with programs -- receiving and sending events
  - what about h2n, ARP, addressing, etc?


# Configuration
**Activate IP forwarding.** This is done in an OS-specific manner. For example on OSX:
```
sysctl -w net.inet.ip.forwarding=1
```
You also need to make sure that any firewalls are configured to permit the
traffic you want TCPuny to receive and send.

**Configure TCPuny**. For an example see [echo_wiring.json](echo_wiring.json).


# Example
An [Echo server](https://en.wikipedia.org/wiki/Echo_Protocol) is included as an
example program. The implementation ([Echo.cs](Echo.cs)) can use both TCPuny and
the TCP implementation made available via .NET.

To run the server using TCPuny:
```
$ sudo mono ./Bin/Pax.exe examples/TCP/echo_wiring.json examples/Bin/Examples.dll
```

To run the server using [TCwraP.cs](TCwraP.cs) (which wraps the .NET Socket class
using [IBerkeleySocket.cs](IBerkeleySocket.cs)):
```
$ sudo mono examples/Bin/Echo.exe --address 127.0.0.1 --port 7000 -v
```

For either TCwraP or TCPuny, to test them run this rom another terminal:
```
$ telnet 127.0.0.1 7000
```


# Limitations
This implementation can be improved and extended in various ways. Currently:
* uses fixed-size segments -- no adaptation of MSS based on "distance" between peers.
* no congestion control (and ssthresh, rto calculation).
* doesn't support any options (e.g., mss, window scaling, fast open)
* doesn't support flags like URG and PSH.
* ignores control messages (via ICMP)
* doesn't provide any support for host naming (to be resolved via DNS) or ARP.
* only supports passive open (no active open, and thus no simultaneous open or self-connect).
* more timers could be added, e.g., for keepalive, persist, fin-wait2.
* no Nagle algorithm (for batching sends),
* only supports immediate ACKing (no delaying). (note: this makes it vulnerable to so-called "silly window syndrome").
