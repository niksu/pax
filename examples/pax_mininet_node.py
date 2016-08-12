# coding: latin-1

"""
pax_mininet_node.py: Defines PaxNode which allows Pax to behave as the sole packet hander on a node.
Jonny Shipton, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
"""

from mininet.node import Node

class PaxNode( Node ):
    "PaxNode: A node which allows Pax to behave as the sole packet hander on that node."

    def __init__(self, name, disable_arp=False, **params):
        super(PaxNode, self).__init__(name, **params)
        self.ip_forward = ""
        self.disable_arp = disable_arp

    def config(self, **params):
        super(PaxNode, self).config(**params)

        # Setup iptable rules to drop incoming packets on each interface:
        # Because Pax only sniffs packets (it doesn't steal them), we need to drop the packets
        #  to prevent the OS from handling them and responding.
        for intf in self.intfList():
            self.cmd("iptables -A INPUT -p tcp -i %s -j DROP" % intf.name)

        # Also drop ARP packets for testing
        if (self.disable_arp):
            self.cmd("arptables -P INPUT DROP")

        # Disable ip_forward because otherwise, even with the above iptables rules, the OS
        #  will still forward packets that have a different IP on the other interfaces, which
        #  is not the behaviour we want from an ideal node that only processes packets through Pax.
        self.ip_forward = self.cmd("sysctl -n net.ipv4.ip_forward")
        self.cmd("sysctl -w net.ipv4.ip_forward=0")

    def terminate(self):
        # Remove iptables rules
        for intf in self.intfList():
            self.cmd("iptables -D INPUT -p tcp -i %s -j DROP" % intf.name)

        if (self.disable_arp):
            self.cmd("arptables -P INPUT DROP")
            self.cmd("arptables --flush")

        # Restore ip_forward value
        self.cmd("sysctl -w net.ipv4.ip_forward=%s" % self.ip_forward)

        super(PaxNode, self).terminate()
