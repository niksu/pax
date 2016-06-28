#!/usr/bin/python

"""
natnet.py: a mininet topology for testing the NAT implementation

"""

from mininet.topo import Topo
from mininet.net import Mininet
from mininet.cli import CLI
from mininet.log import setLogLevel
import time

class NatTopo(Topo):
    "`n` Inside hosts, connected to an Outside host via a node on which NAT can be run"
    def __init__(self, n=2, **opts):
        Topo.__init__(self, **opts)

        # Create the node to host the NAT process
        nat = self.addNode("nat0")

        # Create the Outside host
        publicHost = self.addHost("out0", mac="00:00:00:00:00:09")
        self.addLink(nat, publicHost, intfName1="nat0-eth0")

        # Create Inside hosts
        for i in range(1, n+1):
            networkIP = "192.168.1.%d/24" % (i + 9) # Start at 192.168.1.10
            name = "in%d" % i
            host = self.addHost(name, ip=networkIP)
            self.addLink(nat, host, intfName1="nat0-eth%d" % i, params1={"ip":"192.168.1.1/24"})

def createNetwork():
    n = 2
    topo = NatTopo(n=n)
    net = Mininet(topo=topo)
    net.start()
    print "Network started"
    
    nat0 = "nat0"
    out0 = "out0"
    in1 = "in1"
    in2 = "in2"

    for i in range(1,n+1):
        h = "in%d" % i
        print "Setting the gateway on %s to %s" % (h, nat0)
        runCmd(net, h, 'route add 192.168.1.1/32 dev %s-eth0' % h)
        runCmd(net, h, 'route add default gw 192.168.1.1 dev %s-eth0' % h)
    
    print "Disabling ip_forwarding on %s" % nat0
    runCmd(net, nat0, "sysctl -w net.ipv4.ip_forward=0")

    print "Drop all incoming TCP traffic on nat0 so that Pax is effectively the middle-man"
    for i in range(0,n+1):
        runCmd(net, nat0, "iptables -A INPUT -p tcp -i %s-eth%d -j DROP" % (nat0, i))

    return net

def run():
    "Create network and run the CLI"
    net = createNetwork()
    CLI(net)
    net.stop()

def test():
    "Test the NAT implementation"
    net = createNetwork()

    nat0 = "nat0"
    out0 = "out0"
    in1 = "in1"
    in2 = "in2"
    
    print "Starting Pax NAT process on %s:" % nat0
    runCmd(net, nat0,
        'x-terminal-emulator -e sudo ~/pax/Bin/Pax.exe ~/pax/examples/nat_wiring.json ~/pax/examples/Bin/Examples.dll &')
    
    print "Connecting from %s to %s:" % (in1, out0)
    data = "Hello, are you there?"
    runCmd(net, out0, 'echo %s | netcat -l 12001 &' % data)
    result = runCmd(net, in1, 'netcat -n %s 12001' % ip(net, out0)).rstrip('\n').rstrip('\r')
    received(in1, result)
    if (result != data):
        print "WARNING: incorrect data received"
    else:
        print "Correct data received"

topos = { 'nat': (lambda: NatTopo())}

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
    

import sys
if __name__ == '__main__':
    setLogLevel('info')
    if len(sys.argv) > 1 and sys.argv[1]=="test":
        test()
    else:
        run()
