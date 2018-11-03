using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Apache.Ignite.Core;
using Apache.Ignite.Core.Binary;
using Apache.Ignite.Core.Cache;
using Apache.Ignite.Core.Cache.Configuration;
using Apache.Ignite.Core.Discovery.Tcp;
using Apache.Ignite.Core.Discovery.Tcp.Static;
using Apache.Ignite.NLog;
using Apache.Ignite.Sybase.Ingest.Common;
using Apache.Ignite.Sybase.Ingest.Parsers;
using GridGain.Core;
using JetBrains.Annotations;
using Newtonsoft.Json;
using NLog;

namespace Apache.Ignite.Sybase.Ingest.Cache
{
    public static class CacheLoader
    {
        public static void LoadFromPath(string dir)
        {
            var recordDescriptors = CtrlGenParser.ParseAll(dir).ToArray();

            var cfg = GetIgniteConfiguration();
            using (var ignite = Ignition.Start(cfg))
            {
                DeleteCaches(ignite);
                var sw = Stopwatch.StartNew();
                var dataFiles = new ConcurrentBag<DataFileInfo>();

                // ReSharper disable once AccessToDisposedClosure (not an issue).
                Parallel.ForEach(
                    recordDescriptors,
                    new ParallelOptions {MaxDegreeOfParallelism = 20},
                    desc => InvokeLoadCacheGeneric(dir, desc, ignite, dataFiles));

                var elapsed = sw.Elapsed;

                var cacheNames = ignite.GetCacheNames();
                var totalItems = cacheNames.Sum(n => ignite.GetCache<int, int>(n).GetSize());
                var totalGzippedSizeGb = dataFiles.Sum(f => f.CompressedSize) / 1024 / 1024 / 1024;
                var totalSizeGb = dataFiles.Sum(f => f.Size)  / 1024 / 1024 / 1024;

                var log = LogManager.GetLogger(nameof(LoadFromPath));
                log.Info("Loading complete:");
                log.Info($" * {cacheNames.Count} caches");
                log.Info($" * {totalItems} cache entries");
                log.Info($" * {totalGzippedSizeGb} GB of data (gzipped)");
                log.Info($" * {totalSizeGb} GB of data (raw)");
                log.Info($" * {elapsed} elapsed, {totalItems / elapsed.TotalSeconds} entries per second.");
                log.Info($" * {(double) totalGzippedSizeGb / elapsed.TotalSeconds} GB per second gzipped.");
                log.Info($" * {(double) totalSizeGb / elapsed.TotalSeconds} GB per second raw.");
                log.Info($" * {dataFiles.Count} data files loaded: {JsonConvert.ToString(dataFiles.ToArray())}");
            }
        }

        private static void DeleteCaches(IIgnite ignite)
        {
            foreach (var cacheName in ignite.GetCacheNames())
            {
                ignite.DestroyCache(cacheName);
            }
        }

        private static IgniteConfiguration GetIgniteConfiguration()
        {
            return new IgniteConfiguration
            {
                DiscoverySpi = new TcpDiscoverySpi
                {
                    IpFinder = new TcpDiscoveryStaticIpFinder
                    {
                        // Endpoints = new[] {"127.0.0.1:47500..47501"}
                        Endpoints = new[]
                        {
                            "lpwhdjdnd01.npd.com",
                            "lpwhdjdnd02.npd.com",
                            "lpwhdjdnd03.npd.com",
                            "lpwhdjdnd04.npd.com",
                            "lpwhdjdnd05.npd.com",
                            "lpwhdjdnd06.npd.com",
                            "lpwhdjdnd07.npd.com"
                        }
                    },
                    SocketTimeout = TimeSpan.FromSeconds(0.3)
                },
                JvmInitialMemoryMb = 2000,
                JvmMaxMemoryMb = 3000,
                ClientMode = true,
                Logger = new IgniteNLogLogger(),
                PluginConfigurations = new[]
                {
                    new GridGainPluginConfiguration()
                },
                IgniteHome = "/opt/ignite/gridgain-ultimate-fabric-8.4.9"
            };
        }

        /// <summary>
        /// BinaryObject approach does not require model classes, but is less efficient due to dynamic nature.
        /// </summary>
        [UsedImplicitly]
        private static void LoadCacheBinaryObjects(IIgnite ignite, RecordDescriptor desc, string dir)
        {
            var sw = Stopwatch.StartNew();
            long key = 0;

            var (reader, fullPath) = desc.GetInFileStream(dir);

            if (reader == null)
            {
                // File does not exist.
                return;
            }

            Console.WriteLine(fullPath);
            var cacheName = CreateCache(ignite, desc);

            using (reader)
            {
                var binary = ignite.GetBinary();

                using (var streamer = ignite.GetDataStreamer<long, object>(cacheName).WithKeepBinary<long, object>())
                {
                    while (true)
                    {
                        var builder = reader.ReadAsBinaryObject(cacheName, binary);

                        if (builder == null)
                        {
                            break;
                        }

                        var binaryObject = builder.Build();

                        // TODO: How do we determine proper primary key?
                        streamer.AddData(key++, binaryObject);
                    }
                }
            }

            var itemsPerSecond = key * 1000 / sw.ElapsedMilliseconds;
            Console.WriteLine($"Cache '{cacheName}' loaded in {sw.Elapsed}. {key} items, {itemsPerSecond} items/sec");
        }

