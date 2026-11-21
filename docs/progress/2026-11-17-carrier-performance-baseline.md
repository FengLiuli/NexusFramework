---
layer: progress
status: done
created: 2026-11-17
updated: 2026-11-17
---

# Carrier 核心层性能边界基线（2026-11-17）

> 配套文档：Trait 与序列化见
> [2026-11-17-datatrait-performance-baseline.md](2026-11-17-datatrait-performance-baseline.md)；
> 待修问题汇总见 [2026-11-17-carrier-issues-backlog.md](2026-11-17-carrier-issues-backlog.md)。

## 一、结论速览

| 问题 | 结论 |
|---|---|
| 100 万 Carrier 能不能撑住 | **能**。创建 558 ms、销毁 458 ms，托管堆增量约 **170 MB**（≈173 B/载体） |
| 单次操作最快/最慢 | Trait 查询 139 ns；Tag 反查（1 万槽位）**2.9 ms/次** |
| 真正的瓶颈 | 不是容量，而是 **返回集合的 API 全量拷贝**、**Tag 反查全槽位扫描**、**索引数量对销毁的线性放大** |
| 帧预算（16 ms）内能做什么 | 1M 规模下：全量枚举 1 次、Trait 反查 1 次；Tag 反查在 1 万槽位时约 5 次 |

**最值得改的三处**：

1. `FindCarriersWithTag` 遍历 `carriersBySlot` 全部槽位 —— 应只查目标槽位及其祖先链（见 §5.1）。
2. `TagKey.Normalize` 在一条调用链上被执行两次，每次 `Split + Trim + Join` —— 每次 Tag 读/写固定 223 B 分配（§5.2）。
3. 每个 Carrier 构造即分配空 `Dictionary<Type, IDataTrait>` —— 占总分配近一半（§5.3）。

## 二、测试环境与方法

- **环境**：Unity 2022.3.62f2c1（中国版），Editor / Mono，API Compatibility `.NET Standard 2.1`，Windows 10，16 GB 内存。
- **干扰**：测试期间同机另有一个 Unity 实例（`map2 (1)`）运行，实测占用约 16% 单核。绝对值可能偏高 5%~15%，趋势不受影响。
- **计时**：`Stopwatch`，每次测量前 `GC.Collect()` 归零。
- **分配**：`GC.GetTotalMemory(false)` 前后差。**Unity Mono 下 `GC.GetAllocatedBytesForCurrentThread` 存在但恒返回 0，`GC.GetTotalAllocatedBytes` 不存在**（已由 `AllocationApi_Probe` 验证）。若测量期间触发 GC，分配量会被低估，故同时记录 GC 次数交叉判断。
- **代码**：`Assets/Tests/Stress/CarrierPerformanceTests.cs`（`Category("Performance")`，12 个测试）。

## 三、数据明细

> 吞吐按单线程计算；「分配/次」为每次 API 调用的托管分配。

### 3.1 创建 / 销毁

| 操作 | 1 万 | 10 万 | 100 万 |
|---|---|---|---|
| `CreateCarrier` 延迟 | 386 ns | 318 ns | 558 ns |
| `CreateCarrier` 分配 | 165 B | 166 B | 174 B |
| `CreateCarrier` 吞吐 | 2.59 M ops/s | 3.15 M ops/s | 1.79 M ops/s |
| `DestroyCarrier` 延迟（0 索引） | 464 ns | 441 ns | 458 ns |
| `DestroyCarrier` 分配 | 0 B | 0 B | 49 B |

100 万规模创建触发一次完整 GC（GC0/1/2 各 +1）；销毁未触发。

**销毁的索引扇出**（10 万载体，固定）：

| 注册索引数 | 延迟/次 | 分配/次 | 总耗时 |
|---|---|---|---|
| 0 | 447 ns | 0 B | 44.7 ms |
| 1 | 589 ns | 0 B | 58.8 ms |
| 8 | 1.84 µs | 77 B | 184 ms |
| 32 | **6.46 µs** | **289 B** | 646 ms |

每多一个索引约 +190 ns；32 个索引时销毁慢 14 倍。分配来自每次销毁的 `new ICarrierIndex[count]` 快照。

### 3.2 Trait

固定 10 万载体、每载体 4 个 Trait。

| 操作 | 延迟/次 | 分配/次 |
|---|---|---|
| `AddTrait` | 414 ns | 129 B |
| `GetTrait` | 180 ns | 0 B |
| `HasTrait` | 139 ns | 0 B |
| `RemoveTrait` | 223 ns | 1.4 B |
| `GetAllTraits`（4 个） | 1.07 µs | 0 B |
| `FindCarriersWithTrait`（结果 1 万） | 133 µs | 82 KB |
| `FindCarriersWithTrait`（结果 10 万） | 1.23 ms | 803 KB |
| `FindCarriersWithTrait`（结果 100 万） | **13.1 ms** | **8.0 MB** |

