# TCPuny: a partial implementation of TCP in C#.

TCPuny exposes a Berkeley-style socket interface
([IBerkeleySocket.cs](IBerkeleySocket.cs)) for applications to send and receive
byte streams over an unreliable network, handling the following on the
application's behalf:
* loss (through retransmission)
* delivery of duplicates
* out-of-order delivery

Other than implementing TCP in C#, the goal of this work is to be explicit
about the resources available to C#, to allocate those resources up-front, and
to operate strictly within the resources available.


# Design
* Programs (client, server, peer, ...) run in their own thread(s), e.g., [Echo.cs](Echo.cs).
* TCP implementation uses a Berkeley-style interface with the program's thread. [IBerkeleySocket.cs](IBerkeleySocket.cs).
* Protocol parsing is done by the time the TCP logic sees the packets.
* One sort of timer event (retransmission), drawn from a finite resource set. [TimerCB.cs](TimerCB.cs)
* The TCP machine is organised into multiple threads which communicate using queues. The implementation is mainly described in [TCPuny.cs](TCPuny.cs) and [TCB.cs](TCB.cs).


# Configuration
**Activate IP forwarding.** This is done in an OS-specific manner.
For example on OSX type `$ sysctl -w net.inet.ip.forwarding=1`
and on Linux `$ sysctl -w net.ipv4.ip_forward=1` to enable IP forwarding
temporarily.
You also need to make sure that any firewalls are configured to permit the
traffic you want TCPuny to receive and send.

**Configure TCPuny**. For an example see [echo_wiring.json](echo_wiring.json).
It's important to set the correct MAC and IP addresses, otherwise outgoing
packets might be misrouted or dropped. You can check your MAC address using
`ifconfig` and get an idea of with whom you've been interacting locally by using
`arp`. To view MAC addresses when using `tcpdump` use the `-e` flag.


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

For either TCwraP or TCPuny, to test them run this from another terminal:
```
$ telnet 127.0.0.1 7000
```


# Limitations
This implementation can be improved and extended in various ways. Currently:
* implement connection closure (via FIN) -- currently I rely on RST to shut it down.
* uses fixed-size segments -- no adaptation of MSS based on "distance" between peers.
* no congestion control (and ssthresh, rto calculation).
* doesn't support any options (e.g., mss, window scaling, fast open)
* doesn't support flags like URG and PSH.
* ignores control messages (via ICMP)
* doesn't provide any support for host naming (to be resolved via DNS) or ARP.
* only supports passive open (no active open, and thus no simultaneous open or self-connect).
* more timers could be added, e.g., 2MSL to remove TCB, for connection-establishment (expiry after SYN, remove TCB), keepalive, persist, fin-wait2.
* no Nagle algorithm (for batching sends),
* only supports immediate ACKing (no delaying). (note: this makes it vulnerable to so-called "silly window syndrome").
* all address info is provided statically in the config -- don't currently support ARP and DNS names.
