# KuduSupplyCollector
A supply collector designed to connect to Apache Kudu


## Known issues

* Don't know how to request row count and table size - so metrics don't work
* Random sampling doesn't work

## Requirements

Needs kudu docker image for tests. Copied and modified this one: https://github.com/MartinWeindel/kudu-docker-slim (see `docker` folder)	

Prebuilt image is stored at Docker hub as s2shape/kudu-dev
