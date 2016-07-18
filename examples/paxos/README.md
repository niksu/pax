This is a Pax port of the [P4](http://p4.org) implementation described in the
[NetPaxos](http://www.inf.usi.ch/faculty/soule/netpaxos.html)
[repo](https://github.com/usi-systems/p4paxos/tree/ac69d52e402807f53142123000b0b4e7105fdd3c).

The implementation consists of two parts.

1. Dissector for the Paxos protocol header.
2. Logic implemented in the Coordinator and Acceptor.

## Differences from NetPaxos
* No handling of ARP.
* Rather than having forwarding behaviour, this is a module that could be
  plugged into (or chained with) a forwarding device.
