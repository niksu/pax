![Pax](http://www.cl.cam.ac.uk/~ns441/pax/pax.png)

# [Pax](https://en.wiktionary.org/wiki/pax#Latin) be upon packets

System and network programming can be a magical mystery tour de force across
several APIs and tools.  Pax aspires to be your peaceful
[gesture](http://en.wikipedia.org/wiki/V_sign) at complexity, and seeks to
facilitate prototype development.

Using Pax you describe a network packet processor in terms of what it does to
packets sent and received through network logical *ports*, which are attached
at runtime to network interfaces made available by your OS. The interfaces may
be physical or virtual (e.g., a tap device).

![A Pax packet processor](http://www.cl.cam.ac.uk/~ns441/pax/packetproc.png)

A Pax processor can have any number of ports (numbered 0-3 in the drawing
above) which serve as the main interface with the outside world. The processor
can write to any of these ports, and processes data that arrives on some of the
ports (according to its configuration).

Pax provides a library and runtime support that wrap underlying wrappers so
you can quickly test prototypes of packet-processors written in high-level
languages. Some [example](https://github.com/niksu/pax/tree/master/examples) implementations are included in the repo.

# Building
## Dependencies
Other than a C# (>=6.0) compiler and runtime, you need:
* libpcap (on UNIXy systems) or winpcap.dll (on Windows)
* [SharpPcap](https://github.com/chmorgan/sharppcap) and [PacketDotNet](https://github.com/chmorgan/packetnet)
* Newtonsoft's JSON library (download one of the [releases](https://github.com/JamesNK/Newtonsoft.Json/releases))
* On Ubuntu Linux (14.04) it's easiest to follow Mono's
[instructions](http://www.mono-project.com/docs/getting-started/install/linux/) to install the latest version,
and `apt-get install` the `mono-complete`
package. You might need to tweak Mono's config files (such as that in
`/etc/mono/config`) in order to direct Mono's loader to the right location of
libpcap on your system (by adding a
[dllmap](http://www.mono-project.com/docs/advanced/pinvoke/dllmap/) entry).
* **Optional** Mininet for [testing](examples#testing-the-nat-with-mininet). We used Mininet 2.2.1 on Ubuntu 14.04 LTS.

Put the DLLs for Newtonsoft.Json, SharpPcap and PacketDotNet in Pax's `lib/` directory.

## Compiling
Run the `build.sh` script and everything should go smoothly.
This will produce a single file (Pax.exe), called an *assembly* in .NET jargon. This assembly serves two purposes:
* It is the tool that runs your packet processors using a configuration you provide.
* It is the library that your packet processors reference. This reference will be checked by the .NET compiler when compiling your code.

# Writing for Pax
Packet processers using Pax can be written in any [.NET language](https://en.wikipedia.org/wiki/List_of_CLI_languages).
They use Pax's API and define one or more functions that handle incoming packets.
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
Probably you'd have to run this command with root/administrator-level access
because of the privileged access to hardware that's used while Pax is running.
For the example code I run:
```
sudo ./Bin/Pax.exe examples/wiring.json examples/Bin/Examples.dll
```

Pax then starts up and checks the configuration file and assembly, listing some of their contents.
It connects the network interfaces with the handlers in the assembly, as specified in the configuration.
Then Pax activates the handlers, and your code takes it from there.

![Startup](http://www.cl.cam.ac.uk/~ns441/pax/start_screenshot.png)


# What does a Pax processor look like?
The main handler function of our [NAT example](examples/Nat/NATBase.cs) looks like this.
The return value is the port over which to emit the (modified) packet. The
actual network interface connected to that port is determined by the
configuration file.
```csharp
// Get the forwarding decision
ForwardingDecision forwardingDecision;
if (incomingNetworkInterface == Port_Outside)
  forwardingDecision = OutsideToInside(packet);
else
  forwardingDecision = InsideToOutside(packet, incomingNetworkInterface);

return forwardingDecision;
```
And the `OutsideToInside` function is implemented as follows:
```csharp
private ForwardingDecision OutsideToInside(TEncapsulation packet)
{
  // Retrieve the mapping. If a mapping doesn't exist, then it means that we're not
  // aware of a session to which the packet belongs: so drop the packet.
  var key = new ConnectionKey(packet.GetSourceNode(), packet.GetDestinationNode());
  NatConnection<TPacket,TNode> connection;
  if (NAT_MapToInside.TryGetValue(key, out connection))
  {
    var destination = connection.InsideNode;

    // Update any connection state, including resetting the inactivity timer
    connection.ReceivedPacket(packet, packetFromInside: false);

    // Rewrite the packet destination
    packet.SetDestination(destination);

    // Update checksums
    packet.UpdateChecksums();

    // Forward on the mapped network port
    return new ForwardingDecision.SinglePortForward(destination.InterfaceNumber);
  }
  else
  {
    return Drop;
  }
}
```

# License
Apache 2.0

# Acknowledgements :v:
* Project [NaaS](http://www.naas-project.org/) and its funder ([EPSRC](http://epsrc.ac.uk)).
* Colleagues at [NetOS](http://www.cl.cam.ac.uk/research/srg/netos/).
* Contributors to [PacketDotNet](https://github.com/chmorgan/packetnet), [SharpPcap](https://github.com/chmorgan/sharppcap), [Newtonsoft.JSON](https://github.com/JamesNK/Newtonsoft.Json/), [libpcap](http://www.tcpdump.org/) and [winpcap](http://www.winpcap.org/).

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
