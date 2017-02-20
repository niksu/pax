![Pax](http://www.cl.cam.ac.uk/~ns441/pax/pax.png)

# [Pax](https://en.wiktionary.org/wiki/pax#Latin) be upon packets

System and network programming can be a magical mystery tour de force across
several APIs and tools.  Pax aspires to be your peaceful
[gesture](http://en.wikipedia.org/wiki/V_sign) at complexity, and seeks to
facilitate prototype development.

Pax provides a library and runtime support that wrap underlying wrappers so
you can quickly test prototypes of packet-processors written in high-level
languages. More information can be found in our [API documentation](API.md).

# Example code
Various [example](https://github.com/niksu/pax/tree/master/examples)
implementations are included in this repo.
Additional examples involving Pax show how to replay a pcap file on the network
([recap](https://github.com/niksu/recap)) and an implementation of TCP
([TCPuny](https://github.com/niksu/tcpuny)).

# Building
Follow the instructions in [BUILD.md](BUILD.md).
We've tested Pax on OSX and different versions of Ubuntu Linux, but it shouldn't
be hard to get it running wherever .NET runs.

# Writing for Pax
Packet processers using Pax can be written in any [.NET language](https://en.wikipedia.org/wiki/List_of_CLI_languages).
They use Pax's [API](API.md) and define one or more functions that handle incoming packets.
The [examples](examples) included with Pax could help get you going.
The workflow is as follows:

1. Write your packet processors and use a .NET compiler to produce a DLL. Your DLL may contain multiple packet processors -- it is the *configuration file* that specifies which processor you wish you bind with which network interface.
2. Write a configuration file (or scheme) for your packet processor. This specifies all parameters to your packet processor, including which specific network interfaces that are bound to logical ports. Configuration in Pax is [JSON](https://en.wikipedia.org/wiki/JSON)-encoded.
3. Run Pax, indicating your configuration and DLL.

![A running Pax processor](http://www.cl.cam.ac.uk/~ns441/pax/running.png)

The configuration file "wires up" the network interfaces with packet processors in your assembly. Not all packet processors in your assembly need be connected, and different network interfaces may be connected to the same handler.
The drawing example shows an assembly with four packet processors, only two of which is used. The blue processor handles packets coming on network port 0. The configuration file determines which processors handle traffic coming on which network interface.

# Running
Simply run `Pax.exe CONFIGURATION_FILENAME ASSEMBLY_FILENAME`.
For more on command-line switches, see [CLI.md](CLI.md).
Probably you'd have to run this command with root/administrator-level access
because of the privileged access to hardware that's used while Pax is running.
For the example code I run:
```
sudo ./Bin/Pax.exe examples/wiring.json examples/Bin/Examples.dll
```

This runs the Printer element, which simply prints an integer whenever a
particular kind of packet arrives on an interface. For another element we use
for debugging, try:
```
sudo ./Bin/Pax.exe examples/tallyer_wiring.json examples/Bin/Examples.dll
```

Pax then starts up and checks the configuration file and assembly, listing some of their contents.
It connects the network interfaces with the handlers in the assembly, as specified in the configuration.
Then Pax activates the handlers, and your code takes it from there.

![Startup](http://www.cl.cam.ac.uk/~ns441/pax/start_screenshot.png)

# License
Pax is licensed under [Apache 2.0](license).
[Mono.Options](Options.cs) is licensed as described in its header.


# Acknowledgements :v:
* Project [NaaS](http://www.naas-project.org/) and its funder ([EPSRC](http://epsrc.ac.uk)).
* Colleagues at [NetOS](http://www.cl.cam.ac.uk/research/srg/netos/).
* Contributors to [PacketDotNet](https://github.com/chmorgan/packetnet),
  [SharpPcap](https://github.com/chmorgan/sharppcap),
  [Newtonsoft.JSON](https://github.com/JamesNK/Newtonsoft.Json/),
  [libpcap](http://www.tcpdump.org/) and [winpcap](http://www.winpcap.org/),
  [Mono.Options](https://github.com/mono/mono/blob/master/mcs/class/Mono.Options/Mono.Options/Options.cs).

# Project ideas
Try using Pax to implement prototypes of the following:
* Protocol conversion (e.g., IPv4 <-> IPv6)
* [DPI](https://en.wikipedia.org/wiki/Deep_packet_inspection)
* Load balancer (see these descriptions from the [HAProxy](http://1wt.eu/articles/2006_lb/index.html) and [NGINX](http://nginx.org/en/docs/http/load_balancing.html) sites for ideas)
* Datagram-based servers, e.g., DNS.
* Firewall
* Router

# Fellow travellers
If you're interested in what Pax does, you might be interested in these other systems:
* [Click](http://read.cs.ucla.edu/click/click)
* [Netgraph](https://en.wikipedia.org/wiki/Netgraph)
* [Open vSwitch](https://en.wikipedia.org/wiki/Open_vSwitch)
* [RouteBricks](routebricks.org)
* [Scapy](https://en.wikipedia.org/wiki/Scapy)
