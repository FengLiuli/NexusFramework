---
layer: progress
status: done
created: 2026-11-17
updated: 2026-11-17
---

# DataTrait 与 Carrier 序列化性能边界（2026-11-17）

> 配套文档：核心层基线见 [2026-11-17-carrier-performance-baseline.md](2026-11-17-carrier-performance-baseline.md)；
> 待修问题汇总见 [2026-11-17-carrier-issues-backlog.md](2026-11-17-carrier-issues-backlog.md)。

## 一、结论速览

| 问题 | 结论 |
|---|---|
| Trait 序列化贵不贵 | `ToJson` 1.3~11.6 µs、`FromJson` 1.2~13.2 µs，取决于字段规模 |
| `Clone` 能不能高频用 | 小型 3.3 µs 可以；字符串 4 KB 时 **118.6 µs**、列表 1000 项时 **124.3 µs** |
| `ScriptableObjectDataTrait.Clone` | **861 µs/次**（`CreateInstance` 主导），一帧只能约 18 次 |
| 存档吞吐 | 序列化 **27 µs/载体**、反序列化 **41 µs/载体**；10 万载体存档 2.7 s、读档 4.1 s |
| 存档体积 | 每载体 613 B（4 个 Trait），10 万载体约 **7.8 MB** |
| **最重要的发现** | **`DeserializeCarrier` 无法恢复任何 Trait**——实测恢复数恒为 0（§4） |

## 二、测试环境与方法

- 环境同核心层基线：Unity 2022.3.62f2c1、Editor / Mono、Windows 10。
- 测试代码：`Assets/Tests/Stress/DataTraitPerformanceTests.cs`（10 个测试，`Category("Performance")`）。
- 测量工具复用 `Perf`（`Stopwatch` + `GC.GetTotalMemory` 差值）。
- JSON 由 `JsonUtility` 生成；测试 Trait 均为测试程序集内的 `[Serializable]` 类。

## 三、数据明细

### 3.1 TraitTypeName

| 操作 | 延迟 | 分配 |
|---|---|---|
| `TraitTypeName`（`GetType().Name`） | 272 ns | **59.3 B/次** |

每次访问都分配一个新字符串。`SerializeCarrier` 对每个 Trait 调用一次，是序列化路径上的固定分配源。

### 3.2 单 Trait 序列化 / 反序列化

测试形态：

- small：1 个 `ulong`，JSON 12 B（对标 `GASEntityTrait`）
- medium：3 个 `string` + 1 个 `bool`，JSON 121 B（对标 `BasicInfoTrait`）
- large：16 个 `int` + 4 个 `string` + `List<int>[100]`，JSON 472 B

| 操作 | small | medium | large |
|---|---|---|---|
| `ToJson` | 1.35 µs / 0 B | 3.17 µs / 265 B | 11.6 µs / 1.32 KB |
| `FromJson` | 1.22 µs / 0 B | 3.31 µs / 156 B | 13.2 µs / 0 B |
| `Clone` | 3.26 µs / 0 B | 7.68 µs / 194 B | 28.3 µs / 1.56 KB |

`Clone` ≈ `ToJson` + `FromJson` + 约 0.7 µs（`Activator.CreateInstance`）。

### 3.3 Clone 的规模敏感性

**字符串长度**（`PerfStringTrait`）：

| 字符串长度 | Clone 延迟 | 相对 16 字符 |
|---|---|---|
| 16 | 4.56 µs | 1.0× |
| 256 | 8.67 µs | 1.9× |
| 4 096 | **118.6 µs** | **26×** |

长度 256 倍，耗时 26 倍——**超线性**。4 KB 档触发 2 次 GC。含大字符串（描述、富文本、JSON 片段）的 Trait 不适合用 `Clone` 热拷贝。

**集合大小**（`PerfCollectionTrait`）：

| `List<int>` 元素数 | Clone 延迟 |
|---|---|
| 0 | 5.37 µs |
| 100 | 18.9 µs |
| 1 000 | **124.3 µs** |

### 3.4 ScriptableObjectDataTrait

| 操作 | 延迟（含 `DestroyImmediate`） |
|---|---|
| `ScriptableObjectTrait.Clone` | **861 µs/次** |

`ScriptableObject.CreateInstance` 是主要成本，比普通 `Clone` 慢约 260 倍。若业务把 SO Trait 当作可克隆数据，需要改为缓存实例或改用普通 `DataTrait`。

### 3.5 聚合路径：SerializeCarrier / DeserializeCarrier

固定每载体 4 个 Trait，JSON 613 B/载体。

