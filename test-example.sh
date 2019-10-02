#!/usr/bin/env bash

forge paket clear-cache ; ./build.sh && pushd example/ && dotnet restore && dotnet mono -f net462 --loggerlevel Trace --purge-system-net-http; popd
