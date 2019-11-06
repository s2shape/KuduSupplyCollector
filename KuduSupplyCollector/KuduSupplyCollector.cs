using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Client;
using Kudu.Client.Builder;
using Kudu.Client.Connection;
using Kudu.Client.Protocol;
using S2.BlackSwan.SupplyCollector;
using S2.BlackSwan.SupplyCollector.Models;

namespace KuduSupplyCollector
{
    public class KuduSupplyCollector : SupplyCollectorBase
    {
        private const string PREFIX = "kudu://";

        public string BuildConnectionString(string host, int port)
        {
            return $"{PREFIX}{host}:{port}";
        }

        public override List<string> DataStoreTypes()
        {
            return (new[] { "Kudu" }).ToList();
        }

        private static KuduClient Connect(string connectionString) {
            var hostPort = connectionString.Substring(PREFIX.Length).Split(":");
            var host = hostPort[0];
            var port = Int32.Parse(hostPort[1]);

            return new KuduClient(new KuduClientOptions() {
                MasterAddresses = new List<HostAndPort>() {
                    new HostAndPort(host, port)
                }
            });
        }

        public override List<string> CollectSample(DataEntity dataEntity, int sampleSize)
        {
            var results = new List<string>();

            var conn = Connect(dataEntity.Container.ConnectionString);
            var table = conn.OpenTableAsync(dataEntity.Collection.Name).Result;

            var builder = conn.NewScanBuilder(table);
            builder.SetLimit(sampleSize);
            builder.SetProjectedColumns(new[] {dataEntity.Name});
            var scanner = builder.Build();

            var enumerator = scanner.GetAsyncEnumerator();
            while (enumerator.MoveNextAsync().Result) {
                foreach (var row in enumerator.Current) {
                    results.Add(row.GetString(0));
                }
            }

            return results;
        }

        public override List<DataCollectionMetrics> GetDataCollectionMetrics(DataContainer container) {
            var metrics = new List<DataCollectionMetrics>();

            var conn = Connect(container.ConnectionString);
            var tables = conn.GetTablesAsync().Result;

            metrics.AddRange(
                tables.Select(x => new DataCollectionMetrics() {
                    Name = x.Name
                })
            );
            /*
            foreach (var metric in metrics) {
                var table = conn.OpenTableAsync(metric.Name).Result;

                var scanBuilder = conn.NewScanBuilder(table);
            }*/

            return metrics;
        }

        private DataType ConvertDataType(DataTypePB dbDataType) {
            switch (dbDataType) {
                case DataTypePB.Binary:
                    return DataType.ByteArray;
                case DataTypePB.Bool:
                    return DataType.Boolean;
                case DataTypePB.Decimal128:
                    return DataType.Decimal;
                case DataTypePB.Decimal64:
                    return DataType.Decimal;
                case DataTypePB.Decimal32:
                    return DataType.Decimal;
                case DataTypePB.Double:
                    return DataType.Double;
                case DataTypePB.Float:
                    return DataType.Float;
                case DataTypePB.Int64:
                    return DataType.Long;
                case DataTypePB.Int32:
                    return DataType.Int;
                case DataTypePB.Int16:
                    return DataType.Short;
                case DataTypePB.Int8:
                    return DataType.Byte;
                case DataTypePB.String:
                    return DataType.String;
                case DataTypePB.UnixtimeMicros:
                    return DataType.DateTime;
                default:
                    return DataType.Unknown;
            }
        }

        public override (List<DataCollection>, List<DataEntity>) GetSchema(DataContainer container)
        {
            var collections = new List<DataCollection>();
            var entities = new List<DataEntity>();

            var conn = Connect(container.ConnectionString);
            var tables = conn.GetTablesAsync().Result;

            collections.AddRange(
                tables.Select(x => new DataCollection(container, x.Name))
                );

            foreach (var collection in collections) {
                var schema = conn.GetTableSchemaAsync(collection.Name).Result;

                entities.AddRange(schema.Schema.Columns.Select(x =>
                    new DataEntity(x.Name, ConvertDataType(x.Type), Enum.GetName(typeof(DataTypePB), x.Type), container, collection)));
            }

            return (collections, entities);
        }

        public override bool TestConnection(DataContainer container)
        {
            try {
                var conn = Connect(container.ConnectionString);
                conn.GetTablesAsync().Wait();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

    }
}