| 载体数 | `SerializeCarrier` | `DeserializeCarrier` |
|---|---|---|
| 1 000 | 26.8 µs/载体 | 41.2 µs/载体 |
| 10 000 | 25.9 µs/载体 | 41.0 µs/载体 |
| 100 000 | 27.2 µs/载体 | 40.7 µs/载体 |

- 序列化吞吐约 **37 k carriers/s**，反序列化约 **24 k carriers/s**。
- 16 ms 帧预算内：序列化约 600 个载体，反序列化约 390 个。
- 10 万载体：序列化 2.72 s，反序列化 4.07 s。

### 3.6 存档路径

| 载体数 | `SaveToFile` | `LoadFromFile` | 文件大小 |
|---|---|---|---|
| 1 000 | 47.5 ms / 3.4 MB 分配 | 49.9 ms / 4.8 MB | 784 KB |
| 10 000 | 598.8 ms / 35 MB | 579.3 ms / 51 MB | 7.84 MB |

线性增长。10 万载体按此推算约 6 s 写、6 s 读、78 MB 文件。

## 四、关键缺陷：Trait 反序列化完全失效

`DeserializeCarrier` 实测恢复数：

| 载体数 | 期望 Trait 数 | 实际恢复 |
|---|---|---|
| 1 000 | 4 000 | **0** |
| 10 000 | 40 000 | **0** |
| 100 000 | 400 000 | **0** |

载体本身能重建（数量正确），但**所有 Trait 静默丢失**。

根因（两处叠加，后果不同）：

1. **`TraitTypeName => GetType().Name` 只返回类名**，`CarrierManager.DeserializeCarrier` 用 `Type.GetType(traitData.typeName)` 解析。`Type.GetType(string)` 只查找 mscorlib 与**调用方程序集**（`NexusFramework`），不查找业务程序集。测试 Trait 在 `Tests` 程序集 → 返回 `null` → **跳过该 Trait，载体仍重建成功**。同理 `GASEntityTrait`（`NexusFramework.GAS` 程序集）也无法恢复。
2. **`BasicInfoTrait` 没有无参构造**（只定义了 `BasicInfoTrait(string name, string description = "")`）。若类型能解析，`Activator.CreateInstance(traitType)` 抛 `MissingMethodException`，异常被 `DeserializeCarrier` 的外层 `catch` 吞掉并 **返回 `CarrierId.Invalid`——整个载体都不会重建**。

**结论：当前项目中没有任何 Trait 能通过存档恢复。** 而且 `BasicInfoTrait` 必须存在（若落在 `NexusFramework` 程序集内），会导致读档时整批载体失败而非仅丢 Trait。现有测试 `DeserializeCarrier_RestoresTags` 只验证了 Tag，未覆盖 Trait，所以该缺陷未被发现。

修复方向（详见 backlog）：`TraitTypeName` 改用 `AssemblyQualifiedName` 或建立「类型名 → Type」注册表；`Activator` 失败时回退到 `FormatterServices.GetUninitializedObject`；补一条 Trait 恢复的回归测试。

## 五、边界判定

| 维度 | 安全区 | 警戒区 | 不可用区 |
|---|---|---|---|
| 单帧 `SerializeCarrier` | ≤ 300 载体（8 ms） | 600 载体（16 ms） | ≥ 1 000 载体（27 ms） |
| 单帧 `DeserializeCarrier` | ≤ 200 载体（8 ms） | 390 载体（16 ms） | ≥ 1 000 载体（41 ms） |
| 系统性存档（可接受卡顿） | ≤ 1 万载体（0.6 s） | 5 万载体（3 s） | 10 万载体（6 s+） |
| `Clone` 普通 Trait | 字段少、无大字符串/集合 | 1 KB 字符串或 100 元素列表 | 4 KB 字符串或 1 000 元素列表 |
| `Clone` SO Trait | 不用于热路径 | ≤ 50 次/帧 | ≥ 100 次/帧 |

## 六、复现方式

```bash
unity command run_tests --mode editor --filter "NexusFramework.Core.Tests.Performance.DataTraitPerformanceTests" --async_tests true --project-path "D:\Work\Unity\NexusFramework"
```

结果：`TestResults/datatrait-perf.txt`；JSON 大小与 Trait 恢复数见编辑器日志中的 `[PERF][SIZE]` / `[PERF][CHECK]`。

## 七、未覆盖项

- **IL2CPP / Player**：`JsonUtility` 是原生实现，绝对耗时接近；`Activator` / `Type.GetType` 在 IL2CPP 下可能更慢且受裁剪影响（`link.xml` 需保留 Trait 类型）。
- **并发存档**：`SaveToFile` 无锁遍历 `carrierTraits.Keys`，多线程写入时的安全性未测。
- **压缩 / 增量存档**：当前全量 `JsonUtility`，未测二进制或增量方案。
