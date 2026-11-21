---
layer: progress
status: draft
topic: perf-impact-on-design
created: 2026-11-17
updated: 2026-11-17
related:
  - docs/progress/2026-11-17-carrier-performance-baseline.md
  - docs/progress/2026-11-17-datatrait-performance-baseline.md
  - docs/progress/2026-11-17-carrier-trait-hotpath-baseline.md
  - docs/progress/2026-11-17-factory-scenario-baseline.md
  - docs/progress/2026-11-17-carrier-issues-backlog.md
---

# 性能基线对 Config / 物品系统设计的影响

> 本文只做**映射与修订建议**，不重复基线数据。

## 结论速览

| 影响 | 严重度 | 处置 |
|---|---|---|
| **A1 Trait 反序列化完全失效** | 阻塞 | 物品存档在当前 Core 上**根本无法工作**，必须列为物品系统 P0 前置 |
| **1M 物品全量 Carrier 需 849 MB / 19 s** | 架构级 | 「实例 = Carrier」在工厂类规模下不成立，需要冷热分离；RPG 背包规模可豁免 |
| **逐物品 JSON 存档在 >1 万时不可用** | 设计级 | R19 的「不引入第二套序列化格式」必须重新裁决为容器批量格式 |
| **按 definitionId 全扫 222 ms vs 索引 28 µs** | 设计级 | D007 缺少定义索引，必须补 `CarrierGroupIndex<int>` |
| **容器 Get 每次拷贝（1000 成员 17.8 µs / 8 KB）** | 设计级 | 查询接口需要零拷贝/缓冲复用 |
| **Config 表查询** | 无影响 | `TableModel` 不涉及 Carrier/Trait，现有接口可满足 |

---

## 一、直接影响矩阵

| 基线结论 | 命中现有文档 | 处置 |
|---|---|---|
| `DeserializeCarrier` 恢复 0 个 Trait | 物品 R19/R20、D007 §3.3 | **阻塞**：A1 修复前不实现物品存档 |
| `GetTrait` 149 ns / `HasTrait` 120 ns，零分配 | D007 实例读取 | ✅ 设计成立 |
| `AddTrait` 362~548 ns、68~166 B | D007 物品创建 | ✅ 可接受；创建需分帧 |
| 每载体多 1 个 Trait，销毁 +210 ns | D007 四个 Trait | 建议合并 Trait（Core + Durability） |
| `FindCarriersWithTrait` 1 万结果 430 µs / 80 KB | D007 `Find` | 物品查询不得依赖全量 Trait 反查 |
| `CarrierIndex.Bind` 无冲突 209 ns / 170 B | D007 槽位索引 | ✅ 可接受 |
| `CarrierGroupIndex.Get` 1000 成员 17.8 µs / 8 KB | D007 `GetItems` | 需零拷贝接口 |
| 移动 1.17 µs、生成+销毁 3.13 µs | D007 操作 | 每帧 ≤ 5 000 搬运、≤ 2 000 生成销毁 |
| 1000 活跃容器 tick 20.5 ms | D007 无 tick 设计 | 物品系统本身不 tick；业务侧需分帧 |
| 容器批量存档 0.95 s / 33 MB（1M） | D007 无存档设计 | 新增快照格式 |
| 紧凑 struct 12 B/物品 | D007 全 Carrier | 冷热分离（见 §2.2） |
| `TraitTypeName` 59.3 B/次 | 物品批量存档 | 随 A1/B5 修复 |

---

## 二、物品系统的四个结构性问题

### 2.1 A1：Trait 反序列化失效（阻塞）

- **事实**：1 000 / 10 000 / 100 000 载体存档回读后 Trait 恢复数均为 0。
- **对物品系统的含义**：`ItemCoreTrait`、`ItemDurabilityTrait`、`ItemAffixListTrait`、`ContainerTrait` 全部无法恢复。容器 Carrier 能重建，但内容为空——**静默数据丢失**。
- **额外约束**：物品 Trait 必须提供**无参构造**（`Activator.CreateInstance` 路径），且若 Core 改用 `AssemblyQualifiedName`，物品 Trait 的序列化数据格式会变化，迁移需在首次发布前完成。
- **建议**：A1 列为物品系统 Unit 0 前置；修复同时补「Trait 往返」回归测试；物品 Trait 在 Unit 1 就声明无参构造约束。

### 2.2 冷热分离：不是所有物品都该是 Carrier

工厂场景实测：

| 形态 | 1M 物品内存 | 构建 | 存档 |
|---|---|---|---|
| 全量 Carrier | 849 MB | 19.4 s | 10.2 s / 130 MB |
| 紧凑 struct | 11.4 MB | — | 0.27 s / 11.5 MB |

- **RPG 背包**（单容器 ≤ 数百、总数 ≤ 数千）：全量 Carrier 完全可行（安全区 ≤ 20 万）。
- **工厂/模拟类**（10 万~100 万物品）：必须冷热分离——静态库存用紧凑数组，只有活跃容器、UI 可见物品创建 Carrier。

