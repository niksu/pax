#!/usr/bin/python
# coding: latin-1

"""
nat_topo.py: a mininet topology for testing the NAT implementation
Jonny Shipton, Cambridge University Computer Lab, July 2016

Use of this source code is governed by the Apache 2.0 license; see LICENSE.
"""

from mininet.topo import Topo
from mininet.net import Mininet
from mininet.cli import CLI
from mininet.log import setLogLevel
import random
import time
import thread
import threading
from pax_mininet_node import PaxNode

config = None

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

    # Provide CLI access if requested
    if config.cli_first:
        CLI(net)

    # Names of the hosts we are interested in
    nat0 = "nat0"
    out0 = "out0"
    in1 = "in1"
    in2 = "in2"

    # Start the Pax NAT process on the NAT node:
    # Start it in a separate terminal so that we can see the output in real time.
    print "Starting Pax NAT process on %s:" % nat0
    cmd = 'Bin/Pax.exe examples/nat_wiring.json examples/Bin/Examples.dll'
    if config.X_windows:
        cmd = 'x-terminal-emulator -e \'%s\' &' % (cmd)
        runCmd(net, nat0, cmd)
    else:
        sendCmd(net, nat0, cmd)

    # Test the NAT by opening a connection between in1 and out0:
    print "Connecting from %s to %s:" % (in1, out0)
    # Set up a simple netcat server on out0 to respond with data when in1 connects:
    data = "Hello, are you there?"
    runCmd(net, out0, 'echo %s | netcat -l 12001 &' % data)
    # Connect to out0 from in1 and get the data received:
    # Timeout after 10 seconds if nothing happens
    result = runCmd(net, in1, 'netcat -n %s 12001' % ip(net, out0), timeout=10.0)
    # Trim newline chars from output
    if result is not None:
        result = result.rstrip('\n\r')
    # Print what was received
    received(in1, result)
    # Check that the received data was correct:
    if (result != data):
        print "WARNING: incorrect data received"
    else:
        print "Correct data received"

    # Test UDP support
    print ""
    print "Sending data via UDP from %s to %s:" % (in1, out0)
    # Set up a simple netcat server on out0 to get the data that in1 sends and respond:
    server_data = "Yes, UDP"
    sendCmd(net, out0, 'echo %s | nc -u -l 12001 -q1' % server_data) # q1 makes nc quit 1s after it sends the response
    # Send data to out0 from in1:
    client_data = "Hello, are you there UDP?"
    client_result = runCmd(net, in1, 'echo %s | nc -u %s 12001 -q1' % (client_data, ip(net, out0))) # q1 makes nc quit 1s after sending the packet
    server_result = waitOutput(net, out0)
    # Trim newline chars from output
    if client_result is not None:
        client_result = client_result.rstrip('\n\r')
    if server_result is not None:
        server_result = server_result.rstrip('\n\r')
    # Print what was received
    received(out0, server_result)
    received(in1, client_result)
    # Check that the received data was correct:
    if (server_result != client_data):
        print "WARNING: incorrect data received on the server"
    else:
        print "Correct data received on the server"
    if (client_result != server_data):
        print "WARNING: incorrect data received on the client"
    else:
        print "Correct data received on the client"

    # Run scapy test #1
    print ""
    print "Scapy test #1"
    sendCmd(net, out0, "examples/nat_scapy_tests.py server", xterm=True)
    runCmd(net, in1, "sleep 1")
    runCmd(net, in1, "examples/nat_scapy_tests.py client", xterm=True)
    waitOutput(net, out0)
    clientExitcode = runCmd(net, in1, "echo $?").rstrip('\n\r')
    serverExitcode = runCmd(net, out0, "echo $?").rstrip('\n\r')
    if (clientExitcode != "0" or serverExitcode != "0"):
        print "WARNING scapy test #1 failed. client %s, server %s" % (clientExitcode, serverExitcode)
    else:
        print "Scapy test #1 passed"

    # Run scapy test #2
    print ""
    print "Scapy test #2"
    sendCmd(net, out0, "examples/nat_scapy_tests.py server2", xterm=True)
    runCmd(net, in1, "sleep 1")
    runCmd(net, in1, "examples/nat_scapy_tests.py client2", xterm=True)
    waitOutput(net, out0)
    clientExitcode = runCmd(net, in1, "echo $?").rstrip('\n\r')
    serverExitcode = runCmd(net, out0, "echo $?").rstrip('\n\r')
    if (clientExitcode != "0" or serverExitcode != "0"):
        print "WARNING scapy test #2 failed. client %s, server %s" % (clientExitcode, serverExitcode)
    else:
        print "Scapy test #2 passed"

    # If we couldn't show the Pax output in a separate window, show it now.
    if not config.X_windows:
        sendInt(net, nat0)
        print "Show Pax output? (y/N)"
        if (sys.stdin.read(1).upper() == "Y"):
            waitOutput(net, nat0, verbose=True) # Print output
        else:
            waitOutput(net, nat0) # Don't print, just wait

    if config.hold_open:
        CLI(net)

    net.stop()

