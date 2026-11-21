---
layer: progress
status: done
created: 2026-11-17
updated: 2026-11-17
---

# CarrierManager × DataTrait 高频路径性能边界（2026-11-17）

> 配套文档：
> [核心层基线](2026-11-17-carrier-performance-baseline.md) ·
> [DataTrait 基线](2026-11-17-datatrait-performance-baseline.md) ·
> [问题汇总](2026-11-17-carrier-issues-backlog.md)

## 一、结论速览

**Carrier + DataTrait 的高频用法就是 `ICarrierManager` 的 Trait 方法**：每帧按载体遍历 `GetTrait<T>` / `HasTrait<T>`，生成与销毁时 `AddTrait` / `RemoveTrait`，条件筛选时 `FindCarriersWithTrait<T>`。本报告测的就是这条路径的容量上限。

| 问题 | 结论 |
|---|---|
| 每帧全量读 4 个 Trait | **1 万载体 6.0 ms**；10 万载体 **61.9 ms**（约 4 帧）；100 万载体 595 ms |
| 单次 Trait 读取 | `GetTrait` **149 ns**、`HasTrait` **120 ns**，零分配 |
| 单次 Trait 写入 | `AddTrait` 362~548 ns，分配 68~166 B（随每载体 Trait 数增长） |
| 混合读写 90/10 | 1M 载体 4M 操作 570 ms（142.5 ns/op） |
| 带 Trait 销毁 | 每多一个 Trait 约 +210 ns（4 个 1.38 µs，8 个 2.53 µs） |
| 多条件反查 | `FindCarriersWithTraits` **4 个 Trait 30.4 ms/次**，2 个 15.0 ms/次 |
| 多 typeId 分片 | 不影响 Trait 读取（178 ns/op） |

**一句话边界**：单线程下，**每帧约能完成 60~110 万次 Trait 读**（16 ms 预算）；1 万单位 × 4 Trait 的“全量刷新”占 38% 帧预算，10 万单位就已超帧。

## 二、测试环境与方法

- 环境同前两份基线：Unity 2022.3.62f2c1、Editor / Mono、Windows 10。
- 测试：`Assets/Tests/Stress/CarrierTraitHotPathTests.cs`（9 个测试）。
- **Trait 形态用项目真实类型**：`GASEntityTrait`（ulong）、`BasicInfoTrait`（3 string + bool），另加 6 个带 int 字段的辅助 Trait，共 8 个不同泛型参数，用于 1/4/8 Trait 矩阵。
- Trait 实例复用（与业务一致：一个 Trait 实例可被多个载体引用）。
- 读取的 `_sink` 累加防止 JIT 消除。

## 三、数据明细

### 3.1 写入：载体数 × 每载体 Trait 数

| 载体数 | Trait 数 | 延迟/次 | 分配/次 | 吞吐 |
|---|---|---|---|---|
| 1 万 | 1 | 453 ns | 68 B | 2.21 M ops/s |
| 1 万 | 4 | 362 ns | 103 B | 2.76 M ops/s |
| 1 万 | 8 | 382 ns | 138 B | 2.62 M ops/s |
| 10 万 | 1 | 458 ns | 120 B | 2.18 M ops/s |
| 10 万 | 4 | 476 ns | 133 B | 2.10 M ops/s |
| 10 万 | 8 | **548 ns** | **166 B** | 1.82 M ops/s |

每多挂一个 Trait，分配增加 5~10 B/次（内层字典扩容）；10 万 × 8 时延迟上升约 20%。

### 3.2 读取：全量遍历的帧预算

「全量遍历」= 遍历所有载体，各读 4 个 Trait 一次。

| 载体数 | `GetTrait` 单次遍历 | `HasTrait` 单次遍历 |
|---|---|---|
| 1 万（4 万次读） | **6.0 ms** | 5.1 ms |
| 10 万（40 万次读） | **61.9 ms** | 50.7 ms |
| 100 万（400 万次读） | **595.5 ms** | 480.1 ms |

单次读取延迟：`GetTrait` 149~155 ns，`HasTrait` 120~127 ns，**零分配**。

按 16 ms 帧预算折算：

| 规模 | 占帧预算 | 能否每帧全量刷新 |
|---|---|---|
| 1 万 × 4 Trait | 38% | 可以（还剩 10 ms） |
| 3 万 × 4 Trait | 114% | 超帧 |
| 10 万 × 4 Trait | 387% | 需要 4 帧 |
| 100 万 × 4 Trait | 3 723% | 需要 37 帧 |

### 3.3 混合读写（90% 读 / 10% 写）

100 万载体、每载体 4 Trait，共 400 万次操作：

| 指标 | 数值 |
|---|---|
| 总耗时 | 569.9 ms |
| 单次操作 | 142.5 ns |
| 分配 | 0 B |
| 吞吐 | 7.0 M ops/s |

写入分支为 `RemoveTrait + AddTrait`（状态刷新）。按此折算，1 万单位每帧混合读写约 **5.7 ms**。

### 3.4 Add/Remove 抖动（buff / 状态高频增删）

1 万载体 × 100 轮（每轮 Add + Remove 一个 Trait），共 200 万次：

