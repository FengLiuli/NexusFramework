---
title: 数据包子系统：NexusFramework.Config 库接口设计与实现
type: feat
status: active
date: 2026-08-01
origin: docs/brainstorms/2026-08-01-data-pack-requirements.md
---

# 数据包子系统：NexusFramework.Config 库接口设计与实现

## Overview

为 NexusFramework 增加**数据包子系统**：新建 `NexusFramework.Config` 库，按**接口定义模式**先行交付全部接口契约（仅接口声明与 XML 注释，无实现），经评审确认后实现 `TableModel` 统一存储、注册 API、行级覆盖合并、默认数据源与 Editor 工具，并以 Demo 本体/mod 双数据包（静态引用模拟）+ 零 GAS 测试工程完成端到端验证。业务开发者经 `GetModel<TableModel>()` 框架链路读取配置，全程无手写加载代码。

## Problem Frame

配置表能力目前绑定在 GAS 库内（`IConfigLoader` + `ConfigModel` 私有缓存），Luban 表结构与生成代码散落在 Demo 层；Core 库无任何表加载能力，非 GAS 模块获取配置只能复制 GAS 私有链路（见 origin: Problem Frame）。本次以"数据包"交付物（表结构 + Luban 强类型程序集 + 表数据 + 包清单）落地框架原生配置能力，支持本体/DLC/mod（静态引用模拟）行级覆盖合并，mod 运行时加载与内容包整体架构属后续范畴（见 origin: Scope Boundaries）。

## Requirements Trace

- R1. 数据包四部分自包含交付（表结构/强类型程序集/表数据/包清单）→ Unit 1（清单契约）、Unit 6/7（包结构落地）
- R2. 包清单含包标识/优先级/依赖/表清单字段（四元组公共上下文预留）→ Unit 1（DataPackManifest）
- R3. 表结构基于 Luban 工具链，规范目录与命名约定 → Unit 5（模板）、Unit 6（luban.conf/骨架 xlsx）
- R4. 强类型代码进各包程序集、包级唯一命名空间、框架库零表类型知识 → Unit 1（契约）、Unit 3（约定内省）
- R5. gen 流程标准化 + 引导类/启动接入代码由模板产物生成（非手写）→ Unit 5（后处理生成器）
- R6. `TableModel`（继承 `AbstractModel`）统一运行时存储 → Unit 2
- R7. 注册 API 携带包标识与优先级 → Unit 1（契约）、Unit 2（实现）
- R8. `GetModel<TableModel>()` 后泛型表/行查询 → Unit 2
- R9. 行级覆盖合并全套规则（同表判定/主键约定/优先级/废除语义/JSON 重解析/ref 查询期解析/诊断拒绝）→ Unit 3
- R10. 骨架模板 + 一键复制工具 → Unit 5
- R11. Demo 静态引用多包模拟 + 约定内省合并（与未来动态路径共享合并代码）→ Unit 3、Unit 7
- AE1（覆盖合并结果）→ Unit 8 测试场景；AE2（无感知数据来源包）→ Unit 8；AE3（废除/反转）→ Unit 3/8；AE4（零 GAS 全链路）→ Unit 8；AE5（端到端无手写加载）→ Unit 6/7 验证

## Scope Boundaries

- 本次只交付**接口契约定义 + 实现 + Demo 验证**；运行实现前必须完成接口评审（Unit 1 为确认门）
- mod 仅以静态引用模拟（同构数据包实例），不含运行时加载/分发
- 程序包/资源包/本地化包实现、内容包发现加载管线、远程/热更表源、非 Luban 表源均不做（仅预留字段/扩展点）
- 框架库对具体表类型零知识：所有行类型经注册参数传入，内部仅按约定反射访问

### Deferred to Separate Tasks

- GAS 库重构（删除私有 `ConfigModel`/`IConfigLoader`、改从 `TableModel` 读取、GAS.Editor 工具链迁出）：Config 库完成后作为独立任务，验收标准为 GAS asmdef 对 Luban 完全无感知
- Luban CLI 下载与钉版（`Tools/Luban/Luban.dll` 当前缺失）：AE5 端到端前置小任务，与库实现并行准备
- 动态加载路径的引导类发现契约：本次仅保证 `IDataPackEntry` 契约稳定与合并代码共享，Assembly.Load 反射调用契约为实现期定稿

## Context & Research

### Relevant Code and Patterns

- `Assets/NexusFramework/Framework.cs`：`AbstractModel : IModel`（第 973 行）、`GetModel<T>()` 获取链路、`IArchitecture`/`IUtility` —— `TableModel` 继承与接入模式
- `Assets/NexusFramework.GAS/Config/IConfigLoader.cs`：`IUtility` 接口解耦模式（GAS asmdef 零 Luban 引用）—— 接口契约风格参照
- `Assets/NexusFramework.GAS/Models/ConfigModel.cs`：现有 GAS 私有字典 + Register/Get 模式（本次泛化替代的对象）
- `Assets/NexusFramework.GAS/Editor/CodeGen/LubanConfigLoaderGenerator.cs`：MenuItem + IndentedWriter + GASSettingAsset 的生成器先例 —— Unit 5 后处理生成器参照
- `Assets/Demo/Emberheart/GAS/TableClass/`（`Tbunit.cs`、`exgas/Tbability.cs`、`Tables.cs`、`unit.cs`）：Luban 生成形状 —— `Tables(Func<string, JSONNode> loader)`、`DataMap`/`Get`/`GetOrDefault`、静态 `DeserializeXxx(JSONNode)`、`_dataMap.Add(_v.ID, _v)` 主键约定（int 大写 ID 单键，已实测）、全部 `partial`
- `Assets/Demo/Table/luban.conf`（`topModule: cfg`、c/s/e 组）、`gen.bat`（`dotnet Luban.dll -t client -c cs-simple-json`）：工具链配置形状，骨架模板蓝本
- `Assets/Demo/DataGenerated/Luban/Json/GAS/tbunit.json`：JSON 表数据形状（含 `ID` 字段的对象数组）
- `Assets/NexusFramework/NexusFramework.asmdef`（零引用）、`Assets/NexusFramework.GAS/NexusFramework.GAS.asmdef`（GUID 引用 `0de6792a94c3d1943a2a75abf64597f1` 引 NexusFramework，无 Luban）、`Assets/Demo/Demo.asmdef`（引 Luban.Runtime + ECS 栈）、`Assets/Tests/Tests.asmdef`（`UNITY_INCLUDE_TESTS` + 预编译 nunit 引用）：程序集边界先例
- `Assets/Tests/Editor/LubanConfigLoaderGeneratorTests.cs`：Editor 测试先例
- `Packages/manifest.json`：`com.code-philosophy.luban`（focus-creative-games/luban_unity.git）

