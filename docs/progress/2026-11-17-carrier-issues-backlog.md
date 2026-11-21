---
layer: progress
status: open
created: 2026-11-17
updated: 2026-11-17
---

# Carrier / DataTrait 问题汇总（待统一处理）

> 来源：
> - [2026-11-17-carrier-performance-baseline.md](2026-11-17-carrier-performance-baseline.md)（核心层）
> - [2026-11-17-datatrait-performance-baseline.md](2026-11-17-datatrait-performance-baseline.md)（Trait 与序列化）
> - [2026-11-17-carrier-trait-hotpath-baseline.md](2026-11-17-carrier-trait-hotpath-baseline.md)（CarrierManager × DataTrait 高频路径）
> - [2026-11-17-factory-scenario-baseline.md](2026-11-17-factory-scenario-baseline.md)（工厂类游戏 100 万物品）
>
> 本文只做汇总与排序，**不含修复**。每条给出证据、影响与建议修法，供后续统一排期。

## 优先级说明

| 级别 | 含义 |
|---|---|
| **P0** | 功能失效，数据丢失，必须修 |
| **P1** | 高频路径的明确瓶颈，影响上限或帧预算 |
| **P2** | 中低频路径或不影响正确性，可排期优化 |
| **P3** | 观察项 / 文档补充，暂不处理 |

---

## P0 — 正确性

### A1. Trait 反序列化完全失效

**位置**：`CarrierManager.DeserializeCarrier`（`CarrierManager.cs:679-696`）、`DataTrait.TraitTypeName`（`DataTrait.cs:38`）

**证据**：实测 1 000 / 10 000 / 100 000 载体反序列化后 Trait 恢复数**均为 0**（期望 4 000 / 40 000 / 400 000）。

**根因**（两处叠加）：
1. `TraitTypeName` 返回 `GetType().Name`（仅类名）；`Type.GetType(name)` 只查 mscorlib 与调用方程序集 `NexusFramework`，业务程序集的 Trait 一律返回 `null`。
2. `BasicInfoTrait` 只定义了带参构造，`Activator.CreateInstance` 会抛 `MissingMethodException`；异常被 `DeserializeCarrier` 的外层 `catch` 吞掉。

**影响**：存档读回后载体存在但无任何 Trait，属静默数据丢失。现有测试只验证了 Tag 恢复，未覆盖 Trait。

**建议**：
- `TraitTypeName` 改为 `AssemblyQualifiedName`，或维护「短名 → Type」注册表（启动时扫描 `IDataTrait` 实现）。
- `Activator.CreateInstance` 失败时回退 `FormatterServices.GetUninitializedObject`。
- 补一条 `SerializeCarrier → DeserializeCarrier` 的 Trait 往返回归测试。

---

## P1 — 高频路径瓶颈

### B1. Tag 反查扫描全部槽位

**位置**：`CarrierTagService.FindCarriersWithTag` / `FindCarriersWithTagUnsafe`（`CarrierTagService.cs:372-389, 547-558`）

**证据**：槽位 100 / 1 000 / 10 000 时，单次查询 15.2 µs / 130 µs / **2.88 ms**，严格线性（约 29 ns/槽位）。

**根因**：遍历 `carriersBySlot` 的全部槽位逐个做祖先匹配，而不是只查目标槽位及其后代。

**建议**：在 `TagRuntimeIndex` 补一张 `slot → 直接子槽位` 表，查询时从目标槽位 BFS 收集后代，再合并 `carriersBySlot`。复杂度与全局槽位数解耦。

**预期**：1 万槽位的叶子查询从 2.88 ms 降到微秒级。

### B2. Tag 键归一化重复执行

**位置**：`CarrierTagService.TryNormalize` + `TagRuntimeIndex.Intern/TryGetSlot`（`TagRuntimeIndex.cs:22-56`）

**证据**：`AddTag` / `HasTag` / `RemoveTag` 每次固定 **223 B** 分配，延迟约 3 µs。

**根因**：服务层调用一次 `TagKey.Normalize`，`TagRuntimeIndex` 内部再调用一次；每次 `Split + Trim + Join`。

**建议**：`TagRuntimeIndex` 增加接收「已归一化键」的内部方法；`TagKey.Normalize` 对无空格的键走快速路径（直接返回原字符串）。

**预期**：分配减半，延迟降 30%~40%。

### B3. 全量枚举 API 每次 O(n) 拷贝

**位置**：`CarrierManager.GetCarriersByType` / `GetAllCarriers` / `FindCarriersWithTrait`

**证据**：1M 规模每次调用 **8.0 MB 分配**、12.7~13.4 ms。

