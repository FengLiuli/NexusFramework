using System;
using System.Collections.Generic;
using System.IO;
using NexusFramework.DataCarrier;
using NUnit.Framework;
using UnityDebug = UnityEngine.Debug;

namespace NexusFramework.Core.Tests.Performance
{
    // ═══════════════════════════════════════════════════════════════════
    //  工厂类游戏场景基准：1000 种物品原型 / 10000 个容器 / 每容器 100 物品
    //
    //  数据模型（严格按业务约定）：
    //    - 物品原型（1000 种）走配置表，不进 Carrier；
    //    - 物品实例 = Carrier + FactoryItemTrait{definitionId, count, state}；
    //    - 容器 = Carrier + FactoryContainerTrait{capacity, kind}；
    //    - 归属 = CarrierGroupIndex<int>：容器 → {物品}（反向账本给出物品→容器）。
    //
    //  回答六个问题：
    //    Q1 冷数据构建：1M 物品要多久、多少内存
    //    Q2 活跃容器 tick：每帧只更新活跃容器是否可控
    //    Q3 搬运/生成销毁：高频操作的单价与 GC
    //    Q4 查询：全扫 vs 业务索引
    //    Q5 存档：逐物品 / 容器批量 JSON / 二进制紧凑 三种方式的耗时与体积
    //    Q6 冷热分离：Carrier 全量 vs 紧凑 struct 数组的内存差
    // ═══════════════════════════════════════════════════════════════════

    [Serializable]
    internal class FactoryItemTrait : DataTrait
    {
        public int definitionId;
        public int count;
        public int state;
    }

    [Serializable]
    internal class FactoryContainerTrait : DataTrait
    {
        public int capacity;
        public int kind;
    }

    [Serializable]
    internal class ItemSaveRecord
    {
        public int defId;
        public int count;
        public int state;
    }

    [Serializable]
    internal class ContainerSaveRecord
    {
        public int containerKind;
        public ItemSaveRecord[] items;
    }

    /// <summary>冷热分离的对照实现：一个物品只占 12 B，不含任何容器开销。</summary>
    internal readonly struct CompactItemSlot
    {
        public readonly int DefinitionId;
        public readonly int Count;
        public readonly int State;

        public CompactItemSlot(int definitionId, int count, int state)
        {
            DefinitionId = definitionId;
            Count = count;
            State = state;
        }
    }

    [TestFixture]
    [Category("Performance")]
    public class FactoryScenarioTests
    {
        private const int ContainerCount = 10_000;
        private const int ItemsPerContainer = 100;
        private const int ItemCount = ContainerCount * ItemsPerContainer; // 1,000,000

        private static CarrierManager _manager;
        private static CarrierGroupIndex<int> _containerItems;
        private static CarrierGroupIndex<int> _byDefinition;
        private static int[] _containers;
        private static int[] _items;
        private static int _itemType;
        private static int _containerType;
        private static long _carrierMemoryBytes;
        private static long _sink;

        private static string ResultFilePath =>
            Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", "TestResults", "factory-scenario.txt"));

