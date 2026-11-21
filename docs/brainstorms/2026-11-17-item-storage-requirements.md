---
date: 2026-11-17
updated: 2026-11-17
topic: item-storage
status: archived
superseded_by: docs/requirements/R007-item-storage.md
---

# 物品与存储需求（Kickoff，已归档）

> **本文不再是需求源。** 现行需求见 [R007 物品与存储子系统](../requirements/R007-item-storage.md)。
> 保留本文是为了记录 Kickoff 阶段的问题发现，以及后续评审中被修正的判断。

## Kickoff 的核心产出

1. **三种理解必须拆开**：物品定义（静态）、物品实例（动态）、"存储"的两种语义（归属存储 / 持久化存储）。
2. **15 项越界需求**：网络同步、UI、掉落、商店、合成、词条 roll、耐久规则、货币、地面掉落、容器嵌套、内容包、存档落盘、GAS 重构、编辑器工具。
3. **三层数据模型**：定义 → Luban 表；实例 → Carrier + Trait；归属 → CarrierIndex。
4. **待决问题 Q1–Q8**：存储范围、实例粒度、系统归属、配置表路线、GAS 桥接、标签体系、容器强弱约束、是否实现"使用"。

## 被修正的六处判断

### 1. R19 序列化方式

原文：物品存档"经框架 Carrier 序列化往返"，只需适配 `JsonUtility` 限制。

修正：逐载体 JSON 为 137 B/件、100 万件约 10.2 s；容器批量快照为 35 B/件、约 0.95 s。物品存档的语义单位是**容器**，不是任意载体集合。现行需求改为 `ContainerSnapshot` + `ItemRecord[]`（R007 RI36）。

### 2. R20 版本迁移

原文：快照带版本号，迁移由存档子系统执行——方向正确，但未区分"声明结构"与"执行迁移"。

修正：物品只声明 `SchemaVersion` 与字段兼容约束，迁移执行归存档子系统（RI37）。

### 3. R21 GAS 桥接

原文：装备/卸下"触发 GAS 效果挂载"，措辞暗示物品直接调用 GAS。

修正：物品只发类型化事件，GAS 侧订阅执行；物品不引用 ECS 或 GAS 类型（RI35）。

### 4. 数字定义 ID

原文：定义主键为 `int`，`ItemCoreTrait.DefinitionId` 为 `int`。

修正：与数据包行键一致，改为字符串全局键 `Key`（`core:item.potion`）；`item.definition` 索引为 `CarrierGroupIndex<string>`；快照存完整键（R007 Q9 / RI16 / RI41）。

### 5. A1 是"阻塞"

原文：`DeserializeCarrier` 无法恢复任何 Trait，A1 修复前物品存档不可用。

修正：A1 代码已修复（AQN + 类型注册表 + 未初始化对象兜底），状态为"待 Unity 验证"；仍是存档验收的前置（G5）。

### 6. Q4 配置表路线

原文：A（先落地 Config）/ B（复用 GAS ConfigModel）/ C（物品内最小加载器）。

修正：固定为 Config 适配器（`ConfigItemDefinitionProvider<TRow>`），但接口 `IItemDefinitionProvider` 先行，Config 未就绪期间允许内存实现（R007 RI4 / G3）。

## 仍然成立的部分

- 三层数据模型（定义 / 实例 / 容器）；
- 堆叠 = 单 Carrier + 数量；
- 索引三分（`item.container` / `item.slot` / `item.definition`）及基数约束；
- 全量 Carrier 规模决策（上限约 50 万）；
- 越界需求的归属裁决（已迁入边界分析）。

## 关联

- 现行需求：[R007 物品与存储子系统](../requirements/R007-item-storage.md)
- 边界裁决：[Config 与物品需求边界分析](2026-11-17-config-item-boundary-analysis.md)
- 内容包模型：[内容包模型结论](2026-08-01-content-pack-quadruple-kickoff.md)
