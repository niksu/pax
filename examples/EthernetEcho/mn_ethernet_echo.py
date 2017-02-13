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

net = Mininet()
echoer = net.addHost('echoer', mac=echoer_mac)
host = net.addHost('host', mac=host_mac)
switch = net.addSwitch('s0')
controller = net.addController('c0')
net.addLink(echoer, switch)
net.addLink(host, switch)

net.start()

# FIXME use Jonny Shipton's PaxNode node type, to set these up automatically.
echoer.cmd("iptables -A INPUT -p tcp -i " + echoer_iface_name + " -j DROP")
echoer.cmd("arptables -P INPUT DROP")
echoer.cmd("sysctl -w net.ipv4.ip_forward=0")

echoer.cmd("sudo " + PAX + "/Bin/Pax.exe " + PAX + "/examples/EthernetEcho/ethernet_echo.json " + PAX + "/examples/Bin/Examples.dll &")

output = host.cmdPrint("sudo python " + PAX + "/examples/EthernetEcho/mn_ethernet_echo_test.py")
print output

net.stop()
