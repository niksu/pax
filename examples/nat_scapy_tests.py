#!/usr/bin/python
# coding: latin-1

from scapy.all import *
from scapy.layers.inet import *

port = 12011
serverhost = "10.0.0.4"
nathost = "10.0.0.3"
clienthost = "192.168.1.10"
iptables_rule_fmt = "INPUT -p tcp --sport %d --dport %d -j DROP"
iptables_add_rule_fmt = "iptables -A %s"
iptables_remove_rule_fmt = "iptables -D %s"

server_data = "SERVER DATA"
client_data = "CLIENT DATA"

def run_server(serverport=12012, natport=35001):
    "This should be run on the external host."

    # Set up iptables rule to allow us exclusive access on our port
    iptables_rule = iptables_rule_fmt % (natport, serverport)
    print "  server> $ %s" % (iptables_add_rule_fmt % iptables_rule)
    subprocess.check_call(iptables_add_rule_fmt % iptables_rule, shell=True)

    ip = IP(src=serverhost, dst=nathost)
    filter = "tcp and host %s and port %d" % (nathost, serverport)
    
    # Wait for the connection to be initialised from the client
    print "Waiting for connection from client"
    syn = sniff(count=1, filter=filter)[0]
    # send Syn+Ack
    ackn = syn[TCP].seq + 1
    seqn = 15312
    tcp_synack = TCP(sport=serverport, dport=natport, flags="SA", seq=seqn, ack=ackn, options=[('MSS', 1460)])
    print "Sending Syn+Ack"
    send(ip/tcp_synack)

    # Receive data
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
    print "Sleeping to wait for the connection to be dropped by the NAT"
    time.sleep(12) # 12s

    # Try sending data - should fail
    seqn += 1
    ackn += 1
    print "Trying to send a packet to the client - should fail"
    send(ip/TCP(sport=serverport, dport=natport, flags="", seq=seqn, ack=ackn, options=[('MSS', 1460)])/"FAIL PLEASE")

    # Remove iptables rule
    print "  server> $ %s" % (iptables_remove_rule_fmt % iptables_rule)
    subprocess.check_call(iptables_remove_rule_fmt % iptables_rule, shell=True)

    if (data == client_data):
        return 0
    else:
        return 1

def run_client(serverport=12012, clientport=12011):
    "This should be run on the internal host."

    # Set up iptables rule to allow us exclusive access on our port
    iptables_rule = iptables_rule_fmt % (serverport, clientport)
    print "  client> $ %s" % (iptables_add_rule_fmt % iptables_rule)
    subprocess.check_call(iptables_add_rule_fmt % iptables_rule, shell=True)

    ip = IP(src=clienthost, dst=serverhost)
    filter = "tcp and host %s and port %d" % (serverhost, clientport)
    
    # Establish connection to the server
    seqn = 1230
    tcp_syn = TCP(sport=clientport, dport=serverport, flags="S", seq=seqn, options=[('MSS', 1460)])
    print "Sending syn"
    synack = sr1(ip/tcp_syn)[0]
    print "Received:"
    synack.show()
    # send Ack
    seqn += 1
    ackn = synack.seq + 1
    tcp_ack = TCP(sport=clientport, dport=serverport, flags="A", seq=seqn, ack=ackn, options=[('MSS', 1460)])
    print "Sending ack"
    send(ip/tcp_ack)

    # Send data
    seqn += 1
    print "Sending data"
    send(ip/TCP(sport=clientport, dport=serverport, flags="", seq=seqn, ack=ackn)/client_data)

    # Check we don't get another packet
    print "Waiting to see if we get another packet"
    rcv = sniff(count=1, timeout=15, filter=filter)
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
    "This should be run on the external host."

    # Set up iptables rule to allow us exclusive access on our port
    iptables_rule = iptables_rule_fmt % (natport, serverport)
    print "  server> $ %s" % (iptables_add_rule_fmt % iptables_rule)
    subprocess.check_call("netcat -l %d &" % serverport, shell=True)
    subprocess.check_call(iptables_add_rule_fmt % iptables_rule, shell=True)

    ip = IP(src=serverhost, dst=nathost)
    filter = "tcp"#" and host %s and port %d" % (nathost, serverport)

    ## Set up and tear down connection
    # Wait for Syn
    print "Waiting for Syn from client"
    syn = sniff(count=1, filter=filter)
    # Syn+Ack
    seqn = 12310
    ackn = syn[0].seq + 1
    print "Replying with Syn+Ack"
    ack = sr1(ip/TCP(sport=serverport,dport=natport,flags="SA", seq=seqn,ack=ackn))
    # Fin
    seqn += 1
    ackn += 1
    print "Sending Fin"
    finack = sr1(ip/TCP(sport=serverport,dport=natport,flags="FA", seq=seqn,ack=ackn))
    # Ack
    seqn += 1
    ackn += 1
    print "Replying with Ack"
    send(ip/TCP(sport=serverport,dport=natport,flags="A", seq=seqn,ack=ackn))

    ## Should now be unable to use the connection
    # Send something
    seqn += 1
    ackn += 1
    print "Sending something - it shouldn't be received"
    send(ip/TCP(sport=serverport,dport=natport,flags="A", seq=seqn,ack=ackn)/"FAIL PLEASE")
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
    "This should be run on the internal host."

    # Set up iptables rule to allow us exclusive access on our port
    iptables_rule = iptables_rule_fmt % (serverport, clientport)
    print "  client> $ %s" % (iptables_add_rule_fmt % iptables_rule)
    subprocess.check_call(iptables_add_rule_fmt % iptables_rule, shell=True)

    ip = IP(src=clienthost, dst=serverhost)
    filter = "tcp and host %s and port %d" % (serverhost, clientport)

    ## Set up and tear down connection
    # Send Syn
    print "Sending Syn"
    seqn = 16430
    synack = sr1(ip/TCP(sport=clientport,dport=serverport,flags="S", seq=seqn))
    # Ack
    seqn += 1
    ackn = synack[0].seq + 1
    print "Replying with Ack"
    fin = sr1(ip/TCP(sport=clientport,dport=serverport,flags="A", seq=seqn,ack=ackn))
    # Fin+Ack
    seqn += 1
    ackn += 1
    print "Sending Fin+Ack"
    ack = sr1(ip/TCP(sport=clientport,dport=serverport,flags="FA", seq=seqn,ack=ackn))

    ## Should now be unable to use the connection
    # Send something
    seqn += 1
    ackn += 1
    print "Sending something - it shouldn't be received"
    send(ip/TCP(sport=clientport,dport=serverport,flags="A", seq=seqn,ack=ackn)/"FAIL PLEASE")
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

# This code runs when the script is executed (e.g. $ sudo ./examples/nat_scapy_tests.py)
import sys
if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1]=="server":
        # The command was `$ nat_topo.py test`, so run the automated test:
        sys.exit(run_server())
    elif len(sys.argv) > 1 and sys.argv[1]=="client":
        # The command was `$ nat_topo.py test`, so run the automated test:
        sys.exit(run_client())
    elif len(sys.argv) > 1 and sys.argv[1]=="server2":
        # The command was `$ nat_topo.py test`, so run the automated test:
        sys.exit(run_server2())
    elif len(sys.argv) > 1 and sys.argv[1]=="client2":
        # The command was `$ nat_topo.py test`, so run the automated test:
        sys.exit(run_client2())

