#!/usr/bin/env bash

forge paket clear-cache ; ./build.sh && pushd example/ && dotnet restore && dotnet mono -f net462 --loggerlevel Verbose --purge-system-net-http; popd