        [OneTimeSetUp]
        public void BuildWorld()
        {
            Perf.Lines.Clear();
            var dir = Path.GetDirectoryName(ResultFilePath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(ResultFilePath,
                $"# Factory scenario perf (EditMode, Mono/Editor) {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

            _manager = new CarrierManager();
            _manager.Init();
            _manager.Initialized = true;
            _containerType = _manager.RegisterType("FactoryContainer");
            _itemType = _manager.RegisterType("FactoryItem");

            _containerItems = new CarrierGroupIndex<int>("factory.container.items");
            _byDefinition = new CarrierGroupIndex<int>("factory.definition.items");
            _manager.RegisterIndex(_containerItems);
            _manager.RegisterIndex(_byDefinition);

            _containers = new int[ContainerCount];
            _items = new int[ItemCount];

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memBefore = GC.GetTotalMemory(true);

            Perf.Run($"Build {ContainerCount} containers", ContainerCount, () =>
            {
                var trait = new FactoryContainerTrait { capacity = ItemsPerContainer, kind = 1 };
                for (var c = 0; c < ContainerCount; c++)
                {
                    var container = _manager.CreateCarrier(_containerType);
                    _containers[c] = container;
                    _manager.AddTrait(container, trait);
                }
            });

            Perf.Run($"Build {ItemCount} items (Create+Trait+2 indexes)", ItemCount, () =>
            {
                for (var i = 0; i < ItemCount; i++)
                {
                    var item = _manager.CreateCarrier(_itemType);
                    _items[i] = item;

                    var defId = i % 1000;
                    _manager.AddTrait(item, new FactoryItemTrait
                    {
                        definitionId = defId,
                        count = 1 + i % 64,
                        state = 0
                    });

                    var container = _containers[i / ItemsPerContainer];
                    _containerItems.Add(container, item);
                    _byDefinition.Add(defId, item);
                }
            });

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memAfter = GC.GetTotalMemory(true);

            _carrierMemoryBytes = memAfter - memBefore;
            UnityDebug.Log($"[PERF][MEM] Carrier world: {ContainerCount} containers + {ItemCount} items " +
                           $"= {_carrierMemoryBytes / 1024.0 / 1024.0:F1} MB " +
                           $"({(double)_carrierMemoryBytes / ItemCount:F0} B/item, incl. containers & indexes)");
        }

        [OneTimeTearDown]
        public void DisposeWorld()
        {
            File.WriteAllLines(ResultFilePath, Perf.Lines);
            UnityDebug.Log($"[PERF] results written to {ResultFilePath}");
            _manager?.Dispose();
            _manager = null;
        }

        private static long Count<T>(IEnumerable<T> source)
        {
            long count = 0;
            foreach (var _ in source) count++;
            return count;
        }

        // ─────────────────────────────────────────────────────────────
        //  Q4-A 单容器读取（UI 打开一个箱子）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Read_SingleContainer()
        {
            const int sampleContainers = 1_000;
            var rng = new Random(12345);

            Perf.Run($"Read single container (100 items) x{sampleContainers}", sampleContainers, () =>
            {
                for (var k = 0; k < sampleContainers; k++)
                {
                    var container = _containers[rng.Next(ContainerCount)];
                    foreach (var item in _containerItems.Get(container))
                    {
                        var trait = _manager.GetTrait<FactoryItemTrait>(item);
                        if (trait != null) _sink += trait.count;
                    }
                }
            });
        }

        // ─────────────────────────────────────────────────────────────
        //  Q4-B 容器全量扫描（背包列表 / 仓库总览）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Read_AllContainers()
        {
            Perf.Run($"Read all {ContainerCount} containers (100 items each)", ContainerCount, () =>
            {
                long total = 0;
                for (var c = 0; c < ContainerCount; c++)
                {
                    foreach (var item in _containerItems.Get(_containers[c]))
                    {
                        var trait = _manager.GetTrait<FactoryItemTrait>(item);
                        if (trait != null) total += trait.count;
                    }
                }
                _sink += total;
            });
        }

        // ─────────────────────────────────────────────────────────────
        //  Q2 活跃容器 tick：每帧只更新有输入输出的机器
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Tick_ActiveContainers()
        {
            foreach (var active in new[] { 10, 100, 1_000 })
            {
                var iterations = Math.Max(5, 5_000 / active);
                Perf.Run($"Tick active containers={active} (100 items each)", active * ItemsPerContainer * iterations, () =>
                {
                    for (var k = 0; k < iterations; k++)
                        for (var c = 0; c < active; c++)
                        {
                            var container = _containers[c];
                            foreach (var item in _containerItems.Get(container))
                            {
                                var trait = _manager.GetTrait<FactoryItemTrait>(item);
                                if (trait != null && trait.count > 0) _sink++;
                            }
                        }
                });
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Q4-C 按定义统计：全扫 vs 业务索引
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Query_ByDefinition_FullScan_vs_Index()
        {
            const int targetDefinition = 42;
            const int indexIterations = 100;

            Perf.Run("Full scan 1M items by definition", 1, () =>
            {
                long count = 0;
                for (var i = 0; i < ItemCount; i++)
                {
                    var trait = _manager.GetTrait<FactoryItemTrait>(_items[i]);
                    if (trait != null && trait.definitionId == targetDefinition) count++;
                }
                _sink += count;
            });

            Perf.Run($"Index query by definition (1000 results) x{indexIterations}", indexIterations, () =>
            {
                for (var k = 0; k < indexIterations; k++)
                    _sink += Count(_byDefinition.Get(targetDefinition));
            });
        }

        // ─────────────────────────────────────────────────────────────
        //  Q5-A 存档：逐物品 SerializeCarrier（抽样 10 万，避免 600 MB 字符串）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Save_PerItemSerialization_Sampled()
        {
            const int sample = 100_000;
            long totalChars = 0;

            Perf.Run($"SerializeCarrier per item (sample {sample})", sample, () =>
            {
                for (var i = 0; i < sample; i++)
                    totalChars += _manager.SerializeCarrier(_items[i]).Length;
            });

            UnityDebug.Log($"[PERF][SIZE] Per-item JSON: avg={totalChars / (double)sample:F0} B/item, " +
                           $"extrapolated 1M={(long)(totalChars / (double)sample * ItemCount) / 1024 / 1024} MB");
        }

        // ─────────────────────────────────────────────────────────────
        //  Q5-B 存档：容器批量紧凑 JSON
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Save_ContainerBatchJson()
        {
            long totalBytes = 0;
            var records = new ContainerSaveRecord[ContainerCount];

            Perf.Run($"Container-batch JSON ({ContainerCount} containers x 100 items)", ContainerCount, () =>
            {
                for (var c = 0; c < ContainerCount; c++)
                {
                    var record = new ContainerSaveRecord
                    {
                        containerKind = 1,
                        items = new ItemSaveRecord[ItemsPerContainer]
                    };

                    var index = 0;
                    foreach (var item in _containerItems.Get(_containers[c]))
                    {
                        var trait = _manager.GetTrait<FactoryItemTrait>(item);
                        record.items[index++] = new ItemSaveRecord
                        {
                            defId = trait.definitionId,
                            count = trait.count,
                            state = trait.state
                        };
                    }

                    records[c] = record;
                }
            });

            // 序列化单独计时，避免把数据组装算进 JSON 成本
            Perf.Run($"JsonUtility.ToJson per container x{ContainerCount}", ContainerCount, () =>
            {
                for (var c = 0; c < ContainerCount; c++)
                    totalBytes += UnityEngine.JsonUtility.ToJson(records[c]).Length;
            });

            UnityDebug.Log($"[PERF][SIZE] Container-batch JSON total={totalBytes / 1024.0 / 1024.0:F1} MB " +
                           $"({totalBytes / (double)ItemCount:F0} B/item)");
        }

        // ─────────────────────────────────────────────────────────────
        //  Q5-C 存档：二进制紧凑格式
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Save_BinaryCompact()
        {
            long bytes = 0;

            Perf.Run($"Binary compact save ({ContainerCount} containers x 100 items)", ContainerCount, () =>
            {
                using var stream = new MemoryStream(ItemCount * 12);
                using var writer = new BinaryWriter(stream);
                for (var c = 0; c < ContainerCount; c++)
                {
                    writer.Write(1); // container kind
                    writer.Write(ItemsPerContainer);
                    foreach (var item in _containerItems.Get(_containers[c]))
                    {
                        var trait = _manager.GetTrait<FactoryItemTrait>(item);
                        writer.Write(trait.definitionId);
                        writer.Write(trait.count);
                        writer.Write(trait.state);
                    }
                }
                bytes = stream.Length;
            });

            UnityDebug.Log($"[PERF][SIZE] Binary compact total={bytes / 1024.0 / 1024.0:F1} MB " +
                           $"({bytes / (double)ItemCount:F1} B/item)");
        }

        // ─────────────────────────────────────────────────────────────
        //  Q6 冷热分离：Carrier 全量 vs 紧凑 struct 数组
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Memory_Compare_Carrier_vs_CompactArray()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetTotalMemory(true);

            var slots = new CompactItemSlot[ItemCount];
            for (var i = 0; i < ItemCount; i++)
                slots[i] = new CompactItemSlot(i % 1000, 1 + i % 64, 0);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var after = GC.GetTotalMemory(true);
            var compactBytes = after - before;

            UnityDebug.Log($"[PERF][MEM] Carrier world={_carrierMemoryBytes / 1024.0 / 1024.0:F1} MB | " +
                           $"CompactItemSlot[1M]={compactBytes / 1024.0 / 1024.0:F1} MB " +
                           $"({(double)compactBytes / ItemCount:F0} B/item) | " +
                           $"ratio={_carrierMemoryBytes / (double)Math.Max(1, compactBytes):F1}x");

            GC.KeepAlive(slots);
        }

        // ─────────────────────────────────────────────────────────────
        //  Q3-A 物品搬运：容器 A → B（Remove + Add）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Move_Items()
        {
            const int moveCount = 10_000;
            var manager = NewManager(out var containerType, out var itemType, out var group, out var defIndex);
            var containers = new int[1_000];
            for (var c = 0; c < containers.Length; c++)
            {
                containers[c] = manager.CreateCarrier(containerType);
                manager.AddTrait(containers[c], new FactoryContainerTrait { capacity = 100, kind = 1 });
            }

            var items = new int[moveCount];
            var trait = new FactoryItemTrait { definitionId = 1, count = 1, state = 0 };
            for (var i = 0; i < moveCount; i++)
            {
                var item = manager.CreateCarrier(itemType);
                items[i] = item;
                manager.AddTrait(item, trait);
                group.Add(containers[i / 100], item);
                defIndex.Add(1, item);
            }

            Perf.Run($"Move items across containers x{moveCount}", moveCount, () =>
            {
                for (var i = 0; i < moveCount; i++)
                {
                    var from = containers[i / 100];
                    var to = containers[i / 100 + 1];
                    group.Remove(from, items[i]);
                    group.Add(to, items[i]);
                    _sink += group.KeysOf(items[i]).Count;
                }
            });

            manager.Dispose();
        }

        // ─────────────────────────────────────────────────────────────
        //  Q3-B 高频生成 / 销毁（传送带、掉落、制造）
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void CreateDestroy_Items()
        {
            foreach (var ops in new[] { 1_000, 10_000 })
            {
                var manager = NewManager(out var containerType, out var itemType, out var group, out var defIndex);
                var container = manager.CreateCarrier(containerType);
                manager.AddTrait(container, new FactoryContainerTrait { capacity = 100, kind = 1 });

                Perf.Run($"Create+Attach+Destroy items x{ops}", ops, () =>
                {
                    for (var i = 0; i < ops; i++)
                    {
                        var item = manager.CreateCarrier(itemType);
                        manager.AddTrait(item, new FactoryItemTrait
                        {
                            definitionId = i % 1000,
                            count = 1,
                            state = 0
                        });
                        group.Add(container, item);
                        defIndex.Add(i % 1000, item);
                        manager.DestroyCarrier(item);
                    }
                });

                manager.Dispose();
            }
        }

        private static CarrierManager NewManager(
            out int containerType, out int itemType,
            out CarrierGroupIndex<int> group, out CarrierGroupIndex<int> defIndex)
        {
            var manager = new CarrierManager();
            manager.Init();
            manager.Initialized = true;
            containerType = manager.RegisterType("FactoryContainer");
            itemType = manager.RegisterType("FactoryItem");
            group = new CarrierGroupIndex<int>("factory.container.items");
            defIndex = new CarrierGroupIndex<int>("factory.definition.items");
            manager.RegisterIndex(group);
            manager.RegisterIndex(defIndex);
            return manager;
        }
    }
}
