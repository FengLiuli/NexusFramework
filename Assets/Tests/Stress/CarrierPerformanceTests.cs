using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NexusFramework.DataCarrier;
using NUnit.Framework;
using UnityEngine.Profiling;
using UnityDebug = UnityEngine.Debug;

namespace NexusFramework.Core.Tests.Performance
{
    // ═══════════════════════════════════════════════════════════════════
    //  Carrier 核心层性能边界测试
    //
    //  测试对象：CarrierManager / CarrierTagService / CarrierIndex /
    //           CarrierGroupIndex —— 不含 GAS 业务层。
    //
    //  指标：批量吞吐（ops/s）、单次延迟（ns/op）、
    //        托管堆分配（B/op）与 GC 次数。
    //
    //  运行方式（EditMode）：
    //    unity command run_tests --mode editor --filter CarrierPerformance --async_tests true
    //
    //  ⚠ 数据来源是 Editor + Mono 环境，不是 IL2CPP Player。
    //    Mono 的字典与 GC 表现与 IL2CPP 有差异，本测试用于
    //    「随规模的退化趋势」与「分配热点定位」，不用于绝对值承诺。
    // ═══════════════════════════════════════════════════════════════════

    [Serializable]
    internal class PerfTraitA : DataTrait
    {
        public int a;
    }

    [Serializable]
    internal class PerfTraitB : DataTrait
    {
        public int b;
    }

    [Serializable]
    internal class PerfTraitC : DataTrait
    {
        public int c;
    }

    [Serializable]
    internal class PerfTraitD : DataTrait
    {
        public int d;
    }

    /// <summary>单次测量的结果。</summary>
    internal readonly struct PerfResult
    {
        public readonly string Name;
        public readonly int Operations;
        public readonly double TotalMs;
        public readonly long AllocatedBytes;
        public readonly int Gen0;
        public readonly int Gen1;
        public readonly int Gen2;

        public PerfResult(string name, int operations, double totalMs, long allocatedBytes,
            int gen0, int gen1, int gen2)
        {
            Name = name;
            Operations = operations;
            TotalMs = totalMs;
            AllocatedBytes = allocatedBytes;
            Gen0 = gen0;
            Gen1 = gen1;
            Gen2 = gen2;
        }

        public double OpsPerSecond => TotalMs > 0 ? Operations * 1000.0 / TotalMs : double.PositiveInfinity;

        public double NanosecondsPerOp => Operations > 0 ? TotalMs * 1_000_000.0 / Operations : 0;

        public double BytesPerOp => Operations > 0 ? (double)AllocatedBytes / Operations : 0;

        public string ToLine()
        {
            var throughput = OpsPerSecond >= 1000 ? $"{OpsPerSecond / 1000:F1} kops/s" : $"{OpsPerSecond:F0} ops/s";
            return $"{Name} | n={Operations} | total={TotalMs:F1}ms | {throughput} | {FormatLatency(NanosecondsPerOp)} " +
                   $"| alloc={BytesPerOp:F1} B/op ({AllocatedBytes / 1024.0:F0} KB) " +
                   $"| GC(0/1/2)=+{Gen0}/+{Gen1}/+{Gen2}";
        }

        private static string FormatLatency(double nanoseconds)
        {
            if (nanoseconds >= 1_000_000) return $"{nanoseconds / 1_000_000:F3} ms/op";
            if (nanoseconds >= 1_000) return $"{nanoseconds / 1_000:F3} us/op";
            return $"{nanoseconds:F1} ns/op";
        }
    }

    internal static class Perf
    {
        // 结果同时落到内存列表，由 fixture 在 OneTimeTearDown 一次性写盘。
        // 编辑器日志在多个 Unity 实例并行时会被覆盖，文件是更可靠的取证方式。
        public static readonly List<string> Lines = new();

        /// <summary>
        ///     托管堆快照。
        ///     Unity 2022.3 Mono 下 GetAllocatedBytesForCurrentThread 存在但恒返回 0，
        ///     GetTotalAllocatedBytes 不存在（已由 AllocationApi_Probe 验证）。
        ///     GetTotalMemory(false) 是唯一可用的计量：测量期间若触发 GC 会低估分配量，
        ///     因此同时记录 GC 次数，用于交叉判断。
        /// </summary>
        private static long AllocatedBytesSnapshot() => GC.GetTotalMemory(false);

