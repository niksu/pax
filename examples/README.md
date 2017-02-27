This directory contains examples of packet processors written using Pax's API:
* [Hub](#hub)
* [Learning switch](#learningswitch)
* [Mirror](#mirror)
* [NAT](#nat)
* [Packet generator](#packetgenerator)
* [Ethernet Echo](#ethernetecho)
These examples are written in C#, but any [.NET language](https://en.wikipedia.org/wiki/List_of_CLI_languages) can be used.

# Examples
* <a name="hub"></a>[Hub](Hub.cs) simply forwards all receives all traffic that's received on a port to every other port, as illustrated below.

![Hub](http://www.cl.cam.ac.uk/~ns441/pax/hub.png)

* <a name="learningswitch"></a>[Learning Switch](LearningSwitch.cs) forwards traffic between ports based on a mapping that it "learned" by forwarding traffic(!) If its mapping doesn't specify where to forward traffic, then the switch forwards it to all other ports. In the example below, we receive a frame for destination Yellow, from Pink. The switch learns that Pink can be reached through port 3. Not knowing how to reach Yellow, it forwards the frame through all ports. When it later receives a packet from Green to Pink, it only forwards the frame through port 3.

![LearningSwitch](http://www.cl.cam.ac.uk/~ns441/pax/learningswitch.png)

* <a name="mirror"></a>[Mirror](Mirror.cs) involves duplicating a frame and sending it over an additional port.

![Mirror](http://www.cl.cam.ac.uk/~ns441/pax/mirror.png)

[Test](Test.cs) includes a variety of different packet processors (mostly contrived), including nested and chained packet processors. This means that packet processors can be combined with others to form a new kind of packet processor. Our assembly can accommodate such packet processors, as illustrated below. [Test](Test.cs) contains an example of a Mirror chained with a Switch.

![Chained](http://www.cl.cam.ac.uk/~ns441/pax/chained.png)

* <a name="nat"></a>[Network Address Translation](NAT.cs) is perhaps the most interesting example. Our example works for TCP. You can find out more about NAT from article such as [Wikipedia's](https://en.wikipedia.org/wiki/Network_Address_Translation).

## NAT example

### Configuration
The configuration needs to specify three parameters to the NAT's constructor:

* The IP address of the NAT. This will be used as the source address of packets crossing from the Inside to the Outside of the NAT.
* The starting TCP port to use on the NAT, to proxy TCP connections crossing from the Inside to the Outside of the NAT.
* The MAC address of the next hop to Outside; the address of the node which the Out port (port 0) of the NAT is connected to.

These parameters are provided as part of the `args` map, as shown in the example NAT configuration below.

```javascript
{
  "handlers": [
    {
      "class_name": "NAT",
      "args": {
        "my_address" : "10.0.0.3",
        "next_port" : "35000",
        "next_hop_mac" : "00:00:00:00:00:09"
      }
    }
  ],
  "interfaces": [
    {
      "interface_name" : "nat0-eth0",
      "lead_handler" : "NAT",
      "pcap_filter" : "tcp"
    },
    {
      "interface_name" : "nat0-eth1",
      "lead_handler" : "NAT",
      "pcap_filter" : "tcp"
    },
    {
      "interface_name" : "nat0-eth2",
      "lead_handler" : "NAT",
      "pcap_filter" : "tcp"
    }
  ]
}
```

### Testing
I set up tcpdump to monitor the different network ports between which the NAT
is mediating, and see if the NAT's behaviour matches my intended behaviour.  Of
course, you need to generate network traffic to observe how the NAT behaves.
One could simple use a TCP-based application (e.g., telnet or a web browser) for
this, but I use [Scapy](https://en.wikipedia.org/wiki/Scapy) to craft specific packets as follows.

I use the following code skeleton to generate TCP packets, sometimes setting
`tcp_flags="S"` to generate TCP packets with the SYN flag set.  (You must fill
in specific values for each `"..."` of course, depending on your network
setup.)

```python
destination_ip = "..."
source_ip = "..."
destination_tcp_port = ...
source_tcp_port = ...
tcp_flags = "..."
interface = "..."
send((IP(dst=destination_ip,src=source_ip)/TCP(dport=destination_tcp_port,sport=source_tcp_port,flags=tcp_flags)/"Pax"),iface=interface)
```

I use the following snippet to send a reply.

```python
# Reusing some values from above.
destination_ip = "..."
destination_tcp_port = ...

# Probably updating some fields from above.
interface = "..."
flags = "..."

# Additional values used for reply.
nat_ip = "..."
nat_assigned_tcp_port = ...
send((IP(dst=nat_ip,src=destination_ip)/TCP(dport=nat_assigned_tcp_port,sport=destination_tcp_port,flags=tcp_flags)/"Reply"),iface=interface)
```

**FIXME**: It might be worth wrapping this into a tiny library of Python functions, and to automate the testing.

# Port constraints
Note that the examples are not sensitive to a fixed number of ports.
The Hub, Switch and Mirror work with any number of ports -- though it would not be sensible to have fewer than two.
The NAT needs at least one port (for the Outside), but it would not be sensible to have fewer than two ports.
You need at least one port for the Inside.

# Test workflow
As described for the NAT example above,
I set up tcpdump to monitor an interface over which I expect to see data, and
use tools like ping or [Scapy](https://en.wikipedia.org/wiki/Scapy) to generate generic or customised traffic, and
send it to a specific port. I then observe how the packet processor behaves,
through diagnostic messages it emits to the console, and through the packets
sniffed up by tcpdump on recipient interfaces.
I use `sudo tcpdump -vvvnXX -i IF` to see a detailed description of each packet,
replacing `IF` with the interface you want to sniff on.

# Testing the NAT with Mininet
[Mininet](http://mininet.org/) is a tool for emulating networks on your local machine.
This is quite handy because we can run reproducible tests and experiment with
networks when we don't have the physical machines otherwise needed.

To test the NAT implementation:
- [Install](http://mininet.org/download/) Mininet - it doesn't matter which way. We used Mininet 2.2.1 on Ubuntu 14.04 LTS.
- cd into your cloned Pax directory and run `$ sudo ./examples/Nat/nat_topo.py test`
- You can also jump into the Mininet CLI by running `$ sudo ./examples/Nat/nat_topo.py`

You will find it very helpful to learn more about Mininet if you plan to do anything
apart from run the automated test. The Mininet
[walkthrough](http://mininet.org/walkthrough/) is a pretty good start.
One very useful thing to know is to use `mininet> host command` in the CLI to run
`command` on `host`. E.g. to run ifconfig on in1, use `mininet> in1 ifconfig`.

We could manually test the NAT by running these commands:
``` bash
$ sudo ./examples/Nat/nat_topo.py
mininet> out0 echo 'Hi from out0!' | netcat -l 12010 &
mininet> in1 netcat out0 12010
```
Note that on line 3, we specify `out0` instead of the IP of out0. We can do this
because the Mininet CLI replaces it with the IP. Be aware that this is the IP of the
default interface, which is fine here because out0 only has one, but on nat0
it might not be the IP you were expecting.

_

Feel free to look in [`examples/Nat/nat_topo.py`](Nat/nat_topo.py):
- The `NatTopo` class defines the network topology for Mininet
```
                  ┌──────┐     ┌─────┐
                  |      |-----│ in1 |
    ┌──────┐      |      |     └─────┘
    │ out0 |------| nat0 |
    └──────┘      |      |     ┌─────┐
                  |      |-----│ in2 |
                  └──────┘     └─────┘
```
- You might notice that `nat0` is specified as being a node of type `PaxNode`. You can
  look at the definition in [`pax_mininet_node.py`](pax_mininet_node.py). It is a host with firewall rules on
  each of the interfaces on nat0 to drop all incoming traffic. This is so that only
  the NAT process will respond to packets. Otherwise, the OS could reject or accept
  connections that are intended for internal hosts. We also disable ip_forward so that
  the OS doesn't forward packets that aren't addressed to it.
- The `createNetwork()` procedure instantiates the network topology and sets up
  the hosts. It sets the default gateway for the internal hosts to nat0 so that
  connections to outside the subnet go through the NAT.

  Take special note that the MAC address of out0 is manually set. This is so because
  the NAT implementation currently requires the next hop to be hardcoded in the config.
  Without this, out0 would ignore packets from the NAT, because the MAC would be wrong.
- The `run()` procedure provides a commandline-interface to the network.
- The `test()` procedure creates a network, tests the NAT implementation by
  creating a connection between in1 and out0, and then cleans up.

## <a name="packetgenerator"></a>Packet generator
The packet generator example emits packets on a specific interface at regular
intervals. It's a very simple affair, but can easily be extended for more
complex behaviour.

### Configuration
The generator emits TCP segments from (`src_port`,`src_ip`,`src_mac`) to
(`dst_port`,`dst_ip`,`dst_mac`) every `interval` milliseconds, on interface
`interface_name`. An example configuration can be seen in
[generator_wiring.json](generator_wiring.json):

```javascript
{
  "handlers": [
    {
      "class_name": "Generator",
      "args": {
        "interval": "100",
        "src_port": "10",
        "dst_port": "11",
        "src_ip": "10.0.0.4",
        "dst_ip": "10.0.0.5",
        "src_mac": "02-00-00-00-00-01",
        "dst_mac": "02-00-00-00-00-02"
      }
    }
  ],
  "interfaces": [
    {
      "interface_name" : "en3",
      "lead_handler" : "Generator"
    }
  ]
}
```

### Testing
I ran the generator by calling
```
$ sudo mono ./Bin/Pax.exe --config=examples/generator_wiring.json --code=examples/Bin/Examples.dll
```
and, in parallel used tcpdump  -- with a suitable filter -- to see what the generator is producing:
```
$ sudo tcpdump -nvvv "host 10.0.0.4"
tcpdump: data link type PKTAP
tcpdump: listening on pktap, link-type PKTAP (Packet Tap), capture size 65535 bytes
20:52:29.872721 IP (tos 0x0, ttl 64, id 0, offset 0, flags [none], proto TCP (6), length 40)
    10.0.0.4.10 > 10.0.0.5.11: Flags [none], cksum 0x9bc7 (correct), seq 0, win 0, length 0
20:52:29.966521 IP (tos 0x0, ttl 64, id 0, offset 0, flags [none], proto TCP (6), length 40)
    10.0.0.4.10 > 10.0.0.5.11: Flags [none], cksum 0x9bc7 (correct), seq 0, win 0, length 0
20:52:30.069319 IP (tos 0x0, ttl 64, id 0, offset 0, flags [none], proto TCP (6), length 40)
    10.0.0.4.10 > 10.0.0.5.11: Flags [none], cksum 0x9bc7 (correct), seq 0, win 0, length 0
20:52:30.169720 IP (tos 0x0, ttl 64, id 0, offset 0, flags [none], proto TCP (6), length 40)
    10.0.0.4.10 > 10.0.0.5.11: Flags [none], cksum 0x9bc7 (correct), seq 0, win 0, length 0
20:52:30.269609 IP (tos 0x0, ttl 64, id 0, offset 0, flags [none], proto TCP (6), length 40)
    10.0.0.4.10 > 10.0.0.5.11: Flags [none], cksum 0x9bc7 (correct), seq 0, win 0, length 0
```


## <a name="ethernetecho"></a>Ethernet Echo
[EthernetEcho.cs](EthernetEcho/EthernetEcho.cs) implements an element that swaps
the source and destination Ethernet addresses.
The goal of this example is to show the *byte-based* interface for packet
processors in Pax, i.e., it avoids dissecting and constructing packets, but
rather works at the lower level of byte arrays.

### Testing
This example was tested in Mininet using the [mn_ethernet_echo.py](EthernetEcho/mn_ethernet_echo.py)
script which automatically generates and tests for
[ECTP](https://en.wikipedia.org/wiki/Ethernet_Configuration_Testing_Protocol) [EtherType](https://en.wikipedia.org/wiki/EtherType)
frames.
When running Mininet in a VM, remember that to compile/run Pax elements in that
VM you must have Mono installed there too.
