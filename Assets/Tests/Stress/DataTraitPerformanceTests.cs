using System;
using System.Collections.Generic;
using System.IO;
using NexusFramework.DataCarrier;
using NUnit.Framework;
using UnityDebug = UnityEngine.Debug;

namespace NexusFramework.Core.Tests.Performance
{
    // ═══════════════════════════════════════════════════════════════════
    //  DataTrait 性能边界测试
    //
    //  覆盖：
    //    ① Trait 自身：TraitTypeName / ToJson / FromJson / Clone
    //    ② 字段规模对序列化成本的影响（字符串长度、集合大小）
    //    ③ 聚合路径：CarrierManager.SerializeCarrier / DeserializeCarrier
    //    ④ 存档路径：SaveToFile / LoadFromFile
    //    ⑤ ScriptableObjectDataTrait.Clone（Unity 对象创建成本）
    //
    //  复用的测量工具（Perf / PerfResult）定义在 CarrierPerformanceTests.cs。
    //  ⚠ Editor + Mono 环境；JsonUtility 是 Unity 原生实现，与 IL2CPP 差异较小，
    //    但 GC 与反射成本仍不同。
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>对标 GASEntityTrait：单个 ulong 字段。</summary>
    [Serializable]
    internal class PerfSmallTrait : DataTrait
    {
        public ulong value;
    }

    /// <summary>对标 BasicInfoTrait：3 个 string + 1 个 bool。</summary>
    [Serializable]
    internal class PerfMediumTrait : DataTrait
    {
        public string name = "unit.placeholder";
        public string description = "placeholder description";
        public string iconPath = "UI/Icons/placeholder.png";
        public bool isActive = true;
    }

    /// <summary>宽字段：16 个 int + 4 个 string + 列表。</summary>
    [Serializable]
    internal class PerfLargeTrait : DataTrait
    {
        public int f0, f1, f2, f3, f4, f5, f6, f7;
        public int f8, f9, f10, f11, f12, f13, f14, f15;
        public string s0 = "alpha", s1 = "beta", s2 = "gamma", s3 = "delta";
        public List<int> numbers = new();
    }

    [Serializable]
    internal class PerfStringTrait : DataTrait
    {
        public string text;
    }

    [Serializable]
    internal class PerfCollectionTrait : DataTrait
    {
        public List<int> numbers = new();
    }

    internal class PerfScriptableTrait : ScriptableObjectDataTrait
    {
        public int value;
    }

    [TestFixture]
    [Category("Performance")]
    public class DataTraitPerformanceTests
    {
        private static long _sink;

        private static string ResultFilePath =>
            Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", "TestResults", "datatrait-perf.txt"));

