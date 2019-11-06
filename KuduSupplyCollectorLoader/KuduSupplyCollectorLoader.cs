﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Client;
using Kudu.Client.Builder;
using Kudu.Client.Connection;
using Kudu.Client.Protocol.Master;
using S2.BlackSwan.SupplyCollector.Models;
using SupplyCollectorDataLoader;

namespace KuduSupplyCollectorLoader
{
    public class KuduSupplyCollectorLoader : SupplyCollectorDataLoaderBase
    {
        private const string PREFIX = "kudu://";

        private static KuduClient Connect(string connectionString)
        {
            var hostPort = connectionString.Substring(PREFIX.Length).Split(":");
            var host = hostPort[0];
            var port = Int32.Parse(hostPort[1]);

            return new KuduClient(new KuduClientOptions()
            {
                MasterAddresses = new List<HostAndPort>() {
                    new HostAndPort(host, port)
                }
            });
        }

        public override void InitializeDatabase(DataContainer dataContainer) {
            
        }

        private List<ListTablesResponsePB.TableInfo> _tablesList;

        private KuduType ConvertDataType(DataType type) {
            switch (type) {
                case DataType.Boolean:
                    return KuduType.Bool;
                case DataType.Int:
                    return KuduType.Int32;
                case DataType.String:
                    return KuduType.String;
                case DataType.Double:
                    return KuduType.Double;
                case DataType.DateTime:
                    return KuduType.UnixtimeMicros;
                default:
                    throw new ArgumentException($"Unsupported data type {type}");
            }
        }

        private KuduTable CreateTable(KuduClient conn, string name, DataEntity[] columns) {
            if (_tablesList == null) {
                _tablesList = conn.GetTablesAsync().Result;
            }

            if (_tablesList.Any(x => x.Name.Equals(name))) {
                conn.DeleteTableAsync(name).Wait();
            }

            var builder = new TableBuilder();
            builder.SetTableName(name);

            foreach (var column in columns) {
                builder.AddColumn(columnBuilder => {
                    columnBuilder.Name = column.Name;
                    columnBuilder.Type = ConvertDataType(column.DataType);
                });
            }

            return conn.CreateTableAsync(builder).Result;
        }

        public override void LoadSamples(DataEntity[] dataEntities, long count) {
            var conn = Connect(dataEntities[0].Container.ConnectionString);
            var tbl = CreateTable(conn, dataEntities[0].Collection.Name, dataEntities);

            long rows = 0;
            var session = conn.NewSession(new KuduSessionOptions());

            var r = new Random();

            while (rows < count) {
                if (rows % 1000 == 0) {
                    Console.Write(".");
                    session.FlushAsync().Wait();
                }

                var insert = tbl.NewInsert();

                for (int i = 0; i < dataEntities.Length; i++) {
                    switch (dataEntities[i].DataType) {
                        case DataType.Boolean:
                            insert.Row.SetBool(i, r.Next(100) > 50);
                            break;
                        case DataType.Int:
                            insert.Row.SetInt32(i, r.Next());
                            break;
                        case DataType.String:
                            insert.Row.SetString(i, Guid.NewGuid().ToString());
                            break;
                        case DataType.Double:
                            insert.Row.SetDouble(i, r.NextDouble());
                            break;
                        case DataType.DateTime:
                            insert.Row.SetDateTime(i, DateTimeOffset
                                .FromUnixTimeMilliseconds(
                                    DateTimeOffset.Now.ToUnixTimeMilliseconds() + r.Next()).DateTime);
                            break;
                        default:
                            throw new ArgumentException($"Unsupported data type {dataEntities[i].DataType}");
                    }
                }

                session.EnqueueAsync(insert).GetAwaiter().GetResult();
                rows++;
            }
            Console.WriteLine();
            session.FlushAsync().Wait();

            session.DisposeAsync().GetAwaiter().GetResult();
        }

        private void LoadTable(KuduClient conn, string tableName, string filePath)
        {
            var container = new DataContainer();
            var collection = new DataCollection(container, tableName);

            using (var reader = new StreamReader(filePath))
            {
                var header = reader.ReadLine();
                var columnsNames = header.Split(",");

                var tbl = CreateTable(conn, tableName,
                    columnsNames.Select(x => new DataEntity(x, DataType.String, "string", container, collection)).ToArray());

                var session = conn.NewSession(new KuduSessionOptions());

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (String.IsNullOrEmpty(line))
                        continue;

                    var insert = tbl.NewInsert();
                    var cells = line.Split(",");

                    for (int i = 0; i < cells.Length && i < columnsNames.Length; i++)
                    {
                        insert.Row.SetString(i, cells[i]);
                    }

                    session.EnqueueAsync(insert).GetAwaiter().GetResult();
                }

                session.FlushAsync().Wait();

                session.DisposeAsync().GetAwaiter().GetResult();
            }
        }


        public override void LoadUnitTestData(DataContainer dataContainer) {
            var conn = Connect(dataContainer.ConnectionString);

            LoadTable(conn, "emails", "tests/EMAILS.CSV");
        }
    }
}
