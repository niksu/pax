# Pax API documentation
An overview of programming for Pax is given below, followed by a [description of
the API](#Details).

## Overview

## Programming model
Using Pax you describe a network packet processor in terms of what it does to
packets sent and received through network logical *ports*. At runtime, these
ports are attached by Pax to network interfaces made available by your OS. The
interfaces may be physical or virtual (e.g., a tap device).

![A Pax packet processor](http://www.cl.cam.ac.uk/~ns441/pax/packetproc.png)

A Pax processor can have any number of ports (numbered 0-3 in the drawing
above) which serve as the main interface with the outside world. The processor
can write to any of these ports, and processes data that arrives on some of the
ports (according to its configuration).


# What does a Pax processor look like?
The main handler function of our [NAT example](examples/Nat/NATBase.cs) looks like this.
The return value is the port over which to emit the (modified) packet. The
actual network interface connected to that port is determined by the
configuration file.
You can also read more about Pax's [API](API.md).
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


## Details
The API consists of 3 parts:
* The [core API](#Interfaces)
* Pax's [frontend](#Frontend)
* [Version management](#Versioning)


### Frontend
The *frontend* consists of the command-line tool that is passed a DLL and
configuration, and that runs the packet processors in your OS.

The code for this is in [Pax.cs](Pax.cs), in the class called `Frontend`.
You can interact with this in two ways:
* providing switches to the tool, to control its appearance perhaps, as if it was called from the command line.
* terminating the Frontend, for batch processes for example, by calling `Frontend.shutdown ()`.


### FIXME: Versioning
(Talk about static and dynamic checking)


### Interfaces
Pax provides different interfaces to facilitate the writing of different kinds
of packet processors. The code for this can be found in
[Paxifax.cs](Paxifax.cs).
Remember that ultimately the programming model consists of reading/writing to
logical network ports that are linked to resources made available by the OS
(e.g., physical NICs). But it's useful to differentiate between whether a packet
processor is allow to forward packets for example, and how many ports its
allowed to forward to.

#### Basic packet processors

##### IAbstract_PacketProcessor
```csharp
  public interface IAbstract_PacketProcessor {
    ForwardingDecision process_packet (int in_port, ref Packet packet);
    ...
```

##### IHostbased_PacketProcessor
```csharp
  public interface IHostbased_PacketProcessor {
    void packetHandler (object sender, CaptureEventArgs e);
  }
```

#### Derived packet processors

##### IPacketProcessor
```csharp
  public interface IPacketProcessor : IAbstract_PacketProcessor, IHostbased_PacketProcessor {}
```

##### PacketMonitor
```csharp
  // A packet monitor does not output anything onto the network, it simply
  // accumulates state based on what it observes happening on the network.
  // It might produce output on side-channels, through side-effects.
  // This could be used for diagnosis, to observe network activity and print
  // digests to the console or log.
  public abstract class PacketMonitor : IPacketProcessor {
    abstract public ForwardingDecision process_packet (int in_port, ref Packet packet);
    ...
```
*Example*: [Dropper](https://github.com/niksu/pax/blob/bbbbc34f412b196c24baa30ec4395b1455314bc5/Paxifax.cs#L232)

##### SimplePacketProcessor
```csharp
  // Simple packet processor: it can only transform the given packet and forward it to at most one interface.
  public abstract class SimplePacketProcessor : IPacketProcessor {
    // Return the offset of network interface that "packet" is to be forwarded to.
    abstract public ForwardingDecision process_packet (int in_port, ref Packet packet);
    ...
```
*Example*: [Mirror](examples/Mirror.cs)

##### MultiInterface_SimplePacketProcessor
```csharp
  // Simple packet processor that can forward to multiple interfaces. It is "simple" because
  // it can only transform the given packet, and cannot generate new ones.
  public abstract class MultiInterface_SimplePacketProcessor : IPacketProcessor {
    // Return the offsets of network interfaces that "packet" is to be forwarded to.
    abstract public ForwardingDecision process_packet (int in_port, ref Packet packet);
    ...
```
*Example*: [Hub](https://github.com/niksu/pax/blob/bbbbc34f412b196c24baa30ec4395b1455314bc5/examples/Hub.cs) and [Switch](https://github.com/niksu/pax/blob/bbbbc34f412b196c24baa30ec4395b1455314bc5/examples/LearningSwitch.cs)

##### PacketProcessor_Chain
```csharp
  public class PacketProcessor_Chain : IPacketProcessor {
    List<IPacketProcessor> chain;

    public PacketProcessor_Chain (List<IPacketProcessor> chain) {
      this.chain = chain;
    }
    ...
```
*Example*: [Nested_Chained_Test](https://github.com/niksu/pax/blob/bbbbc34f412b196c24baa30ec4395b1455314bc5/examples/Test.cs#L90) and [Nested_Chained_Test2](https://github.com/niksu/pax/blob/bbbbc34f412b196c24baa30ec4395b1455314bc5/examples/Test.cs#L108)

##### IActive
```csharp
  public interface IActive {
    // NOTE "PreStart" and "Start" might be called multiple times -- once for
    //      each device to which a packet processor is associated with.
    void PreStart (ICaptureDevice device);
    void Start ();
    void Stop ();
  }
```
*Examples*: [Generator](examples/Generator.cs) and [TCPuny](https://github.com/niksu/tcpuny) and [Recap](https://github.com/niksu/recap).

##### ByteBased_PacketProcessor
```csharp
  public abstract class ByteBased_PacketProcessor : IPacketProcessor {
    // FIXME include LL interface type as parameter.
    abstract public void process_packet (int in_port, byte[] packet);
    ...
```
*Examples*: [EthernetEcho](examples/EthernetEcho/EthernetEcho.cs) and [Recap](https://github.com/niksu/recap).
