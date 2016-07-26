#!/usr/bin/python
# coding: latin-1

"""
nat_scapy_tests.py: Rudimentary servers/clients for use in testing of the NAT.
Jonny Shipton, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.

This script is used in nat_topo.py in the testing of the NAT implementation.
The main reason for using Scapy is to be able to do things that may not be
possible with the OS network stack, such as sending packets during the TIME_WAIT state.
"""

from scapy.all import *
from scapy.layers.inet import *

# Values that need to be kept in sync with those in the wiring config:
# NOTE that all times are in seconds
tcp_inactivity_timeout = 20
tcp_time_wait_duration = 10
udp_inactivity_timeout= 200

# The IP addresses of the nodes we use
serverhost = "10.0.0.4"
nathost = "10.0.0.3"
clienthost = "192.168.1.10"

# Templates for iptables rules
iptables_rule_fmt = "INPUT -p tcp --sport %d --dport %d -j DROP"
iptables_add_rule_fmt = "iptables -A %s"
iptables_remove_rule_fmt = "iptables -D %s"

# Data to be sent through the NAT
server_data = "SERVER DATA"
client_data = "CLIENT DATA"

def run_server(serverport=12012, natport=35001):
    """This should be run on the external host.
       This test checks that a TCP connection can be opened from the inside to the outside and that
       the connection entry is removed after the inactivity timeout elapses."""

    # Set up iptables rule to allow us exclusive access on our port
    iptables_rule = iptables_rule_fmt % (natport, serverport)
    print "  server> $ %s" % (iptables_add_rule_fmt % iptables_rule)
    subprocess.check_call(iptables_add_rule_fmt % iptables_rule, shell=True)

    # We use the same IP layer and filter each time
    ip = IP(src=serverhost, dst=nathost)
    filter = "tcp and host %s and port %d" % (nathost, serverport)

    # Wait for the connection to be initialised from the client
    print "Waiting for connection from client"
    syn = sniff(count=1, filter=filter)[0]
    assert syn.sprintf("%TCP.flags%") == "S" # Check it's a Syn
    # send Syn+Ack
    tcp_synack = TCP(sport=serverport, dport=natport, flags="SA", options=[('MSS', 1460)])
    print "Sending Syn+Ack"
    send(ip/tcp_synack)

    # Receive data and check if it is correct
    print "Waiting for data from client"
    datap = sniff(count=2, filter=filter)[1]
    print "Received:"
    datap.show()
    data = str(datap[TCP].payload)
    if (data == client_data):
        print "Correct data received from the client."
    else:
        print "WARNING: incorrect data received from the client: '%s'" % data

    # Wait for connection to be dropped
    print "Sleeping to wait for the connection to be dropped by the NAT (reliant on tcp_inactivity_timeout=%ds)" % tcp_inactivity_timeout
    time.sleep(tcp_inactivity_timeout + 2)

    # Try sending data - should fail. We wait on the client to check that we don't receive this packet.
    print "Trying to send a packet to the client - should fail"
    send(ip/TCP(sport=serverport, dport=natport, flags="", options=[('MSS', 1460)])/"FAIL PLEASE")

    # Remove iptables rule
    print "  server> $ %s" % (iptables_remove_rule_fmt % iptables_rule)
    subprocess.check_call(iptables_remove_rule_fmt % iptables_rule, shell=True)

    if (data == client_data):
        return 0
    else:
        return 1

