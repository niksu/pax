# Scapy testing script for EthernetEcho
# Nik Sultana, February 2017
#
# Use of this source code is governed by the Apache 2.0 license; see LICENSE.

from scapy.all import *
import time

# FIXME duplicated hardcoded configuration info
host_mac = "02:00:00:00:00:02"
echoer_mac = "02:00:00:00:00:01"
host_iface_name = "host-eth0"

sniff_count = 20
sniff_timeout = 10
wait_before_send = 6
wait_before_start = 6
# FIXME ensure that sniff_timeout > wait_before_send + wait_before_start

my_p = Ether(type=0x9000, src=host_mac, dst=echoer_mac)/Raw("Hello")
expected_p = Ether(type=0x9000, src=echoer_mac, dst=host_mac)/Raw("Hello")

newpid = os.fork()
# NOTE There might still a bunch of control traffic happening
#      at this point, we might want to filter it out.
time.sleep(wait_before_start)
if newpid == 0:
  time.sleep(wait_before_send)
  sendp(my_p)
  exit(0)
else:
  pkts = sniff(count=sniff_count, iface=host_iface_name, timeout=sniff_timeout)

# FIXME i assume that my packets occur at the end of the capture.
if (len(pkts) >= 2 and
    pkts[len(pkts) - 2] == my_p and
    pkts[len(pkts) - 1] == expected_p):
   print "Test succeeded"
   exit(0)
else:
   print "Test failed"
   exit(1)
