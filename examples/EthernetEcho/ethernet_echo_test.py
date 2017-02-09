# Testing script for EthernetEcho example.
# Nik Sultana, February 2017
#
# Use of this source code is governed by the Apache 2.0 license; see LICENSE.

from scapy.all import *

h2_mac = "00:00:00:00:00:01"
h1_mac = "00:00:00:00:00:02"
sendp(Ether(type=0x9000, src=h2_mac, dst=h1_mac)/Raw("Hello"))
