#!/usr/bin/env sh
set -e
[ -z "${PAX}" ] && echo "PAX environment variable must point to a clone of the Pax repo" && exit 2
sudo mono ${PAX}/Bin/Pax.exe ${PAX}/examples/EthernetEcho/ethernet_echo.json ${PAX}/examples/Bin/Examples.dll