### Institutional Learnings

- `docs/solutions/` 目录不存在，无既有解决方案沉淀

### External References

- Luban 官方文档外查失败（lubanspec.github.io 404、搜索仅笼统结果）——生成形状以 Demo 实测产物为准（见上），Luban 定制模板能力不做外部依赖

## Key Technical Decisions

- **D1 查询接口形状：`GetTable<TRow>(string tableName)`（表名 + 行类型）**（原规划分叉点 1，已确认采纳）。依据 R4：框架对表类型零知识；行类型由调用方编译期持有，内部按约定内省。备选 `GetTable<Tbunit>()`（内省 Luban 表容器形状）耦合过深，否决。
- **D2 注册 API 层级：包级 `RegisterDataPack` + 表级原语 `RegisterTable` 双暴露**（原规划分叉点 2，已确认采纳）。包级兑现"引导类封装一切"（R5）；表级原语作诊断、测试与未来远程/热更表源的扩展点（Scope 预留）。
- **D3 AE5 验证载体：Demo 内最小工程**（原规划分叉点 3，已确认采纳）。复用现有 Demo 程序集布局与 Luban 配置，成本最低且直接服务 R11 静态引用验证；独立工程留作未来验证路径。
- **D4 数据输入：原始行 JSON（JSONNode）而非行实例**。R9 要求高优先级行以"已注册表行类型"重解析，注册必须携带原始 JSON；引导类因此不含加载逻辑（与 `Tables(Func<string, JSONNode>)` 形状对称）。
- **D5 引导类仅声明元数据**：`IDataPackEntry` = 包标识/优先级/表清单（表名/行类型/数据名），数据统一走 `IDataPackDataSource` —— 兑现 R5"注册触发无需手写加载代码"。`IDataPackDataSource : IUtility`（框架理念契合审查修正 Y1：与 GAS `IConfigLoader` 同构——纯 I/O 不持有缓存；经架构 `RegisterUtility`/`GetUtility` 接入能力体系，显式传入优先、缺省获取回退）。
- **D6 主键约定：int 大写 `ID` 单键**。已实测 Demo 全部生成表 `_dataMap.Add(_v.ID, _v)`；固化到 `DataPackConventions` 常量，`ITableReader<TRow>` 键类型定 int。
- **D7 废除语义：约定列 `is_void`（bool）**。由模板默认包含；`ITableReader` 的查询/迭代/计数接口均过滤废除行，覆盖 is_void=false 新行反转废除（AE3）。
- **D8 跨包查询行类型必须等于存储行类型**（最低优先级包的类型）。合并数据只以存储类型实例存在；mod 读本体表 → 静态引用下引用本体程序集用本体类型（R11 路径天然支持）。类型不匹配 → 查询失败 + 警告，不猜测转换。
- **D9 框架不调用 Luban `ResolveRef`**：ref 以原始键存储，查询期经 `ITableView` 在合并视图上解析，目标缺失返回默认值 + 警告（R9）；引用密集型表稀疏覆盖不做特殊优化（Deferred）。
- **D10 合并实现基于约定内省**（反射提取主键/废除列/枚举列/找反序列化器），不依赖编译期泛型约束——静态引用验证与未来运行时程序集加载共享同一合并代码（R11 硬性要求）。
- **D11 注册失败不回滚已成功表**：包级失败 = 存在表级失败（拒绝该表 + 字段差异诊断），已成功表保留——增量友好、可测。
- **D12 程序集形式：独立 asmdef 源码目录**（与框架源码级结构一致），非预编译 dll。

## Open Questions

### Resolved During Planning

- TableModel 泛型查询形状 → D1（表名 + 行类型）
- 包清单格式与字段序列化 → `[Serializable]` 类 + JSON 化（Editor 期维护），运行时由引导类编译期持有同等信息（清单与引导类双载体，运行时路径只依赖引导类）
- 注册契约形状 → D2/D5（`IDataPackRegistry` + `IDataPackEntry`）
- 主键约定实测 → 已完成（int 大写 ID 单键，D6）
- 默认表数据源 → `IDataPackDataSource` + `ResourcesDataPackDataSource`（`Resources/DataPacks/{PackId}/{dataName}.json`）
- 一键复制工具入口 → Editor 菜单（`NF.DataPack/Initialize Data Pack…`）为主，工具类保留命令行可调用的静态入口（便于 gen 脚本后处理调用）
- 测试工程基础设施 → 新建 `Tests.DataPack` asmdef（仅引 NexusFramework + NexusFramework.Config，零 GAS，AE4 硬性约束）

### Deferred to Implementation

- `ITableView` 跨表引用解析的辅助代码形状（生成代码 vs 纯查询 API）：依赖实测生成的 ref 字段形状后定稿，接口层先提供 `ITableView`
- `Resources/DataPacks/{PackId}/` 最终目录约定（Resources 根 vs StreamingAssets）：打包需求（build 含资源）实测后定稿，接口不变
- 引导类发现契约的最终命名（动态加载路径反射调用用）：本次仅稳定 `IDataPackEntry` 形状
- 包级注册原子性以外的诊断输出通道（Debug.Log vs 回调）：实现期按框架既有日志习惯定稿

## Output Structure

