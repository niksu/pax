#!/usr/bin/python
# coding: latin-1

"""
nat_topo.py: a mininet topology for testing the NAT implementation

"""

from mininet.topo import Topo
from mininet.net import Mininet
from mininet.cli import CLI
from mininet.log import setLogLevel
import time
from pax_mininet_node import PaxNode

# This class defines the topology we use for testing the Pax NAT implementation.
# The parameter `n` defines the number of inside nodes created.
#                  ┌──────┐     ┌─────┐
#                  |      |-----│ in1 |
#    ┌──────┐      |      |     └─────┘
#    │ out0 |------| nat0 |     ┌─────┐
#    └──────┘      |      |-----│ in2 |
#                  |      |     └─────┘
#                  |      |        ︙    
#                  |      |     ┌─────┐
#                  |      |-----│ inN |
#                  └──────┘     └─────┘
class NatTopo(Topo):
    "`n` Inside hosts, connected to an Outside host via a node on which NAT can be run"
    def __init__(self, n=2, **opts):
        Topo.__init__(self, **opts)

        # Create the node to host the NAT process:
        nat = self.addNode("nat0", cls=PaxNode)

        # Create the Outside host:
        # Use a hardcoded MAC address, because we provide the MAC to the NAT as next_hop
        publicHost = self.addHost("out0", mac="00:00:00:00:00:09")
        # Link the Outside host (out0) to the NAT on the outside port:
        self.addLink(nat, publicHost, intfName1="nat0-eth0")

        # Create Inside hosts:
        for i in range(1, n+1):
            networkIP = "192.168.1.%d/24" % (i + 9) # Start at 192.168.1.10
            name = "in%d" % i
            # Create the host with an IP on the inside network
            host = self.addHost(name, ip=networkIP)
            # Link the host to the NAT, creating an interface on the NAT:
            # The IP addresses of all the inside ports of NAT are the same because the NAT
            #  is effectively a NAT router behind a switch, but all in the same node.
            self.addLink(nat, host, intfName1="nat0-eth%d" % i, params1={"ip":"192.168.1.1/24"})

# Instantiate the NAT network, using NatTopo, and set it up for testing.
def createNetwork(n=2):
    # Create the network using NatTopo:
    topo = NatTopo(n=n)
    net = Mininet(topo=topo)
    net.start()
    print "Network started"
    
    # Names of the hosts we are interested in
    nat0 = "nat0"
    out0 = "out0"

    # Set the gateway on the inside hosts, so that they use the NAT to send packets outside:
    for i in range(1,n+1):
        h = "in%d" % i
        print "Setting the gateway on %s to %s" % (h, nat0)
        net.get(h).setDefaultRoute('via 192.168.1.1')

    return net

# Start the network and open a commandline-interface for manual testing.
def run(n=2):
    "Create network and run the CLI"
    # Create the network and initialise for testing:
    net = createNetwork(n)
    # Start the commandline-interface for interactive testing:
    CLI(net)
    # Stop and cleanup the network:
    net.stop()

# Start the network, run an automated test, and shut down the network.
def test():
    "Test the NAT implementation"
    # Create the network and initialise for testing:
    net = createNetwork(n=2)

    # Names of the hosts we are interested in
    nat0 = "nat0"
    out0 = "out0"
    in1 = "in1"
    in2 = "in2"
    
    # Start the Pax NAT process on the NAT node:
    # Start it in a separate terminal so that we can see the output in real time.
    print "Starting Pax NAT process on %s:" % nat0
    runCmd(net, nat0,
        'x-terminal-emulator -e sudo Bin/Pax.exe examples/nat_wiring.json examples/Bin/Examples.dll &')
    
    # Test the NAT by opening a connection between in1 and out0:
    print "Connecting from %s to %s:" % (in1, out0)
    # Set up a simple netcat server on out0 to respond with data when in1 connects:
    data = "Hello, are you there?"
    runCmd(net, out0, 'echo %s | netcat -l 12001 &' % data)
    # Connect to out0 from in1 and get the data received:
    result = runCmd(net, in1, 'netcat -n %s 12001' % ip(net, out0)).rstrip('\n').rstrip('\r')
    received(in1, result)
    # Check that the received data was correct:
    if (result != data):
        print "WARNING: incorrect data received"
    else:
        print "Correct data received"

# List topologies defined in this file for Mininet
topos = { 'nat': (lambda: NatTopo())}

# Helper procedures
def runCmd(net, name, cmd):
    h = net.get(name)
    print "  %s> $ %s" % (name, cmd)
    return h.cmd(cmd)
def sendCmd(net, name, cmd):
    h = net.get(name)
    print "  %s> $ %s" % (name, cmd)
    h.sendCmd(cmd)
def waitOutput(net, name):
    h = net.get(name)
    h.waitOutput()
def received(name, result):
    print "  %s> RCV: '%s'" % (name, result)
def ip(net, name):
    h = net.get(name)
    return h.IP()
    

# This code runs when the script is executed (e.g. $ sudo ./examples/nat_topo.py)
import sys
if __name__ == '__main__':
    setLogLevel('info')
    if len(sys.argv) > 1 and sys.argv[1]=="test":
        # The command was `$ nat_topo.py test`, so run the automated test:
        test()
    else:
        # No command argument wsa provided that we understand; start the CLI:
        run()
