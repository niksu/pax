![Pax](http://www.cl.cam.ac.uk/~ns441/files/pax.png)

System and network programming can be a magical mystery tour de force across
several APIs and tools.  Pax aspires to be your peaceful
[gesture](http://en.wikipedia.org/wiki/V_sign) at complexity, and seeks to
facilitate prototype development.

Using Pax you describe a network packet processor in terms of what it does to
packets sent and received through network logical *ports*, which are attached
at runtime to network interfaces made available by your OS. The interfaces may
be physical or virtual (e.g., a tap device).

Pax provides a library and runtime support that wrap underlying wrappers so
you can quickly test prototypes of packet-processors written in high-level
languages. Some examples implementations are included in the repo.

# Building
## Dependencies
Other than a C# compiler and runtime, you need:
* libpcap (on UNIXy systems) or winpcap.dll (on Windows)
* [SharpPcap](https://github.com/chmorgan/sharppcap) and [PacketDotNet](https://github.com/chmorgan/packetnet)
* Newtonsoft's JSON library ([releases](https://github.com/JamesNK/Newtonsoft.Json/releases))

Put the DLLs for Newtonsoft.Json, SharpPcap and PacketDotNet in Pax's `lib/` directory.

## Compiling
Run the `build.sh` script and everything should go smoothly.
This will produce a single file (Pax.exe), called an *assembly* in .NET jargon. This assembly serves two purposes:
* It is the tool that runs your packet processors using a configuration you provide.
* It is the library that your packet processors reference. This reference will be checked by the .NET compiler when compiling your code.

# Writing for Pax
Packet processers using Pax can be written in any .NET language. They use Pax's API and define one or more functions that handle incoming packets.  The examples included with Pax could help get you going. The code is compiled using a .NET compiler to produce an assembly, which is then given to Pax as a parameter.

All parameters to your code (including the specific network interfaces that are bound to logical ports) are described in a configuration file.
Configuration in Pax is [JSON](https://en.wikipedia.org/wiki/JSON)-encoded.

# Running
Simply run `Pax.exe CONFIGURATION_FILENAME ASSEMBLY_FILENAME`.
Probably you'd have to run this command with root/administrator-level access
because of the privileged access to hardware that's used while Pax is running.
For the example code I run:
```
sudo ./Bin/Pax.exe examples/wiring.json examples/Bin/Examples.dll
```

# License
Apache 2.0

# Acknowledgements
* Project [NaaS](http://www.naas-project.org/) and its funders ([EPSRC](http://epsrc.ac.uk)).
* Colleagues at [NetOS](http://www.cl.cam.ac.uk/research/srg/netos/).
* Contributors to packetdotnet, sharppcap, Netwonsoft.Json, libpcap and winpcap.

# Ideas of other examples to implement using Pax
* Protocol conversion (e.g., IPv4 <-> IPv6)
* DPI
* Load balancer
* Datagram-based servers, e.g., DNS.
* Firewall
* Router

# Other systems to look at for prototyping or building network systems:
* [Click](http://read.cs.ucla.edu/click/click)
* [Netgraph](https://en.wikipedia.org/wiki/Netgraph)
* [Open vSwitch](https://en.wikipedia.org/wiki/Open_vSwitch)
* [RouteBricks](routebricks.org)
* [Scapy](https://en.wikipedia.org/wiki/Scapy)
