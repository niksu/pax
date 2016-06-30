# coding: latin-1

"""
pax_mininet_node.py: Defines PaxNode which allows Pax to behave as the sole packet hander on a node.
"""

from mininet.node import Node
from mininet.log import info, warn

class PaxNode( Node ):
    "PaxNode: A node which allows Pax to behave as the sole packet hander on that node."

    def __init__(self, name, **params):
        super(PaxNode, self).__init__(name, **params)

    def config(self, **params):
        super(PaxNode, self).config(**params)

        # Setup iptable rules to drop incoming packets on each interface:
        # Because Pax only sniffs packets (it doesn't steal them), we need to drop the packets
        #  to prevent the OS from handling them and responding.
        print "Drop all incoming TCP traffic on nat0 so that Pax is effectively the middle-man"
        for intf in self.intfList():
          self.cmd("iptables -A INPUT -p tcp -i %s -j DROP" % intf.name)

    def terminate(self):
        # Remove iptables rules
        for intf in self.intfList():
          self.cmd("iptables -D INPUT -p tcp -i %s -j DROP" % intf.name)

        super(PaxNode, self).terminate()
