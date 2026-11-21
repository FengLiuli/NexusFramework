using System;
using System.Collections.Generic;
using System.IO;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.Traits;
using NUnit.Framework;
using UnityDebug = UnityEngine.Debug;

namespace NexusFramework.Core.Tests.Performance
{
    // ═══════════════════════════════════════════════════════════════════
    //  CarrierManager × DataTrait 高频使用路径性能边界
    //
    //  核心问题：游戏每帧对「载体 → Trait」的读写，CarrierManager
    //  的相关方法能撑住多大规模？
    //
    //  覆盖：
    //    ① 写入规模矩阵：载体数 × 每载体 Trait 数
    //    ② 读取帧预算：全量遍历 GetTrait / HasTrait 一次要多久
    //    ③ 混合读写（90% 读 / 10% 写，模拟真实循环）
    //    ④ Add/Remove 抖动（buff、状态频繁增删）
    //    ⑤ 反查：FindCarriersWithTrait / FindCarriersWithTraits
    //    ⑥ 带 Trait 的销毁成本（反向索引清理）
    //    ⑦ GetAllTraits / GetTraitTypes 的拷贝成本
    //    ⑧ 多 typeId 分片是否影响读取
    //
    //  Trait 形态使用项目真实类型（GASEntityTrait / BasicInfoTrait）+ 6 个辅助类型，
    //  以 8 个不同泛型参数覆盖 1/4/8 Trait 矩阵。
    // ═══════════════════════════════════════════════════════════════════

    [Serializable]
    internal class HotTrait0 : DataTrait
    {
        public int v;
    }

    [Serializable]
    internal class HotTrait1 : DataTrait
    {
        public int v;
    }

    [Serializable]
    internal class HotTrait2 : DataTrait
    {
        public int v;
    }

    [Serializable]
    internal class HotTrait3 : DataTrait
    {
        public int v;
    }

    [Serializable]
    internal class HotTrait4 : DataTrait
    {
        public int v;
    }

    [Serializable]
    internal class HotTrait5 : DataTrait
    {
        public int v;
    }

    [TestFixture]
    [Category("Performance")]
    public class CarrierTraitHotPathTests
    {
        private const int MaxTraits = 8;

        private static long _sink;

        private static string ResultFilePath =>
            Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", "TestResults", "carrier-trait-hotpath.txt"));

