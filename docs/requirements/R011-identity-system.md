---
layer: requirements
status: draft
topic: identity-system
origin:
  - docs/requirements/R006-data-pack-config.md
  - docs/requirements/R007-item-storage.md
  - docs/brainstorms/2026-11-17-config-item-boundary-analysis.md
  - Assets/NexusFramework/DataCarrier/CarrierId.cs
  - Assets/NexusFramework/DataCarrier/TagRuntimeIndex.cs
  - Assets/NexusFramework.GAS/Services/EcsTagService.cs
  - TestResults/id-handle-perf.txt
created: 2026-11-17
updated: 2026-11-18
---

# 需求：通用标识（Identity）子系统

## 0. 本文档的定位

本文是框架层**运行时标识能力**的正式需求文档。它只服务**运行时内存数据**，不服务持久化输入。

核心分层：

| 层 | 内容 | 标识 | 生命周期 |
|---|---|---|---|
| **L1 持久化输入** | 配置表、资源、本地化、存档 | 字符串 key | 跨会话、跨进程 |
| **L2 运行时数据** | 定义、类型、实例、临时对象 | int id | 本次运行、本架构 |
| **ID 管理器** | key→id 映射、发号、类型标签 | — | 与架构同生命周期 |

- L1 只认字符串 key。
- L2 只认 int id。
- 边界转换发生在加载、注册、存档、读档四个点。
- **id 不进入任何持久化格式。**

本文只定义「系统必须能做到什么」，接口设计由 [D008](../design/D008-identity-interface-design.md) 承接。

---

## 一、背景

框架里存在四套局部 ID 机制：

| 机制 | 稳定标识 | 运行时值 | 分配方式 | 问题 |
|---|---|---|---|---|
| `CarrierManager` 类型注册 | `typeName`（裸字符串） | `ushort typeId` | 惰性递增 | 15 位上限；跨会话不稳定 |
| `CarrierId` | 无 | `ulong`（8+16+40 位域） | 复合打包 | 位域无统一描述；持久化存 RawValue |
| `TagRuntimeIndex` / `RuntimeTagSlot` | Tag key | `int slot` | 惰性递增、不复用 | 槽位只在本 manager 有效 |
| `EcsTagService` | GAS 标签 key | `int tagId` | 配置驱动 | 与 Core Tag 不互通 |

四套实现都在做同一件事：把字符串映射成整数。但各自重复实现、语义不一致，且没有区分“持久化输入”与“运行时数据”：

- 配置行键、资源 key 是 L1，跨会话稳定，不能变成数字 id；
- Carrier 类型、实例是 L2，运行时构建，id 只在本次运行有效；
- 之前的 `CarrierId` 把 L2 的 id 写进存档，导致读档时 typeId 漂移。

分层之后，这个问题从源头消失：id 不出 L2。

---

## 二、目标与非目标

### 目标

1. **分层清晰**：持久化输入只用 key，运行时数据只用 id，边界显式转换。
2. **三个通用原语**：`KeyIndex`（key↔id）、`IdAllocator`（纯发号）、`TypedAllocator`（发号 + 类型标签）。
3. **每架构独立**：原语实例归属于 `IArchitecture`，域内独立编号。
4. **完全裸 int**：不提供泛型包装；类型安全由模块边界与命名约定保证。
5. **Carrier 重构**：`CarrierId` 移除；类型走 `KeyIndex`，实例走 `TypedAllocator`。
6. **可验证的性能**：热路径零分配，现有基线不回退。

### 非目标

- 不定义存档文件格式、加密、迁移策略（存档子系统）。
- 不定义内容包的发现、清单、生命周期（R008）。
- 不定义 Luban 表结构或配置加载流程（R006）。
- 不定义网络协议。
- 不统一 GAS 的业务语义，只统一其运行时 ID 基础设施。
- 不提供代际（generation）检测。
- 不保证 id 跨进程、跨架构、跨构建一致。

---

## 三、术语