```
Assets/
├── NexusFramework.Config/
│   ├── NexusFramework.Config.asmdef                    # 引用 NexusFramework + Luban.Runtime
│   ├── Packaging/                                      # R1/R2：包标识、清单、优先级常量
│   │   ├── DataPackId.cs
│   │   ├── DataPackManifest.cs
│   │   └── DataPackPriorities.cs
│   ├── Registration/                                   # R5/R7：注册契约与结果类型
│   │   ├── IDataPackRegistry.cs
│   │   ├── IDataPackEntry.cs
│   │   ├── DataPackRegisterResult.cs
│   │   └── DataPackTableRegistration.cs
│   ├── Storage/                                        # R6/R8：统一存储与查询
│   │   ├── TableModel.cs
│   │   ├── ITableReader.cs
│   │   ├── ITableView.cs
│   │   └── TableContainer.cs                           # internal：表容器与存储实现
│   ├── Merging/                                        # R9/R11：约定常量、内省、合并
│   │   ├── DataPackConventions.cs
│   │   ├── ITableIntrospector.cs
│   │   ├── ITableSchemaValidator.cs
│   │   └── ITableMerger.cs
│   ├── Sources/                                        # R6：表数据源
│   │   ├── IDataPackDataSource.cs
│   │   └── ResourcesDataPackDataSource.cs
│   └── Editor/
│       ├── NexusFramework.Config.Editor.asmdef        # 引 Config + UnityEditor
│       ├── DataPackInitializer.cs                      # R10：一键复制（模板资产）
│       ├── DataPackCodePostProcessor.cs                # R5：引导类/启动接入代码生成器
│       └── DataPackSchemaDiffTool.cs                   # R5：两包 schema 对比诊断
├── Demo/DataPack/                                      # 本体数据包（Unit 6）
│   ├── Table/luban.conf、gen.bat                        # 模板落地（骨架）
│   ├── Datas/*.xlsx                                     # 业务表（含 is_void 列）
│   ├── TableClass/…（生成强类型） + DataGenerated/…（生成 JSON）
│   └── Generated/DataPackEntry.cs、DataPackBootstrapper.cs  # 后处理生成
├── Demo/ModDataPack/                                   # mod 数据包（Unit 7，静态引用模拟）
│   └── …（同构：覆盖表结构 xlsx + 生成代码 + 引导类）
└── Tests/DataPackTests/
    ├── Tests.DataPack.asmdef                           # 仅 NexusFramework + NexusFramework.Config
    ├── Runtime/TableModelTests.cs、TableMergerTests.cs、TableIntrospectorTests.cs、
    │         DataPackIdTests.cs、DataSourceTests.cs
    └── Editor/DataPackInitializerTests.cs、PostProcessorTests.cs
```

## High-Level Technical Design

> *以下为接口契约草案（Unit 1 交付物，接口定义模式产出）。这是方向性指导，供评审确认；实现阶段按确认后的签名落地，不在此扩展实现细节。*

```
// 注册流程（启动时，按优先级排序）：
//   Demo 引导代码 → CoreDataPackBootstrapper.RegisterTo(model) / ModDataPackBootstrapper.RegisterTo(model)
//   → model.RegisterDataPack(entry, dataSource)          // 包级
//   → 框架拆表：表不存在 → 用 entry 行类型建表；已存在 → schema 校验 → JSON 重解析合并
// 查询流程（运行时）：
//   GetModel<TableModel>() → model.GetTable<unit>("tbunit") → ITableReader<unit>
//   → Get(key)/TryGet/All/Pairs（废除行过滤）→ 业务数据（无感知数据来源包，AE2）

// 核心契约（签名见 Unit 1 文件清单，此处给出形状摘要）：
IDataPackRegistry
    DataPackRegisterResult RegisterDataPack(IDataPackEntry entry, IDataPackDataSource dataSource)
    DataPackTableResult    RegisterTable(DataPackTableRegistration registration)
IDataPackEntry { DataPackId PackId; int Priority; IReadOnlyList<DataPackTableDeclaration> Tables }
IDataPackDataSource : IUtility { JSONNode Load(string dataName) } // 对齐 GAS IConfigLoader（框架理念契合修正 Y1）：纯 I/O 不持有缓存；经 RegisterUtility 注册、GetUtility 缺省获取
ITableReader<TRow> { Count; Get(key); GetOrDefault(key); TryGet(key, out); Contains(key);
                     IsVoid(key); All(); Pairs() }              // 主键 int（D6）
ITableView { object GetRow(string tableName, int key) }         // ref 查询期解析（D9）
ITableIntrospector { GetPrimaryKeyField(type); GetVoidField(type); EnumerateColumns(type);
                     FindDeserializer(type); DeserializeRow(type, node);
                     GetPrimaryKeyValue(row); IsVoidRow(row) }  // R11 约定内省（D10）
ITableSchemaValidator { SchemaCompareResult Compare(existing, incoming) }   // 同表判定 + 字段差异诊断（R9）
ITableMerger { MergeRows(target, registeredRowType, rowsJson, tableName, packId, introspector) }
DataPackConventions { ID = "ID"; is_void = "is_void" }         // 常量唯一事实来源（D6/D7）
DataPackRegisterResult / DataPackTableResult / DataPackTableStatus { New, Overlayed,
    SchemaMismatch, DeserializeFailed, VoidSemanticsUnsupported }  // 诊断枚举（R9）
Editor: DataPackInitializer.Initialize(packId, targetDirectory)  // R10
       DataPackCodePostProcessor.Generate(context) → 生成文件清单  // R5 后处理
       DataPackSchemaDiffTool.Compare(baseXlsx, overrideXlsx) → SchemaDiffReport  // R5
```

## Implementation Units

- [ ] **Unit 1: Config 库接口层定义（接口定义模式交付物）**

**Goal:** 创建 `NexusFramework.Config` 程序集与全部接口契约（见 Output Structure 树），仅接口声明 + XML 注释 + 空实现体外的值类型/常量，编译通过；作为评审确认门，确认后进入 Unit 2-8。

**Requirements:** R1, R2, R4, R5, R7, R8, R9（契约部分）, R11（契约部分）

