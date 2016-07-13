#!/bin/bash

# FIXME: create a .sln file that includes both projects
xbuild Pax_Lite.csproj && \
xbuild Pax.csproj && \
xbuild examples/Examples.csproj && \
xbuild examples/Hub.csproj && \
echo Success