热路径（Get/Has）表现良好：亚 200 ns、零分配。`AddTrait` 的 129 B 来自 `traitReverseIndex` 首次创建 `HashSet` 与字典扩容。

### 3.3 全量枚举

| 操作 | 1 万 | 10 万 | 100 万 |
|---|---|---|---|
| `GetCarriersByType` | 235 µs / 84 KB | 1.34 ms / 807 KB | **13.4 ms / 8.0 MB** |
| `GetAllCarriers` | 136 µs / 82 KB | 1.28 ms / 811 KB | **12.7 ms / 8.0 MB** |
| `TotalTraitCount` | 318 µs / 0 B | 3.27 ms / 0 B | **30.2 ms / 0 B** |
| `GetCarrierCount` | 2.5 µs | 235 ns | 840 ns |

前两个 API 每次调用都 `ToList()` 拷贝整个集合——1M 时每次 8 MB 分配、约 13 ms。`TotalTraitCount` 无分配但纯 O(n) 遍历。

### 3.4 Tag 写入与单载体查询（1 万载体 × 10 个三层 Tag）

| 操作 | 延迟/次 | 分配/次 |
|---|---|---|
| `AddTag`（首次，含 Intern 递归） | 3.06 µs | 326 B |
| `AddTag`（重复） | 2.67 µs | 223 B |
| `HasTag`（叶子命中，遍历 10 个 slot） | 3.09 µs | 223 B |
| `HasTag`（祖先命中，提前退出） | 993 ns | 0 B |
| `HasTag`（未注册键） | 1.57 µs | 0 B |
| `RemoveTag` | 3.03 µs | 223 B |
| `GetTags`（10 个） | 2.94 µs | 0 B |

每次读写固定 223 B：`CarrierTagService` 归一化一次，`TagRuntimeIndex` 内部再归一化一次，每次都是 `Split + Trim + Join`。

### 3.5 Tag 反查（最重要的退化点）

固定每槽位 10 个载体，查询返回 10 个结果：

| 槽位数 | 延迟/次 | 吞吐 |
|---|---|---|
| 100 | 15.2 µs | 65.8 k queries/s |
| 1 000 | 130 µs | 7.7 k queries/s |
| 10 000 | **2.88 ms** | **347 queries/s** |

严格线性，约 29 ns/槽位。原因：`FindCarriersWithTag` 遍历的是 `carriersBySlot` 的**全部槽位**，而不是只查目标槽位。槽位 10 万时单次查询约 29 ms。

组合查询（2 万载体）：

| 查询 | 延迟/次 | 分配/次 |
|---|---|---|
| `ALL(2)` | 3.34 ms | 2.9 MB |
| `ANY(2)` | 1.95 ms | 1.8 MB |
| `ALL + NONE` | 2.74 ms | 2.9 MB |

每个 key 都会做一次全槽位扫描，再用 `HashSet` 求交/并，单次查询分配 MB 级。

### 3.6 索引表

| 操作 | 延迟/次 | 分配/次 |
|---|---|---|
| `CarrierIndex.Bind`（无冲突） | 209 ns | 170 B |
| `CarrierIndex.Bind`（值冲突驱逐） | 140 ns | 0 B |
| `CarrierIndex.TryGet` | 34 ns | 0 B |
| `CarrierIndex.TryGetCarrier`（反查） | 24.5 ns | 0 B |
| `CarrierIndex.Unbind` | 98 ns | 0 B |
| `CarrierGroupIndex.Add` | 1.12 µs | 207 B |
| `CarrierGroupIndex.Get`（1000 成员） | 17.8 µs | 8 KB |
| `CarrierGroupIndex.KeysOf` | 551 ns | 23 B |
| `CarrierGroupIndex.RemoveCarrierFromAll` | 691 ns | 0 B |

索引容器本身开销很低，O(1) 读写；`Group.Get` 每次拷贝整个分组，成本与组大小成正比。

## 四、边界判定

| 维度 | 安全区 | 警戒区 | 不可用区 |
|---|---|---|---|
| Carrier 总量 | ≤ 100 万（~170 MB） | 300 万（~510 MB） | 1000 万（~1.7 GB，Mono 风险高） |
| 单帧 `GetCarriersByType` | ≤ 10 万（1.3 ms） | 30 万（~4 ms） | 100 万（13 ms，一帧一次） |
| 单帧 Tag 反查 | 槽位 ≤ 1 000（130 µs） | 槽位 5 000（~1.4 ms） | 槽位 ≥ 1 万（2.9 ms） |
| 单帧 Tag 组合查询 | 1 万载体以内 | 5 万载体 | 20 万载体（~30 ms） |
| 销毁吞吐（32 索引） | ≤ 100/帧（0.65 ms） | 500/帧（3.2 ms） | 2 000/帧（13 ms） |
| 每帧 Tag 写入 | ≤ 1 000 次（3 ms） | 3 000 次（9 ms） | ≥ 5 000 次（15 ms+） |

