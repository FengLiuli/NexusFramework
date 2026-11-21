---
layer: progress
status: done
created: 2026-11-17
updated: 2026-11-17
---

# 工厂类游戏场景性能边界（2026-11-17）

> 场景：1000 种物品原型、10 000 个容器、每容器 100 个物品实例 = **1 000 000 个 Carrier**。
> 配套文档：[核心层](2026-11-17-carrier-performance-baseline.md) ·
> [Trait 高频路径](2026-11-17-carrier-trait-hotpath-baseline.md) ·
> [DataTrait](2026-11-17-datatrait-performance-baseline.md) ·
> [问题汇总](2026-11-17-carrier-issues-backlog.md)

## 一、结论

**能撑住，但不能用「全量 Carrier」这一种形态。**

| 维度 | 全量 Carrier 实测 | 判断 |
|---|---|---|
| 驻留内存（10k 容器 + 1M 物品 + 2 索引） | **849.3 MB**（891 B/物品） | PC 勉强，移动端不可行 |
| 冷构建 1M 物品 | **19.4 s**（含 5 次完整 GC，累计分配 972 MB） | 不可作为加载时间 |
| 全量存档 | 逐物品 **10.2 s / 130 MB** | 不可接受 |
| 容器批量存档 | **0.95 s / 33.4 MB** | 可用 |
| 二进制紧凑存档 | **0.27 s / 11.5 MB** | 推荐 |
| 紧凑 struct 对照（1M） | **11.4 MB**（12 B/物品） | 比 Carrier 省 74 倍 |
| 100 活跃容器 tick | **2.38 ms/帧** | 安全 |
| 1000 活跃容器 tick | **20.5 ms/帧** | 超帧 |
| 单容器读取（100 物品） | 27.1 µs | 安全 |
| 物品搬运 | 1.17 µs/次 | 每帧 ≤ 5 000 次 |
| 生成+挂载+销毁 | 3.13 µs/次 | 每帧 ≤ 2 000 次 |

**推荐架构**：静态库存用紧凑结构（12 B/物品），只为「活跃容器 / 手上物品 / UI 可见物品」创建 Carrier。Carrier 负责行为与索引，紧凑数组负责海量存储。

## 二、测试模型

- 物品实例：Carrier + `FactoryItemTrait { definitionId, count, state }`
- 容器：Carrier + `FactoryContainerTrait { capacity, kind }`
- 归属：`CarrierGroupIndex<CarrierId>`（容器 → 物品集合；反向账本 `KeysOf` 给出物品 → 容器）
- 定义索引：`CarrierGroupIndex<int>`（definitionId → 物品集合）
- 1000 种物品原型**不进 Carrier**，只存 `definitionId`
- 环境：Unity 2022.3.62f2c1 / Editor Mono。同机另一个 Unity 实例在运行，绝对值偏高 5%~15%。

## 三、构建与内存

| 阶段 | 耗时 | 累计分配 | GC |
|---|---|---|---|
| 10 000 个容器 | 29.7 ms | 2.3 MB | 0 |
| 1 000 000 个物品（Create + Trait + 2 次索引写入） | **19 368.6 ms** | 972 MB | 5 次完整 GC |

单物品构建 19.4 µs，其中**大部分是 1M 对象存活时的完整 GC**（5 次约 15 s），不是 CPU 本身。若分批构建并在块间让 GC 有机会回收临时对象，实测的“纯 CPU”约 3~4 µs/物品。

**驻留内存**：

| 方案 | 内存 | 每物品 |
|---|---|---|
| Carrier 全量（含容器与索引） | 849.3 MB | 891 B |
| `CompactItemSlot[]` | **11.4 MB** | **12 B** |
| 比例 | **74.3×** | |

Carrier 的 891 B/物品构成为：空 Trait 字典（~80 B）+ 字典/HashSet 条目（3 套：载体表、类型集合、反向索引）+ 两张业务索引的正反账本 + 每个 Trait 对象本身。

## 四、运行期

### 4.1 读取与 tick

| 操作 | 耗时 | 分配 |
|---|---|---|
| 单容器读取（100 物品 + Trait） | 27.1 µs | 1.33 KB |
| 10 000 容器全扫描 | 223.0 ms | 13.3 MB |
| tick 10 个活跃容器 | 176 µs/次 | — |
| tick 100 个活跃容器 | 2.38 ms/次 | — |
| tick 1 000 个活跃容器 | 20.5 ms/次 | — |

每物品读取约 176~238 ns。**活跃容器 100 个以内可稳定 60 FPS**；1000 个需要拆分到多帧。

### 4.2 搬运与生成销毁

