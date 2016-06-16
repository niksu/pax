This directory contains examples of packet processors written using Pax's API.
These examples are written in C#, but any [.NET language](https://en.wikipedia.org/wiki/List_of_CLI_languages) can be used.

Examples:
* [Hub](Hub.cs) simply forwards all receives all traffic that's received on a port to every other port, as illustrated below.

![Hub](http://www.cl.cam.ac.uk/~ns441/pax/hub.png)

* [Learning Switch](LearningSwitch.cs) forwards traffic between ports based on a mapping that it "learned" by forwarding traffic(!) If its mapping doesn't specify where to forward traffic, then the switch forwards it to all other ports. In the example below, we receive a frame for destination Yellow, from Pink. The switch learns that Pink can be reached through port 3. Not knowing how to reach Yellow, it forwards the frame through all ports. When it later receives a packet from Green to Pink, it only forwards the frame through port 3.

![LearningSwitch](http://www.cl.cam.ac.uk/~ns441/pax/learningswitch.png)

* [Mirror](Mirror.cs) involves duplicating a frame and sending it over an additional port.

![Mirror](http://www.cl.cam.ac.uk/~ns441/pax/mirror.png)

[Test](Test.cs) -- slightly random, but also instantiates other packet processors

![Chained](http://www.cl.cam.ac.uk/~ns441/pax/chained.png)

* [Network Address Translation](NAT.cs) is perhaps the most interesting example. Our example works for TCP, you can find out more about NAT from article such as [Wikipedia's](https://en.wikipedia.org/wiki/Network_Address_Translation).

# Port constraints
Note that the examples are not sensitive to a fixed number of ports.
The Hub, Switch and Mirror work with any number of ports -- though it would not be sensible to have fewer than two.
The NAT needs at least one port (for the Outside), but it would not be sensible to have fewer than two ports.
You need at least one port for the Inside.

# Test workflow
I set up tcpdump to monitor an interface over which I expect to see data, and use tools like ping or Scapy to generate generic or customised traffic, and send it to a specific port. I then observe how the packet processor behaves, through diagnostic messages it emits to the console, and through the packets sniffed up by tcpdump on recipient interfaces.