**建议**：提供 `int CopyCarriersTo(List<CarrierId> buffer)` 或回调式遍历；至少让调用方复用缓冲。

### B4. 每个 Carrier 预分配空 Trait 字典

**位置**：`CarrierManager.CreateCarrier`（`CarrierManager.cs:247`）

**证据**：创建分配 **165~174 B/载体**，1M 约 170 MB；空 `Dictionary<Type, IDataTrait>` 约占一半。

**建议**：值改为 `null` 表示「无 Trait」，`AddTrait` 时惰性创建。

**预期**：纯载体内存降到约 90 MB/1M。

### B5. `TraitTypeName` 每次分配字符串

**位置**：`DataTrait.TraitTypeName`（`DataTrait.cs:38`）

**证据**：272 ns、**59.3 B/次**；`SerializeCarrier` 对每个 Trait 调用一次。

**建议**：改为静态只读字段或 `[ThreadStatic]` 缓存；序列化时复用。

### B6. `Clone` 基于 JSON 往返 + 反射构造

**位置**：`DataTrait.Clone`（`DataTrait.cs:40-46`）

**证据**：small 3.26 µs、medium 7.68 µs、large 28.3 µs；4 KB 字符串 **118.6 µs**（超线性），1000 元素列表 **124.3 µs**。

**建议**：
- 热路径避免 `Clone`，改为显式拷贝构造或对象池。
- 业务侧限制单个 Trait 的字符串/集合规模。
- 若必须通用克隆，考虑生成式克隆或缓存 `Activator` 委托（`Expression.Lambda` / `CreateDelegate`）。

### B7. `ScriptableObjectDataTrait.Clone` 成本极高

**位置**：`DataTrait.cs:64-75`

**证据**：**861 µs/次**（含 `DestroyImmediate`），约为普通 `Clone` 的 260 倍。

**建议**：SO Trait 不应作为可克隆数据结构；改为缓存实例、池化，或迁移到普通 `DataTrait`。

---

## P2 — 可排期优化

### B8. `DestroyCarrier` 的索引扇出、Trait 清理与快照分配

**位置**：`CarrierManager.DestroyCarrier`（`CarrierManager.cs:315-368`）

**证据**：
- 索引数 0 / 1 / 8 / 32 时，销毁延迟 447 ns / 589 ns / 1.84 µs / **6.46 µs**；32 索引时分配 289 B/次（索引快照数组）。
- 每载体 Trait 数 0 / 4 / 8 时，销毁延迟 836 ns / 1.38 µs / **2.53 µs**（每个 Trait 约 +210 ns，清理 `traitReverseIndex`）。
- 两种成本叠加：32 索引 + 8 Trait 的载体，单次销毁约 9 µs。

**建议**：复用实例级快照数组；评估「按索引订阅类型分发」以减少无关索引回调；Trait 反向索引可考虑批量清理。

### B9. `FindCarriersWithTraits` 多条件反查严重退化

**位置**：`CarrierManager.FindCarriersWithTraits`（`CarrierManager.cs:516-541`）

**证据**（10 万载体、每载体 4 Trait）：

| 条件数 | 延迟/次 | 分配/次 |
|---|---|---|
| 1 | 1.30 ms | 803 KB |
| 2 | **15.0 ms** | 7.45 MB |
| 4 | **30.4 ms** | 2.37 MB（触发 GC） |

每个条件都完整构建一次 `HashSet`，再用 LINQ `Intersect` 生成中间集合；既未按集合大小排序，也未原地求交。2 个条件比单条件慢 11 倍。

**建议**：先按反向索引大小升序，从最小集合开始，原地 `IntersectWith` 复用第一个集合；结果集仍需 `ToList` 快照时再一次性拷贝。

### B10. `TotalTraitCount` 是 O(n) 遍历

**位置**：`CarrierManager.TotalTraitCount`（`CarrierManager.cs:767-776`）

**证据**：1M × 4 Trait 时 **30.2 ms/次**，零分配。

**建议**：维护增量计数器（`AddTrait` / `RemoveTrait` / 销毁时更新），或明确文档标注「诊断用途，勿在热路径调用」。

### B11. `SerializeCarrier` 无 Tag 也分配

**位置**：`CarrierManager.SerializeCarrier`（`CarrierManager.cs:630-633`）

**证据**：`tags = tagService?.GetTags(...).OrderBy(...).ToArray() ?? Array.Empty<string>()`；每次调用即使无 Tag 也走 LINQ + 排序 + 数组。

**建议**：先判断 Tag 数量，为 0 时直接复用 `Array.Empty<string>()`。

### B12. `SerializeCarrier` 持锁执行 JsonUtility