| 指标 | 数值 |
|---|---|
| 总耗时 | 440.1 ms |
| 单次 | 220 ns |
| 1 万单位单轮（2 万次） | 4.4 ms |

适合「每帧对一小批单位刷新状态」；全量 10 万单位每轮则需 44 ms。

### 3.5 反查

**`FindCarriersWithTrait<T>`**：

| 结果集大小 | 延迟/次 | 分配/次 |
|---|---|---|
| 1 万 | 430 µs | 80 KB |
| 10 万 | 1.31 ms | 803 KB |
| 100 万 | **12.5 ms** | **8.0 MB** |

**`FindCarriersWithTraits(params Type[])`**（10 万载体、每载体 4 Trait）：

| 条件数 | 延迟/次 | 分配/次 |
|---|---|---|
| 1 个 | 1.30 ms | 803 KB |
| 2 个 | **15.0 ms** | 7.45 MB |
| 4 个 | **30.4 ms** | 2.37 MB（触发 GC） |

多条件反查是当前最差的一组：2 个条件比单条件慢 11 倍，4 个慢 23 倍。原因：每个条件都完整构建一次 10 万元素 `HashSet`，再用 LINQ `Intersect` 生成中间集合，既未按集合大小排序，也未原地求交。

### 3.6 带 Trait 的销毁

10 万载体：

| 每载体 Trait 数 | 销毁延迟/次 | 相对空载体 |
|---|---|---|
| 0 | 836 ns | 1.0× |
| 4 | 1.38 µs | 1.7× |
| 8 | **2.53 µs** | 3.0× |

每个 Trait 约 +210 ns（清理 `traitReverseIndex`）。批量销毁 1 万单位（8 Trait）约 25 ms。

### 3.7 全量拷贝 API

10 万载体、每载体 8 Trait：

| 操作 | 延迟/次 | 分配/次 |
|---|---|---|
| `GetAllTraits` | 1.01 µs | 75 B |
| `GetTraitTypes` | 977 ns | 75 B |

### 3.8 多 typeId 分片

4 个 typeId、每类 2.5 万载体、每载体 4 Trait，共 40 万次读：

| 指标 | 数值 |
|---|---|
| 总耗时 | 71.3 ms |
| 单次 | 178 ns |
| 反查结果 | 10 万（全局，符合预期） |

typeId 只影响 `carriersByType`，不改变 Trait 读写路径；分片带来的 15% 延迟差异来自缓存局部性。

## 四、边界判定

| 场景 | 安全区 | 警戒区 | 不可用区 |
|---|---|---|---|
| 每帧全量读 4 Trait | ≤ 1 万载体（6 ms） | 2~3 万（12~18 ms） | ≥ 5 万 |
| 每帧混合读写（90/10） | ≤ 1 万载体（5.7 ms） | 2 万 | ≥ 3 万 |
| 每帧 Add/Remove 抖动 | ≤ 1 万次（2.2 ms） | 3 万次 | ≥ 5 万次 |
| `FindCarriersWithTrait` 单条件 | 结果 ≤ 1 万（430 µs） | 10 万（1.3 ms） | 100 万（12.5 ms） |
| `FindCarriersWithTraits` 多条件 | 1 个条件 | 2 个（15 ms） | 4 个（30 ms） |
| 批量销毁（8 Trait） | 1 000/帧（2.5 ms） | 4 000/帧 | 1 万/帧（25 ms） |

## 五、对高频用法的建议

1. **按帧分摊**：Trait 全量刷新不要每帧做，按 3~10 帧轮转或按脏标记更新。
2. **条件反查先缩小候选集**：先用更小的 `CarrierGroupIndex`（阵营、场景 ID）收窄，再对候选集做 Trait 判断，避免 `FindCarriersWithTraits` 的多条件全量求交。
3. **避免每帧 `FindCarriersWithTrait`**：结果集 1 万时单次 430 µs，且每次分配 80 KB；应在 Trait 变更时增量维护业务侧集合。
4. **状态刷新用最小 Trait**：抖动测试显示 Add+Remove 一次 220 ns，但每多一个 Trait，销毁与写入都会变慢；把「高频变更」与「静态数据」拆成不同 Trait。
5. **大批量生成/销毁分帧**：10 万单位 × 8 Trait 的销毁约 25 ms，应分帧。

## 六、复现方式

```bash
unity command run_tests --mode editor --filter "NexusFramework.Core.Tests.Performance.CarrierTraitHotPathTests" --async_tests true --project-path "D:\Work\Unity\NexusFramework"
```

结果：`TestResults/carrier-trait-hotpath.txt`。

## 七、未覆盖项

- **多线程**：所有测量为单线程。`lock` 争用、`IsCarrierValid` 无锁读的竞态未覆盖。
- **IL2CPP / Player**：Mono 数据不能直接外推；泛型字典在 IL2CPP 下通常更快。
- **Trait 内容访问**：读取 Trait 后访问其字段的成本未单独测（应计入业务逻辑）。
- **事件回调**：`AddTrait` / `RemoveTrait` 不广播事件；若业务在 Trait 变更时订阅事件，需另行评估。