**Dependencies:** 无

**Files:**
- Create: `Assets/NexusFramework.Config/NexusFramework.Config.asmdef`
- Create: `Assets/NexusFramework.Config/Packaging/DataPackId.cs`
- Create: `Assets/NexusFramework.Config/Packaging/DataPackManifest.cs`
- Create: `Assets/NexusFramework.Config/Packaging/DataPackPriorities.cs`
- Create: `Assets/NexusFramework.Config/Registration/IDataPackRegistry.cs`
- Create: `Assets/NexusFramework.Config/Registration/IDataPackEntry.cs`
- Create: `Assets/NexusFramework.Config/Registration/DataPackRegisterResult.cs`
- Create: `Assets/NexusFramework.Config/Registration/DataPackTableRegistration.cs`
- Create: `Assets/NexusFramework.Config/Storage/TableModel.cs`（仅类声明 + 接口实现挂接，无方法体）
- Create: `Assets/NexusFramework.Config/Storage/ITableReader.cs`
- Create: `Assets/NexusFramework.Config/Storage/ITableView.cs`
- Create: `Assets/NexusFramework.Config/Merging/DataPackConventions.cs`
- Create: `Assets/NexusFramework.Config/Merging/ITableIntrospector.cs`
- Create: `Assets/NexusFramework.Config/Merging/ITableSchemaValidator.cs`
- Create: `Assets/NexusFramework.Config/Merging/ITableMerger.cs`
- Create: `Assets/NexusFramework.Config/Sources/IDataPackDataSource.cs`
- Create: `Assets/NexusFramework.Config/Sources/ResourcesDataPackDataSource.cs`（仅签名）
- Test: `Assets/Tests/DataPackTests/Runtime/DataPackIdTests.cs`
- Test: `Assets/Tests/DataPackTests/Tests.DataPack.asmdef`（本单元仅建立测试工程骨架）

**Approach:**
- asmdef 引用：`NexusFramework` + `Luban.Runtime`（D 直接包装 Luban，见 origin: Key Decisions）
- `DataPackId` 为值类型，构造时校验取值约束（小写字母/数字/点/下划线），作为清单与注册参数的强类型载体（R2）
- 接口全部置于 `NexusFramework.Config` 命名空间；注释使用 XML doc 标注每个方法的契约语义（覆盖/废除/诊断规则引用 R9 原文语义）
- `IDataPackDataSource : IUtility`（与 GAS `IConfigLoader` 完全同构：纯 I/O + 解析工具，不持有缓存，缓存由 TableModel 管理）；经架构 `RegisterUtility` 注册（GASArchitecture.OnInit 先例），`TableModel` 注册时经 `GetUtility<IDataPackDataSource>()` 缺省获取
- `TableModel.cs` 本单元仅声明 `class TableModel : AbstractModel, IDataPackRegistry`，方法体留待 Unit 2
- 本单元为确认门：完成即"全部接口想好"，评审/优化确认后才进入实现单元

**Patterns to follow:**
- `Assets/NexusFramework.GAS/Config/IConfigLoader.cs` 的接口风格（`IUtility` 解耦）
- `Assets/NexusFramework/Framework.cs` 的 `AbstractModel` 基类约定

**Test scenarios:**
- Happy path: `DataPackId.FromString("core")` / `"mod.sample"` → 取值成功且 `ToString()` 原样返回
- Edge case: 空串、含大写/空格/非法字符的取值 → 构造失败（抛异常或返回失败，按实现定稿）
- Edge case: 两个不同 `DataPackId` 相等性（`==`/`Equals`/哈希）→ 值语义正确

**Verification:**
- 全部接口文件编译通过（Unity 控制台无错误）
- 接口清单与 Output Structure 树一致，无遗漏
- 评审确认：用户认可接口契约后再进入 Unit 2

- [ ] **Unit 2: TableModel 存储与注册实现**

**Goal:** 实现 `TableModel`（`AbstractModel` + `IDataPackRegistry`）：表容器存储、包级注册拆表、表级原语、查询 API、表名枚举。

**Requirements:** R6, R7, R8, R9（注册/存储部分）

**Dependencies:** Unit 1

**Files:**
- Modify: `Assets/NexusFramework.Config/Storage/TableModel.cs`
- Create: `Assets/NexusFramework.Config/Storage/TableContainer.cs`（internal：字典 + 行实例存储 + 废除位）
- Create: `Assets/NexusFramework.Config/Storage/TableView.cs`（internal 实现类，实现 Unit 1 定义的 `ITableView`）
- Test: `Assets/Tests/DataPackTests/Runtime/TableModelTests.cs`

**Approach:**
- `TableModel` 自身经 `RegisterModel(new TableModel())` 进入架构容器（`GetModel<T>()` 为容器查表、不自动创建——FrameworkArchitecture.cs:292；GAS 先例 GASArchitecture.OnInit 显式注册，框架理念契合修正 Y2）；本单元以测试架构显式注册后验证获取链路
- `RegisterDataPack` 的数据源缺省解析：显式传入的 `IDataPackDataSource` 优先，否则经 `Architecture` 属性 `GetUtility<IDataPackDataSource>()`（对齐 GAS `IConfigLoader` 获取路径）
- `TableContainer` 持有 `Dictionary<int, object>` + `HashSet<int>` 废除键（或行内 is_void 标记，按性能实测定稿）；查询热路径纯字典 + 缓存的委托访问，反射只出现在注册期（D10 约束）
- `RegisterDataPack`：按 `entry.Tables` 逐表转 `RegisterTable`；包级结果聚合表级结果（D11 不回滚）
- 重复注册同一 `PackId` → 拒绝并诊断（`DataPackRegisterResult` 新增 `AlreadyRegistered` 状态，随 Unit 1 接口确认一并定义）
- `GetTable<TRow>` 行类型不匹配（D8）→ 返回 null + 警告；`TryGetTable` 返回 false
- 查询接口统一经内部 `ITableReader<TRow>` 适配器暴露（废除过滤、Pairs 含主键）

