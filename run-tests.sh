#!/bin/bash
docker run -d --rm --name kudu -p 8050:8050 -p 8051:8051 -p 7050:7050 -p 7051:7051 s2shape/kudu-dev
sleep 10

export KUDU_HOST=localhost
export KUDU_PORT=7051

dotnet restore -s https://www.myget.org/F/s2/ -s https://api.nuget.org/v3/index.json
dotnet build
dotnet publish

pushd KuduSupplyCollectorLoader/bin/Debug/netcoreapp2.2/publish
dotnet SupplyCollectorDataLoader.dll -xunit KuduSupplyCollector hbase://$KUDU_HOST:$KUDU_PORT
popd

dotnet test

docker stop kudu
docker rm kudu