**决策（2026-11-17）**：**采用全量 Carrier，不做冷热分离**。读档 30–300 s 可接受，真正的约束是内存：10 万≈85 MB、50 万≈425 MB、100 万≈850 MB。目标上限 50 万。核心 API 不引入 `IItemStorage` 抽象；超过上限时再评估。

### 2.3 存档格式必须重新裁决

- 原 R19：「不引入第二套序列化格式」——本意是复用 Carrier 序列化。
- 实测：逐物品 JSON 137 B/物品、10.2 s/1M；容器批量 35 B、0.95 s；二进制 12 B、0.27 s。
- **裁决建议**：物品存档采用**容器批量快照**（`ContainerSnapshot` + `ItemRecord[]`），不依赖 `CarrierManager.SaveToFile`。理由：
  1. 存档语义是"容器内容"，不是任意载体集合；
  2. 体积与速度差 4~10 倍；
  3. 单一 schema 更利于版本迁移。
- 这仍满足 R19 的初衷（不重复造实体系统），但**显式放弃了逐载体 JSON**。

### 2.4 查询必须走索引，且零拷贝

- 按 definitionId 全扫 1M：221.7 ms；走 `CarrierGroupIndex<int>`：28.3 µs（**7 800 倍**）。
- `CarrierGroupIndex.Get(container)` 每次拷贝：1000 成员 17.8 µs / 8 KB。
- **建议**：
  - 补 `"item.definition"` 索引（`CarrierGroupIndex<int>`）；
  - `GetItems` 提供 `int CopyItemsTo(ContainerId, List<ItemHandle>)` 与 `IEnumerable` 双形态；UI 热路径用前者；
  - `CountInContainers` 优先走定义索引，不遍历容器。

### 2.5 补充：Trait 数量与事件成本

- 每个 Trait 增加销毁 +210 ns、写入 +5~10 B。物品实例建议**最多 2~3 个 Trait**：把 Durability 合入 `ItemCoreTrait`，词条保留独立（低频）。
- 每帧 5 000 次搬运若各发一次事件，订阅方成本会放大。建议提供**帧末批量事件**选项（`ItemBatchChangedEvent`），或文档约束"高频搬运用静默 API"。

---

## 三、对 Config 的影响

- **基本无影响**：`TableModel` 不涉及 Carrier/Trait 核心路径，`ITableReader` 查询是字典 O(1)。
- **一个注意点**：`ITableReader.All()` 若用迭代器，每次遍历分配一个迭代器对象（约 32 B）；若表行数大且每帧遍历，建议实现为「返回缓存的只读集合视图」或提供 `CopyRowsTo(List<TRow>)`。
- **Config 的 1000 张表 × 行**：注册期一次性反射/反序列化，与运行时帧预算无关。
- **与 A1 无关**：表数据走 Luban 反序列化，不经过 `DataTrait`。

---

## 四、修订清单

| 文档 | 修订 |
|---|---|
| `docs/brainstorms/2026-11-17-item-storage-requirements.md` | R19/R20 改为容器批量快照；数量级假设改为「默认 RPG 规模，工厂规模走可选后端」；Dependencies 增加 A1 前置与性能边界引用 |
| `docs/design/D007-item-storage-interface-design.md` | 补定义索引、零拷贝查询、存档快照、Trait 合并建议、无参构造约束、性能边界章节 |
| `docs/design/D006-data-pack-interface-design.md` | 补一条：`ITableReader.All()` 的分配约束与可选批量拷贝接口 |
| `docs/plans/2026-08-01-001-...-plan.md` | 无修订（Config 不受影响） |

---

## 五、目标规模（已决策）

**决策（2026-11-17）**：采用全量 Carrier，不做冷热分离。目标上限 50 万物品实例。

| 物品总数 | 驻留内存 | 冷构建 | 读档（A1 修复后估算） |
|---|---|---|---|
| 10 万 | ≈ 85 MB | ≈ 2 s | ≈ 4 s |
| 50 万 | ≈ 425 MB | ≈ 10 s | ≈ 20 s |
| 100 万 | ≈ 850 MB | ≈ 19 s | ≈ 40 s |

读档 30–300 s 可接受；**内存是硬约束**（100 万约 1.7 GB 属 Mono 风险区）。此项不再作为开放问题。超过 50 万时再评估冷热分离。

---

## 六、建议的工作顺序调整

原计划「Config 一期 → 物品垂直切片」，在性能数据之后应调整为：

1. **A1 修复**（P0，1~2 天）—— 物品存档的前置，与 Config 并行；
2. **B4 Trait 字典惰性化 + B3 枚举零拷贝**（P1）—— 直接改善物品内存与查询接口形态；
3. **Config Unit 1**（接口契约）—— 不受性能影响，可同时进行；
4. **物品 Unit 1**（接口契约）—— 按本文修订后的 D007；
5. 物品垂直切片 —— 在 A1 完成后才能验证存档往返。

B1/B2（Tag 优化）与物品无直接关系，可排在其后。