**Execution note:** 注册 → 合并 → 查询的链路行为测试先行（AE1/AE2/AE3 语义在 TableModel 层先有失败测试）

**Technical design:** *方向性指导。* 注册流程：`RegisterTable` → 表名不存在 → 建容器（`TableContainer`，存储类型 = 传入 RowType）→ `Overlayed`；表名已存在 → `ITableSchemaValidator` 校验 → 通过 → `ITableMerger.MergeRows`；失败 → `SchemaMismatch`/`VoidSemanticsUnsupported` + `Diagnostic` 差异文本。

**Patterns to follow:**
- `Assets/NexusFramework.GAS/Models/ConfigModel.cs` 的字典缓存模式（替换为通用形状）
- `Assets/Demo/Emberheart/GAS/TableClass/Tbunit.cs` 的 `DataMap` 语义（`Get`/`GetOrDefault` 命名对齐）

**Test scenarios:**
- Happy path: 注册一包两张新表 → 两表均 `New` 且可查询（`Count`/`Get` 正确）
- Happy path: `GetTable<TRow>` 返回行与注册数据一致（AE1：主键 5 覆盖行、3 本体行、15 新增行）
- Integration: `RegisterModel(new TableModel())` 后 `GetModel<TableModel>()` 返回同一实例（框架获取链路，Y2 修正验证）
- Edge case: 注册空表（零行 JSON）→ 成功且 `Count == 0`
- Error path: 查询不存在的表名 → `GetTable` 返回 null，`TryGetTable` false
- Error path: 行类型与存储类型不一致（D8）→ 查询失败 + 警告，不抛异常
- Error path: 重复注册同一包 → 拒绝 + 诊断，已存在表数据不变
- Integration: 两包按优先级注册后，任意查询不经来源包区分（AE2：结果与合并规则一致）

**Verification:**
- TableModelTests 全绿；控制台无警告级噪音（查询失败警告除外）

- [ ] **Unit 3: 约定内省器与合并执行器**

**Goal:** 实现 `ITableIntrospector`（约定反射）、`ITableSchemaValidator`（同表判定 + 字段差异诊断）、`ITableMerger`（行级覆盖/追加/废除/反转，JSON 重解析）。

**Requirements:** R9（全部规则）, R11（合并代码共享）

**Dependencies:** Unit 1

**Files:**
- Create: `Assets/NexusFramework.Config/Merging/TableIntrospector.cs`
- Create: `Assets/NexusFramework.Config/Merging/TableSchemaValidator.cs`
- Create: `Assets/NexusFramework.Config/Merging/TableMerger.cs`
- Test: `Assets/Tests/DataPackTests/Runtime/TableIntrospectorTests.cs`
- Test: `Assets/Tests/DataPackTests/Runtime/TableMergerTests.cs`

**Approach:**
- 内省器按 `DataPackConventions` 反射：主键字段（int ID）、废除字段（bool is_void，可缺失）、public 实例字段枚举（列属性）、静态 `Deserialize{TypeName}(JSONNode)` 定位；反射结果缓存（每类型一次），合并期仅调用
- 反序列化器容错：签名以 `JSONNode` 参数匹配（Luban 生成形状 `DeserializeXxx(JSONNode)`，已实测）
- schema 校验：表名一致 + 列名/列类型/主键字段逐一对比 → `SchemaCompareResult` 携带差异文本（列名/列类型/主键/is_void 四类差异，R9 诊断要求）
- 合并器：高优先级行 JSON 重解析到已注册行类型 → 主键存在则覆盖（含废除行写入）、不存在则追加；废除行查询期由 `ITableReader` 过滤（AE3）；覆盖 is_void=false 新行反转废除
- 反序列化失败（行 JSON 结构不兼容）→ 该表拒绝 + 诊断（`DeserializeFailed`）
- 实现不依赖任何编译期泛型约束：全部经 `Type`/反射操作（D10，静态路径与未来动态路径共享同一实现）

**Execution note:** AE3 废除/反转语义与 R9 诊断场景测试先行

**Technical design:** *方向性指导。* 合并伪流程：`foreach row in rowsJson` → `primaryKey = introspector.GetPrimaryKeyValue(introspector.DeserializeRow(registeredType, row))` → `target.Contain(key)` ? 覆盖 : 追加 → 覆盖时以 registeredType 重建实例（JSON 重解析）写入。

**Patterns to follow:**
- `Assets/Demo/Emberheart/GAS/TableClass/unit.cs` 的生成形状（readonly 字段 + 静态 Deserialize）——反射目标形状
- `Assets/Demo/DataGenerated/Luban/Json/GAS/tbunit.json` 的 JSON 形状（含 ID 的对象数组）

**Test scenarios:**
- Happy path: 覆盖合并（AE1）——mod 注册主键 5..15 → 5/15 为 mod 行、3 为本体行、10 之前本体行为 mod 覆盖后可见数据正确
- Happy path: 追加行（主键 15 新增）→ `Contains(15)` true 且值为新增行
- Happy path: 废除（AE3）——本体主键 1 有效 + mod 覆盖 is_void=true → `Get(1)` 返回 default、`All()` 不含、`IsVoid(1)` true
- Happy path: 反转废除（AE3）——再注册 is_void=false 主键 1 新行 → `Get(1)` 恢复返回
- Edge case: 同优先级双包平局 → 最后注册者胜（稳定裁决）
- Error path: 表名相同但列不一致（mod 改列/删列/改主键）→ `SchemaMismatch` + 诊断文本含差异字段名与类型
- Error path: 已注册表无 is_void 列而覆盖方含废除行 → `VoidSemanticsUnsupported` + 诊断
- Error path: 高优先级行 JSON 含非法行（缺 ID/类型不符）→ `DeserializeFailed` + 表名诊断
- Integration: 内省器对 `cfg.unit` 与 `cfg.exgas.Tbability` 形状均正确提取主键/废除列/反序列化器（R11 共享合并代码的静态引用验证）

**Verification:**
- TableIntrospectorTests / TableMergerTests 全绿；诊断文本可读且字段级精确

