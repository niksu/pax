This is a Pax port of the [P4](http://p4.org) implementation described in the
[NetPaxos](http://www.inf.usi.ch/faculty/soule/netpaxos.html)
[repo](https://github.com/usi-systems/p4paxos/tree/ac69d52e402807f53142123000b0b4e7105fdd3c).

The implementation consists of two parts.

1. Dissector for the Paxos protocol header.
2. Logic implemented in the Coordinator and Acceptor.

## Differences from the P4 implementation
* No handling of ARP.
* Rather than having forwarding behaviour, this is a module that could be
  plugged into (or chained with) a forwarding device.

## Acknowledgements
Thanks to [Huynh Tu Dang](http://www.people.usi.ch/danghu/), [Marco
Canini](http://perso.uclouvain.be/marco.canini/) and [Robert
Soul&eacute;](http://www.inf.usi.ch/faculty/soule/) for helping me understand
their implementation in P4.