**位置**：`CarrierManager.SerializeCarrier`（`CarrierManager.cs:635-649`）

**影响**：锁持有时间约 27 µs/载体；多线程序列化时锁争用放大。

**建议**：锁内只拷贝 Trait 引用快照，锁外执行 `ToJson`。

### B13. `SaveToFile` 无锁遍历

**位置**：`CarrierManager.SaveToFile`（`CarrierManager.cs:717`）

**证据**：`carrierTraits.Keys.Select(SerializeCarrier).ToArray()` 未加锁；与并发 `CreateCarrier` / `DestroyCarrier` 组合可能抛 `InvalidOperationException`。

**建议**：先取 `GetAllCarriers()` 快照再序列化，或明确文档「存档必须在主线程且无并发写」。

---

## P3 — 观察项

### C1. 表现良好、无需优化

| API | 延迟 | 分配 |
|---|---|---|
| `GetTrait` | 180 ns | 0 |
| `HasTrait` | 139 ns | 0 |
| `CarrierIndex.TryGet` | 34 ns | 0 |
| `CarrierIndex.TryGetCarrier` | 24.5 ns | 0 |
| `CarrierIndex.Bind` | 209 ns | — |
| `CarrierIndex.Unbind` | 98 ns | 0 |
| `GetCarrierCount` | 235~840 ns | 0 |

### C2. 待补充文档

- `CarrierId` 的 40 位 uniqueId 上限（约 1.1 万亿）与 `CreateCarrier` 的 `do/while` 冲突检测在超大规模下的行为。
- `TagQuery` 的 `All/Any/None` 组合语义与空查询（`IsEmpty`）的边界。
- `SaveToFile` / `LoadFromFile` 的错误处理目前只 `Debug.LogError`，无返回值，调用方无法感知失败。

### C3. Trait 全量读取的帧预算约束

**位置**：`CarrierManager.GetTrait` / `HasTrait`

**数据**（单线程、Editor Mono）：

| 规模 | 全量读 4 Trait 一次 | 占 16 ms 帧预算 |
|---|---|---|
| 1 万载体 | 6.0 ms | 38% |
| 3 万载体 | ~18 ms | 114% |
| 10 万载体 | 61.9 ms | 387% |
| 100 万载体 | 595.5 ms | 3 723% |

**处置**：这不是代码缺陷，是使用约束。建议写入开发文档：**每帧 Trait 全量刷新的上限约 1 万载体**；更多单位应使用脏标记、分帧轮转或业务侧增量集合。

---

## 建议处理顺序

1. **P0 / A1**：Trait 反序列化修复 + 回归测试（数据正确性优先）。
2. **P1 / B1、B2**：Tag 反查与归一化（收益最大、改动集中）。
3. **P1 / B3、B4**：枚举零拷贝与 Trait 字典惰性化（内存与帧预算）。
4. **P2**：按业务热点逐个处理；B12/B13 与并发存档一起评估。
5. **P3**：补充文档与诊断约束。

## 八、场景评估：工厂类游戏（100 万物品）

> 详细数据见 [2026-11-17-factory-scenario-baseline.md](2026-11-17-factory-scenario-baseline.md)。

**实测**：10 000 容器 + 100 万物品全量 Carrier 时，驻留内存 **849 MB**、冷构建 **19.4 s**、逐物品存档 **10.2 s / 130 MB**；紧凑 struct 对照仅 **11.4 MB**（74 倍差）。

这不是框架缺陷，而是**容量与形态约束**，会派生出四项工程需求：

| # | 派生需求 | 归属 | 前置 |
|---|---|---|---|
| F1 | 冷热分离：静态库存用紧凑结构，活跃对象才建 Carrier | 物品子系统 | 物品模块设计阶段 |
| F2 | 容器批量 / 二进制存档（逐物品 JSON 在 1 万以上不可用） | 物品子系统 + 存档子系统 | 存档子系统落地 |
| F3 | 活跃集 tick 调度（100 活跃容器 2.4 ms，1000 活跃 20.5 ms） | 物品子系统 | — |
| F4 | 定义索引（`CarrierGroupIndex<int>`）替代全扫（28 µs vs 222 ms） | 物品子系统 | — |

**与 P0 的关系**：F1~F4 都建立在 A1（Trait 反序列化）修复之后；否则热化物品读档会丢数据。

## 复现与数据

- 核心层：`TestResults/carrier-perf.txt`
- DataTrait：`TestResults/datatrait-perf.txt`
- Trait 高频路径：`TestResults/carrier-trait-hotpath.txt`
- 工厂场景：`TestResults/factory-scenario.txt`
- 运行方式见四份基线报告的「复现方式」章节。