- [ ] **Unit 4: 表数据源**

**Goal:** 实现 `IDataPackDataSource` 默认实现（Resources 目录约定）与启动接入适配。

**Requirements:** R6（默认 loader，兑现"全程无手写加载代码"）

**Dependencies:** Unit 1

**Files:**
- Modify: `Assets/NexusFramework.Config/Sources/ResourcesDataPackDataSource.cs`
- Test: `Assets/Tests/DataPackTests/Runtime/DataSourceTests.cs`

**Approach:**
- `ResourcesDataPackDataSource(DataPackId packId) : IUtility`：`Resources.Load<TextAsset>("DataPacks/{PackId}/{dataName}")` → JSONNode；缺失返回 null（表级诊断由注册流程给出）；`Initialized` 生命周期标志实现对齐 `JsonConfigLoader` 形状
- 提供 `FromFunc(Func<string, JSONNode>)` 适配器（对接 Luban `Tables` 构造形状与测试注入）

**Test scenarios:**
- Happy path: 测试资源目录放置 JSON → `Load("tbunit")` 返回正确 JSONNode
- Edge case: 不存在的 dataName → 返回 null（不抛异常）
- Integration: `FromFunc` 适配器与 `RegisterTable` 衔接（数据源 → 注册 → 查询闭环）

**Verification:**
- DataSourceTests 全绿；测试资源约定目录在测试工程内建立

- [ ] **Unit 5: Editor 工具（一键复制 + gen 后处理 + schema 对比）**

**Goal:** 实现模板一键复制（R10）、引导类/启动接入代码生成器（R5）、两包 schema 对比工具（R5）。

**Requirements:** R5, R10

**Dependencies:** Unit 1

**Files:**
- Create: `Assets/NexusFramework.Config/Editor/NexusFramework.Config.Editor.asmdef`
- Create: `Assets/NexusFramework.Config/Editor/DataPackInitializer.cs`
- Create: `Assets/NexusFramework.Config/Editor/DataPackCodePostProcessor.cs`
- Create: `Assets/NexusFramework.Config/Editor/DataPackSchemaDiffTool.cs`
- Create: `Assets/NexusFramework.Config/Editor/Templates/luban.conf.template`、`gen.bat.template`、`Datas/骨架表.xlsx`（或生成脚本，按实现可行性定稿）
- Test: `Assets/Tests/DataPackTests/Editor/DataPackInitializerTests.cs`
- Test: `Assets/Tests/DataPackTests/Editor/PostProcessorTests.cs`

**Approach:**
- 一键复制：菜单 `NF.DataPack/Initialize Data Pack…`（对话框输入包标识与目标目录）；模板复制仅创建缺失文件（不覆盖已有）；模板 gen 脚本默认零配置（路径按包目录自动推导），配置仅作覆盖项（R5/R10）
- gen 后处理：扫描生成的 JSON 数据清单 + 表类程序集（反射 `cfg` 命名空间表类）→ 生成 `{Pack}DataPackEntry.cs`（实现 `IDataPackEntry`，表名/行类型/数据名编译期写死）与 `{Pack}DataPackBootstrapper.cs`（`RegisterTo(TableModel)`，内部创建默认 `ResourcesDataPackDataSource` 并调用 `RegisterDataPack`）；生成器产出文件清单供展示与测试断言
- 后处理调用点（时序约束）：产物中的行类型引用（`typeof`/反射）必须发生在生成代码被 Unity 编译之后——gen 脚本仅负责产出表代码与 JSON（可附带生成纯文本中间清单，无需编译）；entry/bootstrapper 的生成在 Unity 内触发（Editor 菜单手动触发为主，辅以编译后延迟回调）。禁止在 gen.bat 尾部直接批处理生成含类型引用的产物
- schema 对比：解析两包 xlsx 表结构（表名列头）→ `SchemaDiffReport`（新增/缺失/类型变更列 + 主键变更标记）；作为独立工具，不进入运行时

**Patterns to follow:**
- `Assets/NexusFramework.GAS/Editor/CodeGen/LubanConfigLoaderGenerator.cs`（MenuItem + IndentedWriter 生成器先例）
- `Assets/Demo/Table/luban.conf` + `gen.bat`（模板蓝本）

**Test scenarios:**
- Happy path: 初始化空目录 → 模板文件全部生成、已有文件不被覆盖
- Edge case: 目标目录已存在同文件 → 保留原内容（不覆盖）
- Happy path: 后处理输入模拟 JSON 清单 + 表类型 → 生成 entry 与 bootstrapper，且产物可编译、内容与输入一致（表名/类型/数据名）
- Error path: 后处理输入空目录 → 返回空清单（不抛异常）
- Integration: schema 对比两包 xlsx（一致 / 新增列 / 删列 / 类型变更 / 主键变更五组样例）→ 报告字段正确
- Integration: 生成的 Bootstrapper 端到端 → 注册后查询成功（与 Unit 6 联动验证，见 Unit 8 集成测试）

**Verification:**
- 编辑器测试全绿；手动执行初始化 + 后处理产物编译通过

- [ ] **Unit 6: Demo 本体数据包落地**

**Goal:** 在 Demo 建立符合规范的本体数据包（模板 → 表结构 → gen → 生成代码 → 引导类 → 启动接入），兑现 AE5 端到端（无手写加载代码）。

**Requirements:** R1, R3, R5, R6, R8（Demo 演示）, AE5

**Dependencies:** Unit 4, Unit 5

