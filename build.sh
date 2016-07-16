#!/bin/bash

# FIXME: create a .sln file that includes both projects
xbuild Pax_Lite.csproj /t:Rebuild && \
xbuild Pax.csproj /t:Rebuild && \
xbuild examples/Examples.csproj /t:Rebuild && \
xbuild examples/Hub.csproj /t:Rebuild && \
echo Success
