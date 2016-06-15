#!/bin/bash

# FIXME: create a .sln file that includes both projects
xbuild Pax.csproj && xbuild examples/Examples.csproj && echo Success
