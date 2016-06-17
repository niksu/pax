This directory contains examples of packet processors written using Pax's API.
These examples are written in C#, but any [.NET language](https://en.wikipedia.org/wiki/List_of_CLI_languages) can be used.

# Examples
* [Hub](Hub.cs) simply forwards all receives all traffic that's received on a port to every other port, as illustrated below.

![Hub](http://www.cl.cam.ac.uk/~ns441/pax/hub.png)

* [Learning Switch](LearningSwitch.cs) forwards traffic between ports based on a mapping that it "learned" by forwarding traffic(!) If its mapping doesn't specify where to forward traffic, then the switch forwards it to all other ports. In the example below, we receive a frame for destination Yellow, from Pink. The switch learns that Pink can be reached through port 3. Not knowing how to reach Yellow, it forwards the frame through all ports. When it later receives a packet from Green to Pink, it only forwards the frame through port 3.

![LearningSwitch](http://www.cl.cam.ac.uk/~ns441/pax/learningswitch.png)

* [Mirror](Mirror.cs) involves duplicating a frame and sending it over an additional port.

![Mirror](http://www.cl.cam.ac.uk/~ns441/pax/mirror.png)

[Test](Test.cs) includes a variety of different packet processors (mostly contrived), including nested and chained packet processors. This means that packet processors can be combined with others to form a new kind of packet processor. Our assembly can accommodate such packet processors, as illustrated below. [Test](Test.cs) contains an example of a Mirror chained with a Switch.

![Chained](http://www.cl.cam.ac.uk/~ns441/pax/chained.png)

* [Network Address Translation](NAT.cs) is perhaps the most interesting example. Our example works for TCP. You can find out more about NAT from article such as [Wikipedia's](https://en.wikipedia.org/wiki/Network_Address_Translation).

## NAT example

### Configuration
The configuration needs to specify two parameters to the NAT's port 0 (i.e., the port connected to "Outside"):

* The IP address of the NAT. This will be used as the source address of packets crossing from the Inside to the Outside of the NAT.
* The starting TCP port to use on the NAT, to proxy TCP connections crossing from the Inside to the Outside of the NAT.

These parameters are provided as part of the `environment` map, as shown in the example NAT configuration below.

```javascript
[
  {
   "interface_name" : "eth0",
   "lead_handler" : "Nested_NAT",
   "environment" : {
      "my_address" : "192.168.3.3",
      "next_port" : "6000",
     }
  },
  {
   "interface_name" : "eth1",
   "lead_handler" : "Nested_NAT",
  },
  {
   "interface_name" : "eth2",
   "lead_handler" : "Nested_NAT",
  },
]
```

### Testing
I set up tcpdump to monitor the different network ports between which the NAT
is mediating, and see if the NAT's behaviour matches my intended behaviour.  Of
course, you need to generate network traffic to observe how the NAT behaves.
One could simple use a TCP-based application (e.g., telnet or a web browser) for
this, but I use Scapy to craft specific packets as follows.

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
nat_ip = "..."
nat_assigned_port = ...
interface = "..."
send((IP(dst=destination_ip,src=source_ip)/TCP(dport=destination_tcp_port,sport=source_tcp_port,flags=tcp_flags)/"Reply"),iface=interface)
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
use tools like ping or Scapy to generate generic or customised traffic, and
send it to a specific port. I then observe how the packet processor behaves,
through diagnostic messages it emits to the console, and through the packets
sniffed up by tcpdump on recipient interfaces.
I use `sudo tcpdump -vvvnXX -i IF` to see a detailed description of each packet,
replacing `IF` with the interface you want to sniff on.