        /// <summary>
        /// Uses model classes to load the cache.
        /// <see cref="ICanReadFromRecordBuffer"/> is the most efficient way to decode source data.
        /// <see cref="IBinarizable"/> is the most efficient way to pass data to Ignite.
        /// </summary>
        private static void LoadCacheGeneric<T>(IIgnite ignite, RecordDescriptor desc, string dir, ConcurrentBag<DataFileInfo> dataFiles)
            where T : ICanReadFromRecordBuffer, new()
        {
            var log = LogManager.GetLogger(nameof(LoadCacheGeneric));

            try
            {
                var sw = Stopwatch.StartNew();
                long key = 0;

                var paths = desc.GetDataFilePaths(dir);

                if (!paths.Any())
                {
                    log.Error($"Failed to find data files for '{desc.InFile}' in directory '{dir}'.");
                }

                var cache = CreateCacheGeneric<T>(ignite, desc);
                if (cache.GetSize() > 0)
                {
                    log.Warn($"Skipping non-empty cache: {cache.Name}");
                    return;
                }

                foreach (var dataFilePath in paths)
                {
                    log.Info($"Starting {dataFilePath}...");
                    var entryCount = 0;

                    using (var reader = desc.GetBinaryRecordReader(dataFilePath))
                    using (var streamer = ignite.GetDataStreamer<long, T>(cache.Name))
                    {
                        var buffer = new byte[desc.Length];

                        while (reader.Read(buffer))
                        {
                            var entity = new T();
                            entity.ReadFromRecordBuffer(buffer);

                            streamer.AddData(key++, entity);
                            entryCount++;
                        }
                    }

                    var dataFileInfo = new DataFileInfo(
                        dataFilePath,
                        new FileInfo(dataFilePath).Length,
                        (long) entryCount * desc.Length,
                        entryCount);
                    dataFiles.Add(dataFileInfo);
                }

                var itemsPerSecond = key * 1000 / sw.ElapsedMilliseconds;
                log.Info($"Cache '{cache.Name}' loaded in {sw.Elapsed}. {key} items, {itemsPerSecond} items/sec");
            }
            catch (Exception e)
            {
                log.Error(e, $"Failed to load {desc.InFile}: {e}");
            }
        }

        private static void InvokeLoadCacheGeneric(string dir, RecordDescriptor desc, IIgnite ignite,
            ConcurrentBag<DataFileInfo> dataFiles)
        {
            // A bit of reflection won't hurt once per table.
            var typeName = "Apache.Ignite.Sybase.Ingest.Cache." + ModelClassGenerator.GetClassName(desc.TableName);
            var type = Type.GetType(typeName);

            if (type == null)
            {
                throw new Exception("Model class not found: " + typeName);
            }

            var method = typeof(CacheLoader)
                .GetMethod(nameof(LoadCacheGeneric), BindingFlags.Static | BindingFlags.NonPublic);

            var genericMethod = method.MakeGenericMethod(type);

            genericMethod.Invoke(null, new object[] {ignite, desc, dir, dataFiles});
        }

        private static string CreateCache(IIgnite ignite, RecordDescriptor desc)
        {
            // Create new cache (in case when query entities have changed).
            var cacheCfg = CacheConfigurator.GetQueryCacheConfiguration(desc);

            ignite.DestroyCache(cacheCfg.Name);
            ignite.CreateCache<long, object>(cacheCfg);

            return cacheCfg.Name;
        }

        private static ICache<long, T> CreateCacheGeneric<T>(IIgnite ignite, RecordDescriptor desc)
        {
            var cacheCfg = new CacheConfiguration
            {
                Name = desc.TableName,
                QueryEntities = new[]
                {
                    new QueryEntity
                    {
                        TableName = desc.TableName,
                        KeyType = typeof(long),
                        ValueType = typeof(T)
                    }
                },
                EnableStatistics = true
            };

            return ignite.GetOrCreateCache<long, T>(cacheCfg);
        }

        private class DataFileInfo
        {
            public DataFileInfo(string path, long compressedSize, long size, long entries)
            {
                Path = path;
                CompressedSize = compressedSize;
                Size = size;
                Entries = entries;
            }

            public string Path { get; }
            public long CompressedSize { get; }
            public long Size { get; }
            public long Entries { get; }

            public override string ToString()
            {
                return $"{nameof(Path)}: {Path}, {nameof(CompressedSize)}: {CompressedSize}, {nameof(Size)}: {Size}, {nameof(Entries)}: {Entries}";
            }
        }
    }
}