**Files:**
- Create: `Assets/Demo/DataPack/Table/luban.conf`、`Assets/Demo/DataPack/Table/gen.bat`（模板产物，包级命名空间如 `dpack.core`）
- Create: `Assets/Demo/DataPack/Datas/`（业务表骨架 xlsx，含 `is_void` 列约定；至少一张可复用现有 GAS 表结构语义的业务表）
- Create（生成产物）: `Assets/Demo/DataPack/TableClass/…`、`Assets/Demo/DataPack/DataGenerated/…`（JSON）
- Create: `Assets/Demo/DataPack/Generated/DataPackEntry.cs`、`DataPackBootstrapper.cs`（Unit 5 后处理生成）
- Create: `Assets/Demo/DataPack/Demo.DataPack.asmdef`（独立程序集，仅引 NexusFramework + NexusFramework.Config + Luban.Runtime，不引 GAS/ECS——这是 Unit 7 ModDataPack 静态引用的依赖基础，也是保持测试链零 GAS 的关键）
- Modify: `Assets/Demo/Architecture/DemoGameArchitecture.cs`（启动引导，对齐 GASArchitecture.OnInit 模式）：`RegisterModel(new TableModel())` → `RegisterUtility(new ResourcesDataPackDataSource(…))` → `CoreDataPackBootstrapper.RegisterTo(model)` 三步（Y2 修正）

**Approach:**
- 包命名空间 `dpack.core`（R4 包级唯一命名空间，区别于 GAS 遗留 `cfg`）
- 注册触发仅一行（bootstrapper 调用），业务查询经 `GetModel<TableModel>().GetTable<T>(表名)`
- 表结构骨架来自 Unit 5 模板；数据表内容最小化（覆盖 AE1/AE3 语义所需的主键 1..10 等演示数据）
- 若需 Luban CLI（`Tools/Luban/Luban.dll` 缺失）→ 以人工导入生成的 JSON/代码文件替代驱动验证，AE5 完整 CLI 路径列为前置小任务（见 Deferred to Separate Tasks）

**Test scenarios:**
- Happy path: 启动注册本体包 → `GetModel<TableModel>()` 查询业务表返回本体数据
- Integration: bootstrapper 生成的 `ResourcesDataPackDataSource` 指向的 JSON 存在且可解析（无手写加载代码的证据）

**Verification:**
- Demo 进入 Play Mode 后查询正确；启动日志无注册失败诊断；全程无手写加载代码（代码评审确认）

- [ ] **Unit 7: Demo mod 数据包（静态引用模拟）**

**Goal:** 建立 mod 数据包（同构 Luban 生成，复制本体表结构 + 覆盖/新增行），Demo 以静态引用注册，验证覆盖/废除/诊断全语义（R11 静态引用验证路径）。

**Requirements:** R4, R9（覆盖/废除演示）, R11

**Dependencies:** Unit 3, Unit 6

**Files:**
- Create: `Assets/Demo/ModDataPack/Table/luban.conf`（命名空间 `dpack.mod.sample`）、`Datas/`（覆盖表结构 xlsx：复制本体表结构，仅数据差异）
- Create（生成产物）: `Assets/Demo/ModDataPack/TableClass/…`、`Assets/Demo/ModDataPack/DataGenerated/…`
- Create: `Assets/Demo/ModDataPack/Generated/DataPackEntry.cs`、`DataPackBootstrapper.cs`
- Create: `Assets/Demo/ModDataPack/ModDataPack.asmdef`（引 NexusFramework + NexusFramework.Config + Luban.Runtime + Demo.DataPack——静态引用下 mod 代码可用本体行类型查询合并结果，D8 支持）
- Modify: Demo 启动引导：本体注册后按优先级注册 mod 包（`ModDataPackBootstrapper.RegisterTo(model)`）

**Approach:**
- mod 表结构 = 复制本体表结构 xlsx（R5 流程）；数据演示 AE1（主键 5..15 覆盖/新增）与 AE3（主键 1 废除）
- 反例演示（诊断路径）：额外注册一个故意改列的包 → 验证 `SchemaMismatch` 诊断输出（演示而非测试）
- 优先级：本体 `Base`、mod `Mod`（D6 常量）

**Test scenarios:**
- Happy path: 本体 + mod 注册后（AE1）主键 5 → mod 行、3 → 本体行、15 → 新增行
- Happy path: 本体主键 1 被 mod 废除后 `Get(1)` 返回默认值；再注册 is_void=false 行后恢复（AE3 反转）
- Integration: mod 代码经 `GetModel<TableModel>().GetTable<本体行类型>(表名)` 查询合并结果（跨程序集查询，D8 路径验证）

**Verification:**
- Demo Play Mode 中本体代码与 mod 演示查询均输出正确；诊断反例输出符合预期

- [ ] **Unit 8: 测试工程全链路（AE4 零 GAS）**

**Goal:** 建立 `Tests.DataPack` 测试程序集（仅引 NexusFramework + NexusFramework.Config，零 GAS），覆盖注册 → 行级合并 → 框架查询全链路，作为 AE4 验收与回归基线。

**Requirements:** R6, R7, R8, AE4, AE1-AE3（作为测试语义）

**Dependencies:** Unit 1, Unit 2, Unit 3, Unit 4

**Files:**
- Modify: `Assets/Tests/DataPackTests/Tests.DataPack.asmdef`（补齐引用；`UNITY_INCLUDE_TESTS` 条件与既有 Tests asmdef 一致）
- Create: `Assets/Tests/DataPackTests/Runtime/TableModelTests.cs`（Unit 2 已建）、`TableMergerTests.cs`、`TableIntrospectorTests.cs`、`DataPackIdTests.cs`、`DataSourceTests.cs`（随各单元建立）
- Create: `Assets/Tests/DataPackTests/Runtime/DataPackIntegrationTests.cs`（全链路：注册 → 合并 → 查询 + AE1/AE2/AE3 断言）
- Create: `Assets/Tests/DataPackTests/Runtime/FakeRows.cs`（测试行类型：按 Luban 生成形状手写的模拟类——public readonly 字段 + 静态 `DeserializeXxx(JSONNode)` + 首字段 int `ID`，含/不含 `is_void` 两种变体）
- Create: `Assets/Tests/DataPackTests/Resources/DataPacks/test/…`（测试 JSON 数据）