def run_client(serverport=12012, clientport=12011):
    """This should be run on the internal host.
       This test checks that a TCP connection can be opened from the inside to the outside and that
       the connection entry is removed after the inactivity timeout elapses."""

    # Set up iptables rule to allow us exclusive access on our port
    iptables_rule = iptables_rule_fmt % (serverport, clientport)
    print "  client> $ %s" % (iptables_add_rule_fmt % iptables_rule)
    subprocess.check_call(iptables_add_rule_fmt % iptables_rule, shell=True)

    # We use the same IP layer and filter each time
    ip = IP(src=clienthost, dst=serverhost)
    filter = "tcp and host %s and port %d" % (serverhost, clientport)

    # Establish connection to the server
    tcp_syn = TCP(sport=clientport, dport=serverport, flags="S", options=[('MSS', 1460)])
    print "Sending syn"
    synack = sr1(ip/tcp_syn)[0]
    assert synack.sprintf("%TCP.flags%") == "SA" # Check it's a SynAck
    print "Received:"
    synack.show()
    # send Ack
    tcp_ack = TCP(sport=clientport, dport=serverport, flags="A", options=[('MSS', 1460)])
    print "Sending ack"
    send(ip/tcp_ack)

    # Send data
    print "Sending data"
    send(ip/TCP(sport=clientport, dport=serverport, flags="")/client_data)

    # Check we don't get another packet
    print "Waiting to see if we get another packet"
    rcv = sniff(count=1, timeout=tcp_inactivity_timeout + 5, filter=filter)
    if (len(rcv) != 0):
        print "Received %d packets! Shouldn't have" % len(rcv)
        rcv[0].show()

    # Remove iptables rule
    print "  client> $ %s" % (iptables_remove_rule_fmt % iptables_rule)
    subprocess.check_call(iptables_remove_rule_fmt % iptables_rule, shell=True)


    if (len(rcv) != 0):
        return 1
    else:
        return 0

def run_server2(serverport=12022, natport=35002):
    """This should be run on the external host.
       This test checks that a TCP connection can be opened from the inside to the outside, and that when it is
       closed with Fin packets, the connection entry is removed after the TIME_WAIT timeout elapses."""

    # Set up iptables rule to allow us exclusive access on our port
    iptables_rule = iptables_rule_fmt % (natport, serverport)
    print "  server> $ %s" % (iptables_add_rule_fmt % iptables_rule)
    subprocess.check_call("netcat -l %d &" % serverport, shell=True)
    subprocess.check_call(iptables_add_rule_fmt % iptables_rule, shell=True)

    # We use the same IP layer and filter each time
    ip = IP(src=serverhost, dst=nathost)
    filter = "tcp and host %s and port %d" % (nathost, serverport)

    ## Set up and tear down connection
    # Wait for Syn
    print "Waiting for Syn from client"
    syn = sniff(count=1, filter=filter)
    assert syn[0].sprintf("%TCP.flags%") == "S" # Check it's a Syn
    # Syn+Ack
    print "Replying with Syn+Ack"
    ack = sr1(ip/TCP(sport=serverport,dport=natport,flags="SA"))
    assert ack.sprintf("%TCP.flags%") == "A" # Check it's an Ack
    # Fin
    print "Sending Fin"
    finack = sr1(ip/TCP(sport=serverport,dport=natport,flags="F"))
    assert finack.sprintf("%TCP.flags%") == "FA" # Check it's a FinAck
    # Ack
    print "Replying with Ack"
    send(ip/TCP(sport=serverport,dport=natport,flags="A"))

    ## Now in TIME_WAIT
    print "Sleeping to wait for the connection entry to be removed. (reliant on tcp_time_wait_duration=%ds)" % tcp_time_wait_duration
    time.sleep(tcp_time_wait_duration + 1)

    ## Should now be unable to use the connection
    # Send something. We wait on the client to check we don't receive it.
    time.sleep(1) # Wait to make sure the client has begun sniffing
    print "Sending something - it shouldn't be received"
    send(ip/TCP(sport=serverport,dport=natport,flags="A")/"FAIL PLEASE")
    # Check we don't receive anything
    print "Waiting to see if we receive anything"
    rcv = sniff(count=1, timeout=2, filter=filter)
    if (len(rcv) != 0):
        print "Received %d packets! Shouldn't have" % len(rcv)
        rcv[0].show()

    # Remove iptables rule
    print "  server> $ %s" % (iptables_remove_rule_fmt % iptables_rule)
    subprocess.check_call(iptables_remove_rule_fmt % iptables_rule, shell=True)

    if (len(rcv) != 0):
        return 1
    else:
        return 0