# List topologies defined in this file for Mininet
topos = { 'nat': (lambda: NatTopo())}

# Helper procedures
def setup_xterm(h, cmd, title=None):
    "Setup to show the command output in a new terminal, if X_windows enabled. Returns the modified command."
    if config.X_windows:
        # Use the command as the default title
        if title is None:
            title = "%s> $ %s" % (h.name, cmd)

        # Use a named pipe to show the output in a separate terminal, but run
        #  the command in this shell so we have access to the exit code.

        # Create the pipe
        pipe = "/tmp/cmdpipe%d" % (random.randint(0,1000))
        h.cmd("mkfifo %s" % pipe)
        # Create the command which will run inside the terminal:
        term_cmd = ("cat < %s " % pipe + # Show the output in the terminal (from the pipe)
            "; rm %s " % pipe + # Remove the named pipe after the command is done
            ("; read " if config.hold_open else "")) # Hold the terminal window open if configured to
        # Create the terminal window, set the title and run the terminal command
        h.cmd("xterm -T '%s' -e '%s' &" % (title, term_cmd))
        # Modify the command so that output isn't buffered, and pipe the output into the named pipe
        cmd = "stdbuf -i0 -o0 -e0 %s &> %s" % (cmd, pipe) # FIXME for most applications -iL etc. would be enough?
    return cmd
def runCmd(net, name, cmd, timeout=None, xterm=False, **args):
    "Run a shell command on a specific node in the network and return the output"
    # Get the Mininet node
    h = net.get(name)
    # Print the command so the user knows what's happening
    print "  %s> $ %s" % (name, cmd)

    # If the output should be in a separate window, set it up
    if xterm:
        cmd = setup_xterm(h, cmd)

    if (timeout is None):
        # If no timeout required, just run the command
        return h.cmd(cmd, **args)
    else:
        # We want the command to timeout if it runs too long.
        # Start a timer. Send an interrupt when it expires.
        timer = threading.Timer(timeout, lambda: h.sendInt(chr(4)))
        timer.start()
        # Run the command
        rv = h.cmd(cmd, **args)
        # Stop the timer (if it hasn't already timed out)
        timer.cancel()
        return rv
def sendCmd(net, name, cmd, xterm=False, **args):
    "Run a shell command on a specific node in the network and don't block waiting for it to finish. The output can be gotten with waitOutput."
    # Get the Mininet node
    h = net.get(name)
    # Print the command so the user knows what's happening
    print "  %s> $ %s" % (name, cmd)

    # If the output should be in a separate window, set it up
    if xterm:
        cmd = setup_xterm(h, cmd)

    # Start the command
    h.sendCmd(cmd, **args)
def waitOutput(net, name, **args):
    "Block until the running process on a specific node in the network finishes, and return the output."
    # Get the Mininet node
    h = net.get(name)
    # Block until done, and return the output
    return h.waitOutput(**args)
def received(name, result):
    "Prints a notification to the user that a value was received on a node in the network."
    if result is None:
        print "  %s> RCV: Nothing" % name
    else:
        print "  %s> RCV: '%s'" % (name, result)
def ip(net, name, **args):
    "Gets the IP address of a specific node in the network."
    # Get the Mininet node
    h = net.get(name)
    # Return the IP address
    return h.IP(**args)
def sendInt(net, name, **args):
    "Sends an interrupt to a specific node on the network."
    # Get the Mininet node
    h = net.get(name)
    # Send the interrupt
    h.sendInt(**args)


# This code runs when the script is executed (e.g. $ sudo ./examples/nat_topo.py)
import sys
import argparse
if __name__ == '__main__':
    setLogLevel('info')

    ## Parse CLI arguments
    # Set up the parser
    parser = argparse.ArgumentParser(description="Test the Pax NAT implementation.")
    parser.add_argument("action", choices=["run", "test", "perftest"], nargs="?", default="run")
    parser.add_argument("--no-X", help="don't launch additional windows", action="store_false", dest="X_windows")
    parser.add_argument("--hold-open", help="leave xterm windows open", action="store_true", dest="hold_open")
    parser.add_argument("--cli-first", help="provide cli access before starting pax and running the tests. Press ^D when done to begin the testing.", action="store_true", dest="cli_first")

    # Parse
    config = parser.parse_args()

    print "action: %s, X_windows: %s, hold_open: %s" % (config.action, config.X_windows, config.hold_open)

    # Run the specified action
    if config.action == "run":
        run()
    elif config.action == "test":
        test()
    else:
        print "Unknown action"