## 五、瓶颈分析与优化建议（按 ROI 排序）

### 5.1 Tag 反查改为「目标槽位 + 祖先链」直查（收益最大）

现状：`FindCarriersWithTag` / `FindCarriersWithTagUnsafe` 遍历 `carriersBySlot` 全部槽位，对每个槽位调用 `MatchesOrIsAncestor`。成本 O(#槽位 × 平均祖先比较)。

改法：目标槽位 `q` 的匹配集合 = `carriersBySlot[q]` ∪ `carriersBySlot[q 的每个后代]`。由于 Intern 时已为每个 key 建立祖先链，可在 `TagRuntimeIndex` 中补一张 `slot → 直接子槽位` 表，查询时从 `q` 做 BFS 收集后代槽位，再合并对应集合。复杂度降为 O(#后代槽位 + 结果集大小)，与全局槽位数解耦。

**预期**：1 万槽位、查询一个叶子（无后代）从 2.9 ms 降到微秒级。

### 5.2 消除重复的 Tag 键归一化

现状：`CarrierTagService.AddTag/HasTag/RemoveTag` 先 `TryNormalize`，`TagRuntimeIndex.Intern/TryGetSlot` 内部又 `TagKey.Normalize` 一次。两次 `Split + Trim + Join`，固定 223 B/次。

改法：给 `TagRuntimeIndex` 增加接收「已归一化键」的内部方法（`InternNormalized` / `TryGetSlotNormalized`），服务层归一化一次后直接传入。`TagKey.Normalize` 对已规范化的字符串应可做快速路径（无空格、无连续点则原样返回，避免 `Split/Join`）。

**预期**：Tag 写/查延迟降 30%~40%，分配减半（223 B → ~110 B）。

### 5.3 载体 Trait 字典惰性化

现状：`CreateCarrier` 无条件 `carrierTraits[carrierId] = new Dictionary<Type, IDataTrait>()`。空字典对象约 80 B，占 165 B/载体的近一半。

改法：值为 `null` 表示「尚无 Trait」，`AddTrait` 时再创建。`GetTrait/HasTrait/GetAllTraits` 判空即可。

**预期**：1M 纯载体从 ~170 MB 降到 ~90 MB；创建延迟下降。

### 5.4 全量枚举 API 提供零拷贝遍历

现状：`GetCarriersByType` / `GetAllCarriers` / `FindCarriersWithTrait` 每次 `ToList()`，1M 时每次 8 MB。

改法（任选）：
- 提供 `int CopyCarriersTo(List<CarrierId> buffer)`（复用调用方缓冲）；
- 或提供 `ForEachCarrier(Action<CarrierId>)`（锁内回调有风险，需文档约束）；
- 或暴露只读快照 + 版本号，允许调用方缓存。

### 5.5 `DestroyCarrier` 快照复用

现状：每次销毁 `new ICarrierIndex[indexesByTable.Count]` + `CopyTo`；32 索引时 289 B/次。

改法：复用实例级数组缓冲（单线程前提），容量不足时扩容。注意派发期间索引集合可能变化（`OnCarrierRemoved` 内 `UnregisterIndex`），需要保留「快照 == 已移除」的原子语义。

### 5.6 `FindCarriersWithTraits` 先排序再求交

现状：按传入顺序 `Intersect`，中间集合大小不可控。

改法：先按反向索引大小升序排列，从最小集合开始求交，并原地修改第一个集合。

## 六、复现方式

编辑器内（推荐，可增量调试规模）：

```bash
unity command run_tests --mode editor --filter "NexusFramework.Core.Tests.Performance.CarrierPerformanceTests" --async_tests true --project-path "D:\Work\Unity\NexusFramework"
```

批量模式（不依赖 GUI，跑完退出）：

```bash
cmd //c Tools/run-carrier-perf.bat
```

结果：`TestResults/carrier-perf.txt`（每行一条测量），失败信息见 `TestResults/carrier-perf.xml`。

调整规模：修改 `CarrierPerformanceTests.CreateScales`（默认 `{10_000, 100_000, 1_000_000}`）。

## 七、未覆盖项

- **多线程并发**：所有测量为单线程。`CarrierManager` 的 `lock` 在高并发下的争用成本未测。
- **IL2CPP / Player**：Editor Mono 数据不能直接外推到真机；IL2CPP 下泛型与字典通常更快，GC 行为不同。
- **序列化**：`SaveToFile` / `LoadFromFile` 未纳入本次范围。
- **GAS 业务层**：Carrier ↔ Entity ↔ GameObject 映射与 GE/Ability 管线的开销单独测量更合适。