| 术语 | 定义 |
|---|---|
| **L1 持久化输入** | 配置文件、资源清单、本地化表、存档。只用字符串 key 标识。 |
| **L2 运行时数据** | 加载后的定义、注册的类型、运行时实例。只用 int id 标识。 |
| **稳定 key** | `<namespace>:<path>` 形式的字符串标识，用于 L1 与 L2 的边界。 |
| **KeyIndex** | key ↔ id 的双向索引，服务 L2 中有名字的数据。 |
| **IdAllocator** | 纯整数发号与回收，服务无名字的运行时对象。 |
| **TypedAllocator** | 发号并记录 typeId 的分配器，服务多类型运行时对象。 |
| **typeId** | 运行时类型编号，通常来自某个 `KeyIndex`。 |
| **discriminator** | 实例→类型的映射（`typeById`），用于运行时类型判断。 |
| **不安全复用** | 回收后的 id 可被再次分配，旧引用可能指向新对象。 |

---

## 四、需求

### A. 分层与边界

- **RID1.** 持久化输入（配置、资源、本地化、存档）只能使用字符串 key，不得使用数字 id。
- **RID2.** 运行时数据只能使用 int id，不得在运行时保存持久化 key 作为主标识。
- **RID3.** id 不得写入任何持久化格式（存档、配置、网络包、资源清单）。
- **RID4.** 边界转换只发生在四个点：加载配置、注册类型、写存档、读档。转换必须由 ID 管理器提供，不得由业务自行拼接。
- **RID5.** 读档时必须按 key 重建 L2 数据与 id；不得依赖存档中不存在或已漂移的旧 id。
- **RID6.** 运行时对象之间的引用只能使用 id；跨架构、跨进程、跨会话的引用只能使用 key。

### B. KeyIndex

- **RID7.** `KeyIndex` 必须提供 key→id 与 id→key 两个方向的 O(1) 查询。
- **RID8.** `KeyIndex` 服务 L2 中有名字的数据：物品定义、Carrier 类型、GAS 效果/属性/标签、本地化条目、资源配置等。
- **RID9.** key 的来源可以是 L1 资产键（如 `core:item.potion`）或 L2 注册名（如 `core:player`）；`KeyIndex` 不区分两者，只负责映射。
- **RID10.** `Intern(key)` 幂等：已存在返回既有 id；不存在则注册并返回新 id。注册失败必须返回诊断，不得返回 `0` 当作有效值。
- **RID11.** `KeyIndex` 必须支持 `Remove(key)`：删除映射并回收 id。回收后的 id 可被再次分配。
- **RID12.** key 必须按 `<namespace>:<path>` 规范化；冒号为唯一分隔符。是否允许无命名空间的 legacy 键由选项决定。
- **RID13.** 命名空间归属校验必须可注入、可缺失；不得硬依赖未建的内容包子系统。
- **RID14.** 同一索引内重复注册同一 key：同源重复视为错误；无法判定来源时保留先者并告警，不得静默覆盖。
- **RID15.** `KeyIndex` 必须提供诊断：条目数、容量、非法键计数、冲突计数。

### C. IdAllocator

- **RID16.** `IdAllocator` 只负责发号与回收，不持有、不校验、不解释任何字符串。
- **RID17.** 编号从 `1` 开始；`0` 恒为无效。分配失败必须返回失败，不得返回 `0` 当作有效值。
- **RID18.** 必须支持回收：`Free(id)` 后槽位进入空闲表，后续 `Allocate` 优先复用。
- **RID19.** 本期不提供代际检测；回收后旧 id 可能指向新对象，这是接口契约，使用方必须在销毁时不再持有 id。
- **RID20.** 槽位必须连续，业务可用 `T[] byId` 直接索引。
- **RID21.** `Capacity` 单调不减；活跃数与容量分离。
- **RID22.** 必须提供 `Allocate`、`Free`、`IsAlive`、`Count`、`Capacity`、`Clear`。重复 `Free`、`Free(0)`、越界 `Free` 必须返回失败且不破坏状态。
- **RID23.** `IdAllocator` 服务无名字、不需要类型判断的运行时对象：临时子弹、粒子、匿名实体。

### D. TypedAllocator