        [OneTimeSetUp]
        public void ResetResults()
        {
            Perf.Lines.Clear();
            var dir = Path.GetDirectoryName(ResultFilePath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(ResultFilePath,
                $"# CarrierManager x DataTrait hot-path perf (EditMode, Mono/Editor) {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        }

        [OneTimeTearDown]
        public void DumpResults()
        {
            File.WriteAllLines(ResultFilePath, Perf.Lines);
            UnityDebug.Log($"[PERF] results written to {ResultFilePath}");
        }

        // ── 复用实例：与真实业务一致，Trait 实例通常挂载一次、被多次读取 ──
        private static IDataTrait[] NewTraitInstances()
        {
            return new IDataTrait[]
            {
                new HotTrait0 { v = 0 },
                new HotTrait1 { v = 1 },
                new HotTrait2 { v = 2 },
                new HotTrait3 { v = 3 },
                new HotTrait4 { v = 4 },
                new HotTrait5 { v = 5 },
                new GASEntityTrait { GasEntityRaw = 0xABC },
                new BasicInfoTrait("perf-unit", "perf description")
            };
        }

        private static void AddAt(CarrierManager manager, int id, IDataTrait[] traits, int index)
        {
            switch (index)
            {
                case 0: manager.AddTrait(id, (HotTrait0)traits[0]); break;
                case 1: manager.AddTrait(id, (HotTrait1)traits[1]); break;
                case 2: manager.AddTrait(id, (HotTrait2)traits[2]); break;
                case 3: manager.AddTrait(id, (HotTrait3)traits[3]); break;
                case 4: manager.AddTrait(id, (HotTrait4)traits[4]); break;
                case 5: manager.AddTrait(id, (HotTrait5)traits[5]); break;
                case 6: manager.AddTrait(id, (GASEntityTrait)traits[6]); break;
                case 7: manager.AddTrait(id, (BasicInfoTrait)traits[7]); break;
            }
        }

        private static void RemoveAt(CarrierManager manager, int id, int index)
        {
            switch (index)
            {
                case 0: manager.RemoveTrait<HotTrait0>(id); break;
                case 1: manager.RemoveTrait<HotTrait1>(id); break;
                case 2: manager.RemoveTrait<HotTrait2>(id); break;
                case 3: manager.RemoveTrait<HotTrait3>(id); break;
                case 4: manager.RemoveTrait<HotTrait4>(id); break;
                case 5: manager.RemoveTrait<HotTrait5>(id); break;
                case 6: manager.RemoveTrait<GASEntityTrait>(id); break;
                case 7: manager.RemoveTrait<BasicInfoTrait>(id); break;
            }
        }

        private static bool HasAt(CarrierManager manager, int id, int index)
        {
            switch (index)
            {
                case 0: return manager.HasTrait<HotTrait0>(id);
                case 1: return manager.HasTrait<HotTrait1>(id);
                case 2: return manager.HasTrait<HotTrait2>(id);
                case 3: return manager.HasTrait<HotTrait3>(id);
                case 4: return manager.HasTrait<HotTrait4>(id);
                case 5: return manager.HasTrait<HotTrait5>(id);
                case 6: return manager.HasTrait<GASEntityTrait>(id);
                default: return manager.HasTrait<BasicInfoTrait>(id);
            }
        }

        private static bool TryGetAt(CarrierManager manager, int id, int index)
        {
            switch (index)
            {
                case 0: return manager.GetTrait<HotTrait0>(id) != null;
                case 1: return manager.GetTrait<HotTrait1>(id) != null;
                case 2: return manager.GetTrait<HotTrait2>(id) != null;
                case 3: return manager.GetTrait<HotTrait3>(id) != null;
                case 4: return manager.GetTrait<HotTrait4>(id) != null;
                case 5: return manager.GetTrait<HotTrait5>(id) != null;
                case 6: return manager.GetTrait<GASEntityTrait>(id) != null;
                default: return manager.GetTrait<BasicInfoTrait>(id) != null;
            }
        }

        private static CarrierManager NewManager(out int typeId)
        {
            var manager = new CarrierManager();
            manager.Init();
            manager.Initialized = true;
            typeId = manager.RegisterType("PerfUnit");
            return manager;
        }

        private static int[] CreateCarriers(CarrierManager manager, int typeId, int count)
        {
            var ids = new int[count];
            for (var i = 0; i < count; i++) ids[i] = manager.CreateCarrier(typeId);
            return ids;
        }

        private static void AttachTraits(CarrierManager manager, int[] ids, IDataTrait[] traits, int traitCount)
        {
            for (var i = 0; i < ids.Length; i++)
                for (var t = 0; t < traitCount; t++)
                    AddAt(manager, ids[i], traits, t);
        }

        // ─────────────────────────────────────────────────────────────
        //  1. 写入规模矩阵：载体数 × 每载体 Trait 数
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void AddTrait_CarrierCount_x_TraitCount()
        {
            var traits = NewTraitInstances();
            foreach (var carriers in new[] { 10_000, 100_000 })
            {
                foreach (var traitCount in new[] { 1, 4, 8 })
                {
                    var manager = NewManager(out var typeId);
                    var ids = CreateCarriers(manager, typeId, carriers);

                    Perf.Run($"AddTrait carriers={carriers} traits={traitCount}", carriers * traitCount, () =>
                    {
                        for (var i = 0; i < carriers; i++)
                            for (var t = 0; t < traitCount; t++)
                                AddAt(manager, ids[i], traits, t);
                    });

                    manager.Dispose();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  2. 读取帧预算：全量遍历一次
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void ReadTrait_FrameBudget()
        {
            var traits = NewTraitInstances();

            foreach (var carrierCount in new[] { 10_000, 100_000, 1_000_000 })
            {
                var manager = NewManager(out var typeId);
                var ids = CreateCarriers(manager, typeId, carrierCount);
                AttachTraits(manager, ids, traits, 4);

                var iterations = Math.Max(1, 100_000 / carrierCount);
                for (var w = 0; w < iterations && w < 3; w++)
                    _sink += Count(manager.FindCarriersWithTrait<HotTrait0>());

                Perf.Run($"GetTrait all carriers x4 (1 pass) n={carrierCount}",
                    carrierCount * 4 * iterations, () =>
                    {
                        for (var k = 0; k < iterations; k++)
                            for (var i = 0; i < carrierCount; i++)
                                for (var t = 0; t < 4; t++)
                                    if (TryGetAt(manager, ids[i], t)) _sink++;
                    });

                Perf.Run($"HasTrait all carriers x4 (1 pass) n={carrierCount}",
                    carrierCount * 4 * iterations, () =>
                    {
                        for (var k = 0; k < iterations; k++)
                            for (var i = 0; i < carrierCount; i++)
                                for (var t = 0; t < 4; t++)
                                    if (HasAt(manager, ids[i], t)) _sink++;
                    });

                manager.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  3. 混合读写（90% 读 / 10% 写）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void MixedReadWrite_90_10()
        {
            const int carrierCount = 1_000_000;
            const int opsPerCarrier = 4;

            var traits = NewTraitInstances();
            var manager = NewManager(out var typeId);
            var ids = CreateCarriers(manager, typeId, carrierCount);
            AttachTraits(manager, ids, traits, 4);

            Perf.Run($"Mixed 90/10 read/write n={carrierCount * opsPerCarrier}", carrierCount * opsPerCarrier, () =>
            {
                for (var i = 0; i < carrierCount; i++)
                {
                    var id = ids[i];
                    if (i % 10 == 0)
                    {
                        // 写：移除再挂回，模拟状态刷新
                        RemoveAt(manager, id, 0);
                        AddAt(manager, id, traits, 0);
                    }
                    else
                    {
                        for (var t = 0; t < 4; t++)
                            if (TryGetAt(manager, id, t)) _sink++;
                    }
                }
            });

            manager.Dispose();
        }

        // ─────────────────────────────────────────────────────────────
        //  4. Add/Remove 抖动（buff / 状态高频增删）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void AddRemove_Churn()
        {
            const int carrierCount = 10_000;
            const int rounds = 100;

            var traits = NewTraitInstances();
            var manager = NewManager(out var typeId);
            var ids = CreateCarriers(manager, typeId, carrierCount);

            Perf.Run($"Add+Remove churn n={carrierCount} rounds={rounds}", carrierCount * rounds * 2, () =>
            {
                for (var r = 0; r < rounds; r++)
                    for (var i = 0; i < carrierCount; i++)
                    {
                        AddAt(manager, ids[i], traits, 6); // GASEntityTrait
                        RemoveAt(manager, ids[i], 6);
                    }
            });

            manager.Dispose();
        }

        // ─────────────────────────────────────────────────────────────
        //  5. 反查：FindCarriersWithTrait / FindCarriersWithTraits
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void FindCarriersWithTrait_ByResultSize()
        {
            var traits = NewTraitInstances();

            foreach (var carrierCount in new[] { 10_000, 100_000, 1_000_000 })
            {
                var manager = NewManager(out var typeId);
                var ids = CreateCarriers(manager, typeId, carrierCount);
                AttachTraits(manager, ids, traits, 4);

                var iterations = Math.Max(2, 100_000 / carrierCount);
                Perf.Run($"FindCarriersWithTrait result={carrierCount} iterations={iterations}", iterations, () =>
                {
                    for (var k = 0; k < iterations; k++)
                        _sink += Count(manager.FindCarriersWithTrait<HotTrait0>());
                });

                manager.Dispose();
            }
        }

        [Test]
        public void FindCarriersWithTraits_IntersectionSize()
        {
            const int carrierCount = 100_000;
            var traits = NewTraitInstances();
            var manager = NewManager(out var typeId);
            var ids = CreateCarriers(manager, typeId, carrierCount);
            AttachTraits(manager, ids, traits, 4);

            var one = new[] { typeof(HotTrait0) };
            var two = new[] { typeof(HotTrait0), typeof(HotTrait1) };
            var four = new[] { typeof(HotTrait0), typeof(HotTrait1), typeof(HotTrait2), typeof(HotTrait3) };

            foreach (var (label, types) in new[] { ("1 trait", one), ("2 traits", two), ("4 traits", four) })
            {
                Perf.Run($"FindCarriersWithTraits {label} result={carrierCount}", 20, () =>
                {
                    for (var k = 0; k < 20; k++)
                        _sink += Count(manager.FindCarriersWithTraits(types));
                });
            }

            manager.Dispose();
        }

        // ─────────────────────────────────────────────────────────────
        //  6. 带 Trait 的销毁成本（反向索引清理）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void DestroyCarrier_WithTraits()
        {
            const int carrierCount = 100_000;
            var traits = NewTraitInstances();

            foreach (var traitCount in new[] { 0, 4, 8 })
            {
                var manager = NewManager(out var typeId);
                var ids = CreateCarriers(manager, typeId, carrierCount);
                AttachTraits(manager, ids, traits, traitCount);

                Perf.Run($"DestroyCarrier carriers={carrierCount} traits={traitCount}", carrierCount, () =>
                {
                    for (var i = 0; i < carrierCount; i++)
                        manager.DestroyCarrier(ids[i]);
                });

                manager.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  7. GetAllTraits / GetTraitTypes 的拷贝成本
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void GetAllTraits_And_GetTraitTypes()
        {
            const int carrierCount = 100_000;
            var traits = NewTraitInstances();
            var manager = NewManager(out var typeId);
            var ids = CreateCarriers(manager, typeId, carrierCount);
            AttachTraits(manager, ids, traits, 8);

            Perf.Run($"GetAllTraits x8 n={carrierCount}", carrierCount, () =>
            {
                for (var i = 0; i < carrierCount; i++)
                    _sink += Count(manager.GetAllTraits(ids[i]));
            });

            Perf.Run($"GetTraitTypes x8 n={carrierCount}", carrierCount, () =>
            {
                for (var i = 0; i < carrierCount; i++)
                    _sink += Count(manager.GetTraitTypes(ids[i]));
            });

            manager.Dispose();
        }

        // ─────────────────────────────────────────────────────────────
        //  8. 多 typeId 分片是否影响 Trait 读取
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void MultiType_Read()
        {
            const int typeCount = 4;
            const int perType = 25_000;
            const int traitCount = 4;

            var traits = NewTraitInstances();
            var manager = new CarrierManager();
            manager.Init();
            manager.Initialized = true;

            var idsByType = new int[typeCount][];
            for (var t = 0; t < typeCount; t++)
            {
                var typeId = manager.RegisterType($"PerfType{t}");
                idsByType[t] = CreateCarriers(manager, typeId, perType);
                AttachTraits(manager, idsByType[t], traits, traitCount);
            }

            Perf.Run($"MultiType read types={typeCount} carriers={typeCount * perType} traits={traitCount}",
                typeCount * perType * traitCount, () =>
                {
                    for (var t = 0; t < typeCount; t++)
                        for (var i = 0; i < perType; i++)
                            for (var k = 0; k < traitCount; k++)
                                if (TryGetAt(manager, idsByType[t][i], k)) _sink++;
                });

            // 反查不受 typeId 影响，应返回全局结果
            var all = Count(manager.FindCarriersWithTrait<HotTrait0>());
            UnityDebug.Log($"[PERF][CHECK] MultiType FindCarriersWithTrait<HotTrait0> = {all} " +
                           $"(expected {typeCount * perType})");
            Assert.That(all, Is.EqualTo(typeCount * perType));

            manager.Dispose();
        }

        private static long Count<T>(IEnumerable<T> source)
        {
            long count = 0;
            foreach (var _ in source) count++;
            return count;
        }
    }
}