| 操作 | 单价 | 每帧 1 万次 |
|---|---|---|
| 移动物品（Remove + Add + KeysOf 反查） | 1.17 µs | 11.7 ms |
| 生成 + 挂 2 索引 + 销毁 | 3.13 µs | 31.3 ms |

传带、机械臂这类高频搬运建议每帧 ≤ 5 000 次；大量生成/销毁必须分帧或走对象池。

### 4.3 查询

| 方式 | 耗时 |
|---|---|
| 全扫 1M 物品按 definitionId 统计 | 221.7 ms |
| 定义索引查询（1000 结果） | 28.3 µs |

**相差约 7 800 倍**。任何「某定义持有多少 / 有哪些」的查询都必须走业务索引，不能全扫。

## 五、存档方案对比

| 方案 | 耗时（1M 物品） | 体积 | 每物品 |
|---|---|---|---|
| 逐物品 `SerializeCarrier` | **10.2 s**（10 万抽样实测 1.02 s 外推） | **130 MB** | 137 B |
| 容器批量紧凑 JSON | **0.95 s**（组装 0.32 s + ToJson 0.63 s） | **33.4 MB** | 35 B |
| 二进制紧凑格式 | **0.27 s** | **11.5 MB** | 12.1 B |

逐物品 JSON 的体积主要来自每个 Carrier 的 `carrierId`、`tags: []`、Trait 包装等固定开销；容器批量 JSON 把这些开销摊到每容器一次。

**结论**：1M 物品的存档必须用容器批量或二进制格式，`CarrierManager.SaveToFile` 只适合小规模（≤ 1 万）。

## 六、边界判定

| 场景 | 安全区 | 警戒区 | 不可用区 |
|---|---|---|---|
| Carrier 全量物品数（PC） | ≤ 20 万（~170 MB） | 50 万（~420 MB） | 100 万（~850 MB） |
| 冷构建 | ≤ 5 万（< 1 s） | 20 万（~4 s） | 100 万（19 s） |
| 每帧活跃容器 tick | ≤ 100（2.4 ms） | 300（~7 ms） | ≥ 1 000（20 ms） |
| 每帧物品搬运 | ≤ 5 000（5.9 ms） | 1 万（11.7 ms） | 2 万 |
| 每帧生成+销毁 | ≤ 2 000（6.3 ms） | 5 000（15.7 ms） | 1 万 |
| 单容器 UI 读取 | ≤ 500 物品 | 1 000 物品 | 5 000 物品 |
| 存档（全量） | 容器批量 JSON | 二进制紧凑 | 逐物品 JSON（> 1 万） |

## 七、对工厂场景的设计建议

1. **冷热分离**：静态库存用 `CompactItemSlot[]`（12 B/物品），只有活跃机器、传送带、UI 可见容器创建 Carrier。1M 物品静态存储 11.4 MB，按需“热化”数百个。
2. **存档走容器批量/二进制**：按容器聚合，`ItemSaveRecord{defId,count,state}` 数组一次序列化；目标 12~35 B/物品。
3. **每个容器只建一个 Carrier**：容器本体 1 万个 Carrier 可接受（构建 29.7 ms、内存几 MB），物品实例按活跃度决定是否建。
4. **归属用 `CarrierGroupIndex<CarrierId>`**，不要用 `CarrierIndex<CarrierId>`——后者是双射，多个物品指向同一容器会互相驱逐。
5. **定义索引独立维护**：`CarrierGroupIndex<int>`（definitionId → 物品），查询 28 µs 而不是 222 ms。
6. **搬运优先改归属而不是销毁重建**：1.17 µs vs 3.13 µs，且后者带来 GC 压力。
7. **构建分批**：一次连续建 1M 会触发 5 次完整 GC（约 15 s）；按 1 万一批并在批间 `yield`，可把加载时间压到接近纯 CPU 成本（约 3~4 s）。
8. **Trait 反序列化必须先修**（backlog A1），否则热化后的物品读档会丢数据。

## 八、复现方式

```bash
unity command run_tests --mode editor --filter "NexusFramework.Core.Tests.Performance.FactoryScenarioTests" --async_tests true --project-path "D:\Work\Unity\NexusFramework"
```

结果：`TestResults/factory-scenario.txt`；内存与体积见编辑器日志的 `[PERF][MEM]` / `[PERF][SIZE]`。

## 九、未覆盖项

- **反序列化（B/C 方案）**：只测了序列化；读档还需反序列化 + 重建 Carrier，成本约为构建的 1~2 倍。
- **增量存档**：只测全量，未测脏容器增量写。
- **移动端内存约束**：仅 PC Editor 数据；移动端 850 MB 不可接受，必须靠冷热分离。
- **多线程**：全部单线程。