        /// <summary>先归零托管堆再测量。body 内的分配与 GC 次数作为结果返回。</summary>
        public static PerfResult Run(string name, int operations, Action body)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocBefore = AllocatedBytesSnapshot();
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            var sw = Stopwatch.StartNew();
            body();
            sw.Stop();

            var delta = AllocatedBytesSnapshot() - allocBefore;
            if (delta < 0) delta = 0; // GetTotalMemory 回退路径可能因回收出现负值

            var result = new PerfResult(name, operations, sw.Elapsed.TotalMilliseconds, delta,
                GC.CollectionCount(0) - gen0,
                GC.CollectionCount(1) - gen1,
                GC.CollectionCount(2) - gen2);

            var line = result.ToLine();
            Lines.Add(line);
            UnityDebug.Log("[PERF] " + line);
            return result;
        }

        public static void LogMemory(string label)
        {
            UnityDebug.Log(
                $"[PERF][MEM] {label} | managed={Profiler.GetMonoUsedSizeLong() / 1024.0 / 1024.0:F1} MB " +
                $"| total={Profiler.GetTotalAllocatedMemoryLong() / 1024.0 / 1024.0:F1} MB");
        }
    }

    [TestFixture]
    [Category("Performance")]
    public class CarrierPerformanceTests
    {
        // 规模阶梯：按需调整。100 万档单轮约需数百 MB 托管堆。
        private static readonly int[] CreateScales = { 10_000, 100_000, 1_000_000 };

        // 防止查询结果被 JIT 视为死代码。
        private static long _sink;

        private static string ResultFilePath =>
            System.IO.Path.GetFullPath(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "TestResults", "carrier-perf.txt"));

        [OneTimeSetUp]
        public void ResetResults()
        {
            Perf.Lines.Clear();
            var dir = System.IO.Path.GetDirectoryName(ResultFilePath);
            if (dir != null) System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(ResultFilePath,
                $"# Carrier core perf (EditMode, Mono/Editor) {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        }

        [OneTimeTearDown]
        public void DumpResults()
        {
            System.IO.File.WriteAllLines(ResultFilePath, Perf.Lines);
            UnityDebug.Log($"[PERF] results written to {ResultFilePath}");
        }

        private static CarrierManager NewManager(out int typeId)
        {
            var manager = new CarrierManager();
            manager.Init();
            manager.Initialized = true;
            typeId = manager.RegisterType("PerfUnit");
            return manager;
        }

        private static CarrierManager NewTaggedManager(out int typeId, out CarrierTagService tags)
        {
            var manager = NewManager(out typeId);
            tags = new CarrierTagService(manager);
            tags.Init();
            return manager;
        }

        private static long Count<T>(IEnumerable<T> source)
        {
            long count = 0;
            foreach (var _ in source) count++;
            return count;
        }

        /// <summary>把一批 int 预先生成好，避免把测试自身的分配计入被测操作。</summary>
        private static int[] CreateCarriers(CarrierManager manager, int typeId, int count)
        {
            var ids = new int[count];
            for (var i = 0; i < count; i++)
                ids[i] = manager.CreateCarrier(typeId);
            return ids;
        }

        // ─────────────────────────────────────────────────────────────
        //  0. 分配计量 API 探测（Unity Mono 的 API 支持情况与桌面 CLR 不同）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void AllocationApi_Probe()
        {
            const int size = 8 * 1024 * 1024;
            var threadMethod = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",
                BindingFlags.Public | BindingFlags.Static);
            var totalMethod = typeof(GC).GetMethod("GetTotalAllocatedBytes",
                BindingFlags.Public | BindingFlags.Static);

            var threadBefore = threadMethod != null ? (long)threadMethod.Invoke(null, null) : -1;
            var totalBefore = totalMethod != null ? (long)totalMethod.Invoke(null, new object[] { false }) : -1;
            var memBefore = GC.GetTotalMemory(false);

            var buffer = new byte[size];
            buffer[0] = 1;

            var threadAfter = threadMethod != null ? (long)threadMethod.Invoke(null, null) : -1;
            var totalAfter = totalMethod != null ? (long)totalMethod.Invoke(null, new object[] { false }) : -1;
            var memAfter = GC.GetTotalMemory(false);

            var line = $"[PROBE] allocated 8 MiB | " +
                       $"GetAllocatedBytesForCurrentThread exists={threadMethod != null} delta={threadAfter - threadBefore} | " +
                       $"GetTotalAllocatedBytes exists={totalMethod != null} delta={totalAfter - totalBefore} | " +
                       $"GetTotalMemory delta={memAfter - memBefore}";
            Perf.Lines.Add(line);
            UnityDebug.Log(line);

            GC.KeepAlive(buffer);
        }

        // ─────────────────────────────────────────────────────────────
        //  1. 创建吞吐
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void CreateCarrier_Scaling()
        {
            foreach (var n in CreateScales)
            {
                var manager = NewManager(out var typeId);
                var ids = new int[n];

                var result = Perf.Run($"CreateCarrier n={n}", n, () =>
                {
                    for (var i = 0; i < n; i++)
                        ids[i] = manager.CreateCarrier(typeId);
                });

                Assert.That(result.Operations, Is.EqualTo(n));
                Assert.That(manager.TotalCarrierCount, Is.EqualTo(n));

                Perf.LogMemory($"created {n} carriers (empty trait map each)");
                manager.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  2. 销毁吞吐（无索引）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void DestroyCarrier_Scaling_NoIndexes()
        {
            foreach (var n in CreateScales)
            {
                var manager = NewManager(out var typeId);
                var ids = CreateCarriers(manager, typeId, n);

                var result = Perf.Run($"DestroyCarrier n={n} indexes=0", n, () =>
                {
                    for (var i = 0; i < n; i++)
                        manager.DestroyCarrier(ids[i]);
                });

                Assert.That(manager.TotalCarrierCount, Is.Zero);
                Perf.LogMemory($"destroyed {n} carriers");
                manager.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  3. 销毁的索引扇出成本
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void DestroyCarrier_IndexFanout()
        {
            const int n = 100_000;
            foreach (var indexCount in new[] { 0, 1, 8, 32 })
            {
                var manager = NewManager(out var typeId);
                var indexes = new List<CarrierIndex<int>>(indexCount);
                for (var k = 0; k < indexCount; k++)
                {
                    var index = new CarrierIndex<int>($"perf.fanout.{k}");
                    manager.RegisterIndex(index);
                    indexes.Add(index);
                }

                var ids = CreateCarriers(manager, typeId, n);
                for (var i = 0; i < n; i++)
                    foreach (var index in indexes)
                        index.Bind(ids[i], i);

                var result = Perf.Run($"DestroyCarrier n={n} indexes={indexCount}", n, () =>
                {
                    for (var i = 0; i < n; i++)
                        manager.DestroyCarrier(ids[i]);
                });

                Assert.That(result.Operations, Is.EqualTo(n));
                Assert.That(manager.TotalCarrierCount, Is.Zero);
                manager.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  4. Trait 增删查吞吐（固定 10 万载体 × 4 个 Trait）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Trait_Operations_100k()
        {
            const int n = 100_000;
            var manager = NewManager(out var typeId);
            var ids = CreateCarriers(manager, typeId, n);

            // 预生成 trait 实例，使 AddTrait 测量只反映框架开销。
            var traits = new IDataTrait[n][];
            for (var i = 0; i < n; i++)
                traits[i] = new IDataTrait[]
                {
                    new PerfTraitA { a = i }, new PerfTraitB { b = i },
                    new PerfTraitC { c = i }, new PerfTraitD { d = i }
                };

            Perf.Run("AddTrait x4", n * 4, () =>
            {
                for (var i = 0; i < n; i++)
                {
                    var row = traits[i];
                    var id = ids[i];
                    manager.AddTrait(id, (PerfTraitA)row[0]);
                    manager.AddTrait(id, (PerfTraitB)row[1]);
                    manager.AddTrait(id, (PerfTraitC)row[2]);
                    manager.AddTrait(id, (PerfTraitD)row[3]);
                }
            });

            Perf.Run("GetTrait x4 (hit)", n * 4, () =>
            {
                for (var i = 0; i < n; i++)
                {
                    var id = ids[i];
                    if (manager.GetTrait<PerfTraitA>(id) != null) _sink++;
                    if (manager.GetTrait<PerfTraitB>(id) != null) _sink++;
                    if (manager.GetTrait<PerfTraitC>(id) != null) _sink++;
                    if (manager.GetTrait<PerfTraitD>(id) != null) _sink++;
                }
            });

            Perf.Run("HasTrait x4 (hit)", n * 4, () =>
            {
                for (var i = 0; i < n; i++)
                {
                    var id = ids[i];
                    if (manager.HasTrait<PerfTraitA>(id)) _sink++;
                    if (manager.HasTrait<PerfTraitB>(id)) _sink++;
                    if (manager.HasTrait<PerfTraitC>(id)) _sink++;
                    if (manager.HasTrait<PerfTraitD>(id)) _sink++;
                }
            });

            Perf.Run("GetAllTraits (4 traits each)", n, () =>
            {
                for (var i = 0; i < n; i++)
                    _sink += Count(manager.GetAllTraits(ids[i]));
            });

            Perf.Run("RemoveTrait x4", n * 4, () =>
            {
                for (var i = 0; i < n; i++)
                {
                    var id = ids[i];
                    manager.RemoveTrait<PerfTraitA>(id);
                    manager.RemoveTrait<PerfTraitB>(id);
                    manager.RemoveTrait<PerfTraitC>(id);
                    manager.RemoveTrait<PerfTraitD>(id);
                }
            });

            Perf.LogMemory($"trait ops done on {n} carriers");
            manager.Dispose();
        }

        // ─────────────────────────────────────────────────────────────
        //  5. 反向索引查询随结果集规模的退化
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void TraitQuery_Scaling()
        {
            foreach (var n in CreateScales)
            {
                var manager = NewManager(out var typeId);
                var ids = CreateCarriers(manager, typeId, n);
                var shared = new PerfTraitA();
                for (var i = 0; i < n; i++)
                    manager.AddTrait(ids[i], shared);

                var iterations = Math.Max(5, 2_000_000 / n);
                for (var i = 0; i < Math.Min(iterations, 5); i++)
                    _sink += Count(manager.FindCarriersWithTrait<PerfTraitA>());

                var result = Perf.Run($"FindCarriersWithTrait result={n} iterations={iterations}", iterations,
                    () =>
                    {
                        for (var i = 0; i < iterations; i++)
                            _sink += Count(manager.FindCarriersWithTrait<PerfTraitA>());
                    });

                Assert.That(result.Operations, Is.EqualTo(iterations));
                manager.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  6. 全量枚举与统计的分配成本
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Enumerate_Scaling()
        {
            foreach (var n in CreateScales)
            {
                var manager = NewManager(out var typeId);
                var ids = CreateCarriers(manager, typeId, n);
                var shared = new PerfTraitB();
                for (var i = 0; i < n; i++)
                    manager.AddTrait(ids[i], shared);

                var iterations = Math.Max(5, 2_000_000 / n);
                for (var i = 0; i < Math.Min(iterations, 5); i++)
                    _sink += Count(manager.GetCarriersByType(typeId));

                Perf.Run($"GetCarriersByType n={n} iterations={iterations}", iterations, () =>
                {
                    for (var i = 0; i < iterations; i++)
                        _sink += Count(manager.GetCarriersByType(typeId));
                });

                Perf.Run($"GetAllCarriers n={n} iterations={iterations}", iterations, () =>
                {
                    for (var i = 0; i < iterations; i++)
                        _sink += Count(manager.GetAllCarriers());
                });

                Perf.Run($"TotalTraitCount n={n} iterations={iterations}", iterations, () =>
                {
                    for (var i = 0; i < iterations; i++)
                        _sink += manager.TotalTraitCount;
                });

                Perf.Run($"GetCarrierCount(type) n={n} iterations={iterations}", iterations, () =>
                {
                    for (var i = 0; i < iterations; i++)
                        _sink += manager.GetCarrierCount(typeId);
                });

                manager.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  7. Tag 写入 / 查询吞吐
        // ─────────────────────────────────────────────────────────────

        private const int TagCarrierCount = 10_000;
        private const int TagsPerCarrier = 10;

        private static string TagKeyOf(int index)
        {
            return $"perf.{index / 100}.{index / 10 % 10}.{index % 10}";
        }

        [Test]
        public void Tag_AddAndCheck_Throughput()
        {
            var manager = NewTaggedManager(out var typeId, out var tags);
            var ids = CreateCarriers(manager, typeId, TagCarrierCount);

            var keys = new string[TagsPerCarrier];
            for (var i = 0; i < TagsPerCarrier; i++)
                keys[i] = TagKeyOf(i);

            // 第一次添加：含 TagKey.Normalize + TagRuntimeIndex.Intern（递归建祖先）。
            Perf.Run("AddTag (first, depth=3)", TagCarrierCount * TagsPerCarrier, () =>
            {
                for (var i = 0; i < TagCarrierCount; i++)
                    for (var t = 0; t < TagsPerCarrier; t++)
                        tags.AddTag(ids[i], keys[t]);
            });

            // 重复添加：走 Duplicate 路径，仍要 Normalize + 字典查找。
            Perf.Run("AddTag (duplicate)", TagCarrierCount * TagsPerCarrier, () =>
            {
                for (var i = 0; i < TagCarrierCount; i++)
                    for (var t = 0; t < TagsPerCarrier; t++)
                        tags.AddTag(ids[i], keys[t]);
            });

            // 叶子命中：遍历载体持有的 slot 集合做祖先匹配。
            Perf.Run("HasTag (leaf hit)", TagCarrierCount * TagsPerCarrier, () =>
            {
                for (var i = 0; i < TagCarrierCount; i++)
                    for (var t = 0; t < TagsPerCarrier; t++)
                        if (tags.HasTag(ids[i], keys[t])) _sink++;
            });

            // 父级命中：query 是祖先，需对每个 slot 做祖先链比较。
            Perf.Run("HasTag (ancestor hit)", TagCarrierCount, () =>
            {
                for (var i = 0; i < TagCarrierCount; i++)
                    if (tags.HasTag(ids[i], "perf")) _sink++;
            });

            // 未注册键：TryGetSlot 失败即返回，属于快路径。
            Perf.Run("HasTag (unknown key, early out)", TagCarrierCount, () =>
            {
                for (var i = 0; i < TagCarrierCount; i++)
                    if (tags.HasTag(ids[i], "perf.zzz")) _sink++;
            });

            Perf.Run("GetTags (10 each)", TagCarrierCount, () =>
            {
                for (var i = 0; i < TagCarrierCount; i++)
                    _sink += Count(tags.GetTags(ids[i]));
            });

            Perf.Run("RemoveTag (first)", TagCarrierCount * TagsPerCarrier, () =>
            {
                for (var i = 0; i < TagCarrierCount; i++)
                    for (var t = 0; t < TagsPerCarrier; t++)
                        tags.RemoveTag(ids[i], keys[t]);
            });

            Perf.LogMemory($"tag ops done on {TagCarrierCount} carriers");
            manager.Dispose();
        }

        // ─────────────────────────────────────────────────────────────
        //  8. Tag 反查随槽位规模的退化（预期 O(#槽位)）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Tag_FindCarriersWithTag_SlotScaling()
        {
            const int carriersPerSlot = 10;
            foreach (var slotCount in new[] { 100, 1_000, 10_000 })
            {
                var manager = NewTaggedManager(out var typeId, out var tags);
                var ids = CreateCarriers(manager, typeId, slotCount * carriersPerSlot);

                for (var s = 0; s < slotCount; s++)
                    for (var c = 0; c < carriersPerSlot; c++)
                        tags.AddTag(ids[s * carriersPerSlot + c], $"perf.{s:x}");

                var iterations = Math.Max(10, 1_000_000 / slotCount);
                for (var i = 0; i < Math.Min(iterations, 20); i++)
                    _sink += Count(tags.FindCarriersWithTag("perf.0"));

                Perf.Run($"FindCarriersWithTag slots={slotCount} result={carriersPerSlot} iterations={iterations}",
                    iterations, () =>
                    {
                        for (var i = 0; i < iterations; i++)
                            _sink += Count(tags.FindCarriersWithTag($"perf.{i % slotCount:x}"));
                    });

                manager.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  9. 组合 Tag 查询（All / Any / None）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Tag_FindCarriersWithTags_Combinations()
        {
            const int carriers = 20_000;
            var manager = NewTaggedManager(out var typeId, out var tags);
            var ids = CreateCarriers(manager, typeId, carriers);

            for (var i = 0; i < carriers; i++)
            {
                tags.AddTag(ids[i], "perf.all.a");
                if (i % 2 == 0) tags.AddTag(ids[i], "perf.any.b");
                if (i % 3 == 0) tags.AddTag(ids[i], "perf.none.c");
            }

            var allQuery = new TagQuery(all: new[] { "perf.all.a", "perf.any.b" });
            var anyQuery = new TagQuery(any: new[] { "perf.any.b", "perf.none.c" });
            var noneQuery = new TagQuery(all: new[] { "perf.all.a" }, none: new[] { "perf.none.c" });

            const int iterations = 20;
            Perf.Run("FindCarriersWithTags ALL(2)", iterations, () =>
            {
                for (var i = 0; i < iterations; i++)
                    _sink += Count(tags.FindCarriersWithTags(allQuery));
            });

            Perf.Run("FindCarriersWithTags ANY(2)", iterations, () =>
            {
                for (var i = 0; i < iterations; i++)
                    _sink += Count(tags.FindCarriersWithTags(anyQuery));
            });

            Perf.Run("FindCarriersWithTags ALL+NONE", iterations, () =>
            {
                for (var i = 0; i < iterations; i++)
                    _sink += Count(tags.FindCarriersWithTags(noneQuery));
            });

            manager.Dispose();
        }

        // ─────────────────────────────────────────────────────────────
        //  10. CarrierIndex 写入 / 查询吞吐
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void CarrierIndex_Operations_100k()
        {
            const int n = 100_000;
            var manager = NewManager(out var typeId);
            var ids = CreateCarriers(manager, typeId, n);

            var index = new CarrierIndex<int>("perf.index.bind");
            manager.RegisterIndex(index);

            Perf.Run("CarrierIndex.Bind (no conflict)", n, () =>
            {
                for (var i = 0; i < n; i++)
                    index.Bind(ids[i], i);
            });

            Perf.Run("CarrierIndex.TryGet (hit)", n, () =>
            {
                for (var i = 0; i < n; i++)
                    if (index.TryGet(ids[i], out var v)) _sink += v;
            });

            Perf.Run("CarrierIndex.TryGetCarrier (reverse hit)", n, () =>
            {
                for (var i = 0; i < n; i++)
                    _sink += index.TryGetCarrier(i);
            });

            Perf.Run("CarrierIndex.Unbind", n, () =>
            {
                for (var i = 0; i < n; i++)
                    index.Unbind(ids[i]);
            });

            // 值冲突驱逐：全部绑定到同一个值，每次都会驱逐上一个持有者。
            Perf.Run("CarrierIndex.Bind (conflict, evict)", n, () =>
            {
                for (var i = 0; i < n; i++)
                    index.Bind(ids[i], 42);
            });

            Assert.That(index.EntryCount, Is.EqualTo(1));
            manager.Dispose();
        }

        // ─────────────────────────────────────────────────────────────
        //  11. CarrierGroupIndex 写入 / 查询吞吐
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void CarrierGroupIndex_Operations_100k()
        {
            const int n = 100_000;
            const int groupCount = 100;
            var manager = NewManager(out var typeId);
            var ids = CreateCarriers(manager, typeId, n);

            var group = new CarrierGroupIndex<int>("perf.group.flags");
            manager.RegisterIndex(group);

            Perf.Run("CarrierGroupIndex.Add (100 groups)", n, () =>
            {
                for (var i = 0; i < n; i++)
                    group.Add(i % groupCount, ids[i]);
            });

            // Get 每次都拷贝整个分组（1000 个成员），故只取 1 万次。
            Perf.Run("CarrierGroupIndex.Get (single group, 10k iters)", 10_000, () =>
            {
                for (var i = 0; i < 10_000; i++)
                    _sink += Count(group.Get(i % groupCount));
            });

            Perf.Run("CarrierGroupIndex.KeysOf", n, () =>
            {
                for (var i = 0; i < n; i++)
                    _sink += Count(group.KeysOf(ids[i]));
            });

            Perf.Run("CarrierGroupIndex.RemoveCarrierFromAll", n, () =>
            {
                for (var i = 0; i < n; i++)
                    group.RemoveCarrierFromAll(ids[i]);
            });

            Assert.That(group.EntryCount, Is.Zero);
            manager.Dispose();
        }
    }
}
