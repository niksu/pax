#!/usr/bin/env sh
set -e

# Simple script to unset up the environment for running Pax elements.
# NOTE the un-setup step assumes that IP forwarding was disabled.
# Nik Sultana, February 2017
#
# Kudos to Jonny Shipton who wrote pax_mininet_node.py (included in the Pax
# repo) from which this script's contents are extracted.
#
# Use of this source code is governed by the Apache 2.0 license; see LICENSE.

ME="pax_node_unsetup.sh"

INTERFACE=$1
[ -z "${INTERFACE}" ] && echo "${ME} requires a parameter providing the network interface name" && exit 2

iptables -D INPUT -p tcp -i ${INTERFACE} -j DROP

arptables -P INPUT DROP
arptables --flush

sysctl -w net.ipv4.ip_forward=0
