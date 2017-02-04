#!/bin/bash
# Rudimentary build script
# Nik Sultana, Cambridge University Computer Lab, July 2016
#
# FIXME: replace this with an .sln file

set -e

# NOTE to not have any symbols, instead of an empty ${DEFINE} use a dud one like "NOTHING"
if [ -z "${DEFINE}" ]
then
  # Default symbols
  DEFINE="DEBUG TRACE LITE"
fi

xbuild Pax_Lite.csproj /t:Rebuild /p:DefineConstants="${DEFINE}"
xbuild Pax.csproj /p:DefineConstants="${DEFINE}"
xbuild examples/Examples.csproj /p:DefineConstants="${DEFINE}"
xbuild examples/Hub.csproj /p:DefineConstants="${DEFINE}"
xbuild examples/Echo.csproj /p:DefineConstants="${DEFINE}"

echo Success
