#!/usr/bin/env python
# coding: latin-1

# Mininet testing script for EthernetEcho
# Nik Sultana, February 2017
#
# Use of this source code is governed by the Apache 2.0 license; see LICENSE.

from mininet.net import Mininet, CLI
from scapy.all import *
import os

host_mac = "02:00:00:00:00:02"
echoer_mac = "02:00:00:00:00:01"
echoer_iface_name = "echoer-eth0"

PAX = None
try:
  PAX = os.environ['PAX']
except KeyError:
  print "PAX environment variable must point to path where Pax repo is cloned"
  exit(1)

import sys
sys.path.insert(0, PAX + "/mininet/")
from pax_mininet_node import PaxNode

net = Mininet()
echoer = net.addHost('echoer', mac=echoer_mac, cls=PaxNode)
host = net.addHost('host', mac=host_mac)
switch = net.addSwitch('s0')
controller = net.addController('c0')
net.addLink(echoer, switch)
net.addLink(host, switch)

net.start()

echoer.cmd("sudo " + PAX + "/Bin/Pax.exe " + PAX + "/examples/EthernetEcho/ethernet_echo.json " + PAX + "/examples/Bin/Examples.dll &")

output = host.cmdPrint("sudo python " + PAX + "/examples/EthernetEcho/mn_ethernet_echo_test.py")
print output

net.stop()