**Approach:**
- 测试数据以 `IDataPackDataSource.FromFunc` 注入内存 JSON（不依赖 Resources 路径，保证纯逻辑可测）；DataSourceTests 单独覆盖 Resources 路径
- 集成测试使用 `FakeRows.cs` 自建行类型（非任何 Demo 程序集类型）——严格满足 AE4 零 GAS 引用，且不依赖 Demo 资产即可独立运行
- 集成测试断言与 AE1/AE2/AE3 语义一一对应；AE4 约束以 asmdef 引用清单 + 编译检查验证（测试代码中出现 GAS 类型即失败）
- 与 `Assets/Tests/Tests.asmdef`（引 Demo/GAS）完全隔离，互不引用

**Test scenarios:**
- Integration: 本体（1..10）+ mod（5..15，含 5 覆盖）注册 → 5 为 mod 行、3 为本体行、15 新增（AE1）
- Integration: 双包注册后经 `GetModel<TableModel>()` 查询，不感知来源包（AE2）
- Integration: 废除 + 反转全流程（AE3）
- Integration: 反例包（改列）注册 → 拒绝 + 诊断文本断言（R9）
- Edge case: 空包注册（无表）→ 成功空结果；空表注册 → Count 0

**Verification:**
- 全量测试通过（`UNITY_INCLUDE_TESTS` 下 EditMode/RunMode 均可运行）；测试工程 asmdef 零 GAS 引用（AE4 验收）

## System-Wide Impact

- **Interaction graph:** `TableModel` 经 `RegisterModel` 注册进架构（`IArchitecture` 注册表，`GetModel<T>()` 为容器查表不自动创建——GASArchitecture.OnInit 先例）；`IDataPackDataSource` 以 `IUtility` 经 `RegisterUtility` 注册（GAS `IConfigLoader` 同构）；Demo 启动引导（`DemoGameArchitecture.cs` 或等价入口）新增三步引导调用；现有 `DataCarrier`/`DataTrait` 实例数据载体不受影响（表→Model、实例→Carrier 边界，见 origin: Key Decisions）
- **Error propagation:** 注册失败不抛异常，经 `DataPackRegisterResult`/`DataPackTableResult` + `Diagnostic` 返回（表名与字段差异）；查询失败（表不存在/类型不匹配/行废除）返回 null/default + 警告日志；不向调用方抛运行时异常
- **State lifecycle risks:** 注册幂等性（同包重复注册拒绝）；包级失败不回滚已成功表（D11，增量语义）；废除行存在于容器内但查询/迭代过滤——容器 `Count` 语义（总行数 vs 有效行数）在接口注释中已固化
- **API surface parity:** GAS 现有 `IConfigLoader`/`ConfigModel`/`LubanConfigLoader` 与 `NexusFramework` Core 既有 API 全部保持不变（本子系统新增库，不修改既有面）
- **Integration coverage:** Demo Play Mode 端到端（Unit 6/7）、零 GAS 测试链路（Unit 8）、生成器产物 → 注册 → 查询（Unit 5 与 Unit 6 联动）——三者互为独立验证
- **Unchanged invariants:** `NexusFramework.asmdef` 零引用保持；`NexusFramework.GAS` asmdef 的 GUID 引用与零 Luban 引用保持；Demo 现有 GAS 链路不动（GAS 重构为独立任务）；Luban 生成的既有表类不被修改（新包用新命名空间，避免与 `cfg` 冲突）

## Risks & Dependencies

| Risk | Mitigation |
|------|------------|
| 反射性能进查询热路径 | 反射仅注册期与内省缓存（每类型一次），查询走字典 + 缓存委托（Unit 2/3 约束） |
| Luban 升级改变生成形状（反序列化器签名/主键形状） | 形状依赖集中在 `DataPackConventions` + 内省器单点；manifest 钉版本（见 origin: Dependencies）；升级时重跑验证集 |
| JSON 重解析与 Luban 生成反序列化器存在结构差异 | 内省器按签名容错（JSONNode 参数匹配），`DeserializeFailed` 拒绝而非崩溃；测试覆盖非法行 |
| `is_void` 缺失的已发布旧包覆盖删除语义 | `VoidSemanticsUnsupported` 注册期拒绝 + 诊断（R9） |
| 模板一键复制覆盖用户已有包目录 | 仅创建缺失文件（Unit 5 约束），测试断言覆盖 |
| `Tools/Luban/Luban.dll` 缺失阻断 AE5 完整 CLI 路径 | AE5 以手动导入生成产物先行验证；CLI 下载为前置小任务（Deferred to Separate Tasks） |
| 生成器产物（entry/bootstrapper）与手写代码漂移 | 产物一律生成且入库（源码级结构无分发污染）；生成器测试断言产物内容 |
| `NexusFramework.Config` 依赖 `Luban.Runtime` 后 Luban 包不可用 | 依赖已核实存在（manifest.json）；Luban 属框架级依赖，同 GAS 先例 |

## Documentation / Operational Notes

- 交付物含 `docs/brainstorms/2026-08-01-data-pack-requirements.md` 承诺的数据包规范文档（R5 流程规范：gen 标准化、程序集生成与依赖流程、schema 校验用法、引导类契约）——与 Unit 5 工具同步编写
- 承诺记录：对 Luban 生成形状的依赖清单（表类 API、主键约定、命名空间配置）随规范文档维护（origin: Key Decisions）

## Sources & References

- **Origin document:** [docs/brainstorms/2026-08-01-data-pack-requirements.md](../brainstorms/2026-08-01-data-pack-requirements.md)
- Related code: `Assets/NexusFramework/Framework.cs`、`Assets/NexusFramework.GAS/Config/IConfigLoader.cs`、`Assets/NexusFramework.GAS/Models/ConfigModel.cs`、`Assets/NexusFramework.GAS/Editor/CodeGen/LubanConfigLoaderGenerator.cs`
- Related code（生成形状）: `Assets/Demo/Emberheart/GAS/TableClass/`、`Assets/Demo/DataGenerated/Luban/Json/GAS/tbunit.json`
- Related config: `Assets/Demo/Table/luban.conf`、`Assets/Demo/Table/gen.bat`、`Packages/manifest.json`
- Related tests: `Assets/Tests/Editor/LubanConfigLoaderGeneratorTests.cs`