        [OneTimeSetUp]
        public void ResetResults()
        {
            Perf.Lines.Clear();
            var dir = Path.GetDirectoryName(ResultFilePath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(ResultFilePath,
                $"# DataTrait perf (EditMode, Mono/Editor) {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        }

        [OneTimeTearDown]
        public void DumpResults()
        {
            File.WriteAllLines(ResultFilePath, Perf.Lines);
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

        // ─────────────────────────────────────────────────────────────
        //  1. TraitTypeName：GetType().Name 的每次访问成本
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void TraitTypeName_Access()
        {
            var trait = new PerfSmallTrait();

            Perf.Run("TraitTypeName get (1M)", 1_000_000, () =>
            {
                for (var i = 0; i < 1_000_000; i++)
                    _sink += trait.TraitTypeName.Length;
            });
        }

        // ─────────────────────────────────────────────────────────────
        //  2. ToJson / FromJson 随字段规模的退化
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void ToJson_ByShape()
        {
            var small = new PerfSmallTrait { value = 42 };

            var medium = new PerfMediumTrait();

            var large = new PerfLargeTrait();
            for (var i = 0; i < 100; i++) large.numbers.Add(i);

            Perf.Run("ToJson small (1 ulong)", 100_000, () =>
            {
                for (var i = 0; i < 100_000; i++) _sink += small.ToJson().Length;
            });

            Perf.Run("ToJson medium (3 string + bool)", 100_000, () =>
            {
                for (var i = 0; i < 100_000; i++) _sink += medium.ToJson().Length;
            });

            Perf.Run("ToJson large (16 int + 4 string + List[100])", 10_000, () =>
            {
                for (var i = 0; i < 10_000; i++) _sink += large.ToJson().Length;
            });

            UnityDebug.Log($"[PERF][SIZE] json small={small.ToJson().Length}B " +
                           $"medium={medium.ToJson().Length}B large={large.ToJson().Length}B");
        }

        [Test]
        public void FromJson_ByShape()
        {
            var small = new PerfSmallTrait { value = 42 };
            var medium = new PerfMediumTrait();
            var large = new PerfLargeTrait();
            for (var i = 0; i < 100; i++) large.numbers.Add(i);

            var smallJson = small.ToJson();
            var mediumJson = medium.ToJson();
            var largeJson = large.ToJson();

            var smallTarget = new PerfSmallTrait();
            var mediumTarget = new PerfMediumTrait();
            var largeTarget = new PerfLargeTrait();

            Perf.Run("FromJson small", 100_000, () =>
            {
                for (var i = 0; i < 100_000; i++) smallTarget.FromJson(smallJson);
            });

            Perf.Run("FromJson medium", 100_000, () =>
            {
                for (var i = 0; i < 100_000; i++) mediumTarget.FromJson(mediumJson);
            });

            Perf.Run("FromJson large", 10_000, () =>
            {
                for (var i = 0; i < 10_000; i++) largeTarget.FromJson(largeJson);
            });
        }

        // ─────────────────────────────────────────────────────────────
        //  3. Clone 全链路（ToJson + Activator.CreateInstance + FromJson）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Clone_ByShape()
        {
            var small = new PerfSmallTrait { value = 42 };
            var medium = new PerfMediumTrait();
            var large = new PerfLargeTrait();
            for (var i = 0; i < 100; i++) large.numbers.Add(i);

            Perf.Run("Clone small", 10_000, () =>
            {
                for (var i = 0; i < 10_000; i++) _sink += small.Clone() != null ? 1 : 0;
            });

            Perf.Run("Clone medium", 10_000, () =>
            {
                for (var i = 0; i < 10_000; i++) _sink += medium.Clone() != null ? 1 : 0;
            });

            Perf.Run("Clone large", 5_000, () =>
            {
                for (var i = 0; i < 5_000; i++) _sink += large.Clone() != null ? 1 : 0;
            });
        }

        [Test]
        public void Clone_StringLengthScaling()
        {
            foreach (var length in new[] { 16, 256, 4096 })
            {
                var trait = new PerfStringTrait { text = new string('x', length) };
                Perf.Run($"Clone string len={length}", 20_000, () =>
                {
                    for (var i = 0; i < 20_000; i++) _sink += trait.Clone() != null ? 1 : 0;
                });
            }
        }

        [Test]
        public void Clone_CollectionSizeScaling()
        {
            foreach (var count in new[] { 0, 100, 1_000 })
            {
                var trait = new PerfCollectionTrait();
                for (var i = 0; i < count; i++) trait.numbers.Add(i);

                var iterations = Math.Max(500, 50_000 / Math.Max(1, count));
                Perf.Run($"Clone list size={count} iterations={iterations}", iterations, () =>
                {
                    for (var i = 0; i < iterations; i++) _sink += trait.Clone() != null ? 1 : 0;
                });
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  4. ScriptableObjectDataTrait.Clone（Unity 对象创建成本）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void ScriptableObjectTrait_Clone()
        {
            var trait = UnityEngine.ScriptableObject.CreateInstance<PerfScriptableTrait>();
            trait.value = 7;

            const int iterations = 200;
            Perf.Run("ScriptableObjectTrait.Clone", iterations, () =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    var clone = trait.Clone() as UnityEngine.Object;
                    _sink += clone != null ? 1 : 0;
                    if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
                }
            });

            UnityEngine.Object.DestroyImmediate(trait);
        }

        // ─────────────────────────────────────────────────────────────
        //  5. 聚合路径：SerializeCarrier / DeserializeCarrier
        // ─────────────────────────────────────────────────────────────

        private const int TraitsPerCarrier = 4;

        private static int[] CreateCarriersWithTraits(
            CarrierManager manager, int typeId, int count, out string[] json)
        {
            var ids = new int[count];
            json = new string[count];
            var shared = new IDataTrait[]
            {
                new PerfSmallTrait { value = 1 },
                new PerfMediumTrait(),
                new PerfLargeTrait(),
                new PerfStringTrait { text = "payload" }
            };

            for (var i = 0; i < count; i++)
            {
                var id = manager.CreateCarrier(typeId);
                ids[i] = id;
                manager.AddTrait(id, (PerfSmallTrait)shared[0]);
                manager.AddTrait(id, (PerfMediumTrait)shared[1]);
                manager.AddTrait(id, (PerfLargeTrait)shared[2]);
                manager.AddTrait(id, (PerfStringTrait)shared[3]);
                json[i] = manager.SerializeCarrier(id);
            }

            return ids;
        }

        [Test]
        public void SerializeCarrier_Scaling()
        {
            foreach (var n in new[] { 1_000, 10_000, 100_000 })
            {
                var manager = NewManager(out var typeId);
                var ids = CreateCarriersWithTraits(manager, typeId, n, out var json);

                var totalJsonBytes = 0L;
                foreach (var s in json) totalJsonBytes += s.Length;

                Perf.Run($"SerializeCarrier n={n} traits={TraitsPerCarrier}", n, () =>
                {
                    for (var i = 0; i < n; i++)
                        _sink += manager.SerializeCarrier(ids[i]).Length;
                });

                UnityDebug.Log($"[PERF][SIZE] SerializeCarrier n={n} avgJson={totalJsonBytes / n} B/carrier");
                manager.Dispose();
            }
        }

        [Test]
        public void DeserializeCarrier_Scaling()
        {
            foreach (var n in new[] { 1_000, 10_000, 100_000 })
            {
                var source = NewManager(out var typeId);
                CreateCarriersWithTraits(source, typeId, n, out var json);
                source.Dispose();

                var target = NewManager(out var typeId2);
                Assert.That(typeId2, Is.EqualTo(typeId), "两侧类型 ID 必须一致");

                Perf.Run($"DeserializeCarrier n={n}", n, () =>
                {
                    for (var i = 0; i < n; i++)
                        _sink += target.DeserializeCarrier(json[i]) > 0 ? 1 : 0;
                });

                // 记录实际恢复的 Trait 数——Type.GetType 跨程序集解析失败会静默丢失。
                UnityDebug.Log($"[PERF][CHECK] DeserializeCarrier n={n}: " +
                               $"carriers={target.TotalCarrierCount} traits={target.TotalTraitCount} " +
                               $"(expected traits={n * TraitsPerCarrier})");
                target.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  6. 存档路径：SaveToFile / LoadFromFile
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void SaveLoad_FileRoundTrip()
        {
            foreach (var n in new[] { 1_000, 10_000 })
            {
                var manager = NewManager(out var typeId);
                CreateCarriersWithTraits(manager, typeId, n, out _);

                var path = Path.Combine(Path.GetDirectoryName(ResultFilePath) ?? ".", $"datatrait-save-{n}.json");

                Perf.Run($"SaveToFile n={n}", 1, () => manager.SaveToFile(path));
                var fileSize = new FileInfo(path).Length;
                UnityDebug.Log($"[PERF][SIZE] SaveToFile n={n}: {fileSize / 1024.0:F1} KB");

                var target = NewManager(out _);
                Perf.Run($"LoadFromFile n={n}", 1, () => target.LoadFromFile(path));

                UnityDebug.Log($"[PERF][CHECK] LoadFromFile n={n}: carriers={target.TotalCarrierCount} " +
                               $"traits={target.TotalTraitCount}");

                target.Dispose();
                manager.Dispose();
            }
        }
    }
}
