---
date: 2026-08-01
updated: 2026-11-17
topic: data-pack-config
status: archived
superseded_by: docs/requirements/R006-data-pack-config.md
---

# 数据包需求（2026-08-01 原始需求，已归档）

> **本文不再是需求源。** 现行需求见 [R006 数据包子系统](../requirements/R006-data-pack-config.md)。
> 保留本文是为了记录三条关键假设的推翻过程——它们是后续多源设计（内容包、程序包、契约包）的起点。

## 原始结论摘要（R1–R11）

| 旧条目 | 结论 | 现状 |
|---|---|---|
| R1 数据包四部分交付 | 结构 + 程序集 + 数据 + 清单 | 成立，但程序集不再属于"数据包"（见下） |
| R2 包清单 | packId / priority / dependencies / tables | 成立；packId 改为命名空间式 |
| R3 Luban 表结构 | xlsx + luban.conf + gen | 成立 |
| R4 包级程序集与命名空间 | 每包唯一命名空间 | 部分成立：程序集是所有内容包的载体，不是数据包的组成部分 |
| R5 流程规范与生成引导 | gen 模板产出注册类 | 成立（R006 RP11） |
| R6 TableModel 统一存储 | 表→Model | 成立（RP14） |
| R7 注册 API | 携带包标识与优先级 | 成立；数据源改为注册时传入（RP28） |
| R8 框架查询 | `GetModel<TableModel>()` | 成立（RP15–RP18） |
| R9 行级覆盖合并 | 主键覆盖/追加/废除 | 成立；主键改为字符串全局键（RP9a） |
| R10 模板与一键复制 | 骨架资产 | 成立（RP12） |
| R11 Demo 静态引用验证 | 三包共存 | 成立（RP32） |

## 被推翻的三个假设

### 1. 数字主键（int ID）

原文：主键为单字段 `int ID`，合并键即主键。

推翻原因：多源（本体 / DLC / mod）场景下，两个包各自使用 `1001` 时合并层无法区分"新增"还是"覆盖"——冲突不是概率问题，是语义缺失。CarrierId 重构与 Minecraft 1.13 扁平化（数字 ID → `namespace:path`）是同一判断的先例。

现行方案：字符串行键 `<packId>:<localKey>`（`core:item.potion`），冒号为唯一分隔符；命名空间段即归属声明。

### 2. 数据包自带强类型程序集

原文："Luban 生成的强类型程序集进各包自己的程序集，框架对表类型零知识"。

推翻原因：Luban 多源能力实测（[验证文档](2026-11-17-luban-multi-source-capability.md)）表明——跨程序集同名 `Type` 不相等（`typeof(cfg.item) != typeof(cfgvendor.item)`），同命名空间编译冲突（CS0433），IL2CPP 无法运行期加载程序集。因此"每包自己生成一份 schema 代码"在多源运行时不可行。

现行方案：所有内容包都有程序集（用于注册入口、加载器、自定义类型），但**行类型只由本体提供**；mod 的数据走行 JSON 合并。第三方代码扩展通过契约包实现（内容包模型 C5）。

### 3. 核心提供默认数据源目录

原文：`Resources/DataPacks/{packId}/` 为框架默认约定。

矫正原因：内容包各自决定数据从哪读（Resources / AssetBundle / Addressables / 远程），核心不应规定存放位置。

现行方案：加载能力在 `NexusFramework.Config`（`IDataPackDataSource` + 内置实现），内容包在注册时选择或实现自己的 source（R006 RP28–RP30）。

## 仍然成立的部分

- 四部分交付物、包清单字段、Luban 工具链与命名约定；
- `TableModel` 作为统一存储，`GetModel<TableModel>()` 获取；
- 行级覆盖（整行替换）、`is_void` 废除与反转、同表判定；
- 骨架模板、一键复制、schema 差异校验；
- 零 GAS 引用的测试链路与 Demo 三包验证。

## 关联

- 现行需求：[R006 数据包子系统](../requirements/R006-data-pack-config.md)
- 内容包模型：[内容包模型结论](2026-08-01-content-pack-quadruple-kickoff.md)
- 能力基线：[Luban 多源数据能力验证](2026-11-17-luban-multi-source-capability.md)
- 边界裁决：[Config 与物品需求边界分析](2026-11-17-config-item-boundary-analysis.md)
