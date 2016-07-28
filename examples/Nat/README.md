This directory contains the implementation of a simple NAT device written using Pax's API.

The NAT performs Port-Address Translation in order to hide a network behind a single IP
address. For example, if a client within the internal network tries to connect to an
external host, the NAT will rewrite those packets so that it looks as if it's the NAT that
is connecting. Then the external host will respond with packets addressed to the NAT, and
the NAT can lookup the port it arrived on in order to determined which internal host it is
actually for. It can then rewrite the packet so that it is addressed to that host.

## Overview of classes
`NAT` is the main packet processor, and the class that should be instantiated by Pax
and attached to the network interfaces. When it receives a packet, it first determines
which transport protocol it uses, and then passes the packet to a specialised `NATBase`
that can handle that type.

`NATBase` is the superclass for protocol-specific implementations. The mapping of ports
and addresses and removal of old entries are done in this class, and the subclass only
needs to override two abstract methods. Two examples are the `TcpNAT` and `UdpNAT` classes,
which provide support for the TCP and UDP protocols.

The `NATBase` class is parameterised by three types: `TPacket` (the type of transport-layer
packet, such as `TcpPacket`); `TNode` (the type of `Node` we address, e.g. `NodeWithPort`
which represents a socket); and `TEncapsulation` (the type of the `PacketEncapsulation` we
handle).

The `Node` class represents a node on the network, and can carry the MAC address and network
interface needed to reach it, as well as the IP address. Subclasses of `Node` can contain
additional information, such as the TCP port for example, meaning that the class is not
strictly representative of a node so much as of a particular socket or similar. The `Node`
class and any subclasses should only compare network-layer and higher addressing information,
so the MAC address and network interface are not compared in an equality check.

The `PacketEncapsulation` class provides typed access to the packets at the link, network,
and transport layers. It is an abstract, generic class, designed to be only used through
non-generic subclasses that implement protocol-specific behaviour, such as
`TcpPacketEncapsulation`.

In the lookups used within the `NATBase` class, the `ConnectionKey` class is used for the
keys. It consists solely of a source and a destination `Node`. The lookup operation gets a
matching `NatConnection` object.

The `NatConnection` class contains the addressing information for the nodes involved in the
connection (the outside node, the inside node, and the NAT), some connection state, and
information used in the garbage collection of old entries, namely the time that it was
last used. The connection state object must implement the `ITransportState` interface.

The `ITransportState` interface only exposes methods to allow the state to be notified of
packets passing through the connection, and to query whether the connection entry can be
removed before inactivity timeout, for example because it has closed. The `TcpState` class
implements this interface, and tries to infer the TCP state of the connection by tracking
the Syn, Ack and Fin packets that are sent.