- **RID24.** `TypedAllocator` 在发号与回收的基础上，必须维护 `typeById`（discriminator）。
- **RID25.** `Allocate(typeId)` 必须记录该实例的类型；`TryGetType(id)` 必须 O(1) 返回类型。
- **RID26.** `typeId` 由调用方提供，通常来自某个 `KeyIndex` 的 id；`TypedAllocator` 不负责分配类型。
- **RID27.** 类型数量只受 `int` 限制，不得使用 `ushort` 等窄位宽；不得把类型编码进 id 的位域。
- **RID28.** `TypedAllocator` 服务多类型运行时对象：Carrier 实例、GAS ASC、效果实例等。
- **RID29.** 必须提供 `IsType(id, typeId)` 与 `TryGetType(id, out typeId)`；不得要求调用方自行比较位段。
- **RID30.** 回收与 `IdAllocator` 一致：无代际，id 复用，使用方负责不持有失效引用。

### E. 归属与生命周期

- **RID31.** 每个 `IArchitecture` 实例持有独立的 `IdRegistry`；不同架构的同名域互不共享编号。
- **RID32.** 每个域只注册一种原语（`KeyIndex` / `IdAllocator` / `TypedAllocator`）；同名域以不同原语创建必须失败并诊断。
- **RID33.** 域名在架构内唯一；重复创建必须返回既有实例或显式失败，不得静默替换。
- **RID34.** 架构 `Shutdown` 时必须清理全部原语；重启后旧 id 一律失效。
- **RID35.** 跨架构、跨进程传递引用只能使用 key；id 不出运行时边界。

### F. Tag 集成

- **RID36.** Tag 的 key→slot 映射必须由 `KeyIndex` 提供；`TagRuntimeIndex` 作为独立映射实现删除。
- **RID37.** `CarrierTagService` 只负责挂载关系、反向索引、查询与事件；构造时注入 `KeyIndex`（建议域名 `core.tag`），不自行维护 key 映射。
- **RID38.** Tag 的层级语义（父 Tag 命中子 Tag）留在 Tag 服务内，由服务维护 slot→祖先链；`KeyIndex` 不解析点分层级。
- **RID39.** Tag 索引器不得启用 `Remove`；现有 Tag API 没有删除 key 的操作，避免 slot 回收与挂载表脱节。
- **RID40.** Tag 的 key 规范化沿用点分层规则（`TagKey`）；`KeyIndex` 只做通用的空值与首尾空白校验，不强制命名空间。

### G. Carrier 重构

- **RID41.** 移除 `CarrierId` 及其 `8+16+40` 位域设计。
- **RID42.** Carrier 类型属于 L2 数据，不是 L1 输入资产；类型标识由 `KeyIndex` 管理（建议域名 `carrier.type`）。
- **RID43.** Carrier 类型编号必须是 `int`，支持 10 万级以上类型；不得使用 `ushort`。
- **RID44.** Carrier 实例由 `TypedAllocator` 发号（建议域名 `carrier.instance`），`typeId` 来自 `carrier.type`。
- **RID45.** Carrier 实例 id 必须是 `int`；不得包含架构位、类型位或实例位。
- **RID46.** 按类型分组查询由 `CarrierManager` 维护（`typeId → id 集合`），作为业务二级索引；`TypedAllocator` 只负责 discriminator。
- **RID47.** 序列化只写 `typeKey`、Trait 内的稳定 key 与 Tag key；不写 `CarrierId` 或任何实例 id。
- **RID48.** 旧存档若只含 `CarrierId.RawValue`，必须显式拒绝或走独立迁移，不得按旧数字静默重建。

### H. 性能

- **RID49.** key→id 为单次哈希查找；id→key 为数组直索引；发号/回收均摊 O(1)；类型判断为一次数组读。热路径不得产生迭代器分配、装箱或反射。
- **RID50.** 引入 Identity 原语后，Carrier 创建/销毁/查询与 Tag 读写的现有基线不得回退超过 5%。
- **RID51.** 如需在 IL2CPP Player 侧复测，必须记录句柄形态与热路径数据；结论不得只基于 Mono/Editor。

### I. 迁移与兼容

- **RID52.** 迁移分批、每批独立可回滚：
  1. 新增 `KeyIndex` / `IdAllocator` / `TypedAllocator` 与单元测试，不改现有模块；
  2. `CarrierManager` 类型注册改 `KeyIndex`，实例改 `TypedAllocator`，移除 `CarrierId`；
  3. 全仓库 `CarrierId` 引用改为 `int`；
  4. Core Tag 改 `KeyIndex`（删除 `TagRuntimeIndex`）；
  5. GAS Tag 与 ASC 接入；
  6. 配置 / 物品 / 后续模块按需接入。