def run_client2(serverport=12022, clientport=12021):
    """This should be run on the internal host.
       This test checks that a TCP connection can be opened from the inside to the outside, and that when it is
       closed with Fin packets, the connection entry is removed after the TIME_WAIT timeout elapses."""

    # Set up iptables rule to allow us exclusive access on our port
    iptables_rule = iptables_rule_fmt % (serverport, clientport)
    print "  client> $ %s" % (iptables_add_rule_fmt % iptables_rule)
    subprocess.check_call(iptables_add_rule_fmt % iptables_rule, shell=True)

    # We use the same IP layer and filter each time
    ip = IP(src=clienthost, dst=serverhost)
    filter = "tcp and host %s and port %d" % (serverhost, clientport)

    ## Set up and tear down connection
    # Send Syn
    print "Sending Syn"
    synack = sr1(ip/TCP(sport=clientport,dport=serverport,flags="S"))
    assert synack.sprintf("%TCP.flags%") == "SA" # Check it's a SynAck
    # Ack
    print "Replying with Ack"
    fin = sr1(ip/TCP(sport=clientport,dport=serverport,flags="A"))
    assert fin.sprintf("%TCP.flags%") == "F" # Check it's a Fin
    # Fin+Ack
    print "Sending Fin+Ack"
    ack = sr1(ip/TCP(sport=clientport,dport=serverport,flags="FA"))
    assert ack.sprintf("%TCP.flags%") == "A" # Check it's an Ack

    ## Now in TIME_WAIT
    print "Sleeping to wait for the connection entry to be removed. (reliant on tcp_time_wait_duration=%ds)" % tcp_time_wait_duration
    time.sleep(tcp_time_wait_duration + 1)

    ## Should now be unable to use the connection
    # Check we don't receive anything
    print "Waiting to see if we receive anything"
    rcv = sniff(count=1, timeout=2, filter=filter)
    if (len(rcv) != 0):
        print "Received %d packets! Shouldn't have" % len(rcv)
        rcv[0].show()
    # Send something. We wait on the server to make sure we don't receive anything.
    time.sleep(1) # Wait to make sure the server has begun sniffing
    print "Sending something - it shouldn't be received"
    send(ip/TCP(sport=clientport,dport=serverport,flags="A")/"FAIL PLEASE")

    # Remove iptables rule
    print "  server> $ %s" % (iptables_remove_rule_fmt % iptables_rule)
    subprocess.check_call(iptables_remove_rule_fmt % iptables_rule, shell=True)

    if (len(rcv) != 0):
        return 1
    else:
        return 0

import sys
def usage():
    print "%s server[n] [port]     Run the server code for test n. The NAT will use port port."
    print "%s client[n]            Run the client code for test n."
    print "Valid range for n: 1-2"

# This code runs when the script is executed (e.g. $ sudo ./examples/nat_scapy_tests.py)
if __name__ == '__main__':
    print "Please ensure the settings in this script file match those in the nat wiring config file."

    if len(sys.argv) > 1:
        action = sys.argv[1]
        if action.startswith("server"):
            # Get the port
            port = 35000
            if len(sys.argv) > 2:
                port = int(sys.argv[2])

            # Run the server code for the specified test
            suffix = action[len("server"):len(action)]
            if suffix == "" or suffix == "1":
                sys.exit(run_server(natport=port))
            elif suffix == "2":
                sys.exit(run_server2(natport=port))
        elif action.startswith("client"):
            # Run the client code for the specified test
            suffix = action[len("client"):len(action)]
            if suffix == "" or suffix == "1":
                sys.exit(run_client())
            elif suffix == "2":
                sys.exit(run_client2())

    # If we reach this point, the correct syntax wasn't used
    usage()
    sys.exit(2)