- **RID53.** 迁移期允许 legacy 裸键；是否强制命名空间由 `KeyIndex` 选项控制。
- **RID54.** 每批迁移必须有测试覆盖：发号/回收/复用、key↔id 往返、删除后旧 id 行为、架构重启失效、旧存档处理。
- **RID55.** `CarrierId` 移除属于破坏性变更，必须列出全部受影响 API 与替代方式。

---

## 五、越界需求裁决

| # | 条目 | 归属 |
|---|---|---|
| X1 | 存档文件格式、加密、槽位、迁移函数 | 存档子系统（未建） |
| X2 | 网络同步协议与会话协商 | 未来同步层 |
| X3 | 内容包发现、清单、启停 | R008（待写） |
| X4 | Luban 表结构与加载流程 | R006 |
| X5 | 本地化 / 资源 key 的解析与路由 | R009 / R010 与资源包 |
| X6 | GAS 效果/属性的业务语义与数值规则 | GAS 模块 |
| X7 | 编辑器工具（ID 浏览器、冲突报告 UI） | 后续 Editor 工具 |
| X8 | 代际检测、悬垂句柄防护 | 不在本期 |
| X9 | 数字 id 的跨版本稳定化 | 不做；稳定标识只有字符串 key |

---

## 六、依赖与假设

| # | 依赖 / 假设 | 状态 |
|---|---|---|
| G1 | R006 已确立 `<packId>:<localKey>` 与命名空间归属规则 | 已定稿 |
| G2 | R007 已把物品定义键、效果键、词条键改为字符串键 | 已定稿 |
| G3 | 现有 `CarrierId` 为 8+16+40 位域，被 Carrier/Tag/GAS 广泛引用 | 已核实；移除是破坏性变更 |
| G4 | 句柄形态基线：裸 int 包装与泛型包装在实现 `IEquatable` 后无实质差异 | 已实测 |
| G5 | Unity 2022.3 / C# 9 | 已核实 |
| G6 | 现有 `TagRuntimeIndex` 为惰性递增、不复用 | 已核实 |
| G7 | 现有 `EcsTagService` 为配置驱动的封闭集合 | 已核实 |
| G8 | 存档子系统尚未建设 | 未建 |

---

## 七、问题状态

### 已决策

| 问题 | 结论 |
|---|---|
| Q1 分层 | L1 只用 key，L2 只用 id，id 不持久化 |
| Q2 原语 | 三个：`KeyIndex`、`IdAllocator`、`TypedAllocator` |
| Q3 编号空间 | 每域独立；不做全局唯一 |
| Q4 句柄形式 | 完全裸 int，不提供泛型包装 |
| Q5 回收 | 允许回收复用，无代际；使用方负责不持有失效 id |
| Q6 `CarrierId` | 彻底移除；类型走 `KeyIndex`，实例走 `TypedAllocator` |
| Q7 归属 | 每架构一份 `IdRegistry` |

### 未决（不阻塞需求定稿，需设计阶段确认）

- 是否保留非泛型的强类型别名（如 `CarrierHandle` 包装 int），还是 API 全部使用裸 int。
- `KeyIndex` 的 key 是否允许删除（当前允许；Tag 索引器不使用）。
- 分块数组 vs 字典用于 `typeById` 的实现选择。
- 域名的分层规范与命名空间治理。
- `CarrierManager` 依赖的窄接口边界。
- Trait 内 ID 引用字段的声明方式（接口 / attribute / 专用类型）。
- 旧存档的断代策略。

---

## 八、关联文档

- 数据包键规范：[R006 数据包子系统](R006-data-pack-config.md)
- 物品定义键：[R007 物品与存储](R007-item-storage.md)
- 索引机制：[D005 Carrier 索引表](D005-carrier-index-registry.md)
- 接口设计：[D008 通用标识接口契约](../design/D008-identity-interface-design.md)
- 性能基线：`TestResults/id-handle-perf.txt`、[Carrier 性能基线](../progress/2026-11-17-carrier-performance-baseline.md)
