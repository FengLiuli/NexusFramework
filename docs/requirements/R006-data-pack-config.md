---
layer: requirements
status: draft
topic: data-pack-config
supersedes:
  - docs/brainstorms/2026-08-01-data-pack-requirements.md
  - docs/requirements/R006-data-pack-config.md（2026-11-17 首版）
origin:
  - docs/brainstorms/2026-08-01-data-pack-requirements.md
  - docs/brainstorms/2026-08-01-content-pack-quadruple-kickoff.md
  - docs/brainstorms/2026-11-17-config-item-boundary-analysis.md
  - docs/brainstorms/2026-11-17-luban-multi-source-capability.md
  - docs/design/D006-data-pack-interface-design.md
  - docs/progress/2026-11-17-data-pack-plan-review.md
  - docs/progress/2026-11-17-d006-d007-design-review.md
created: 2026-11-17
updated: 2026-11-17
---

# 需求：NexusFramework.Config 表能力库

## 0. 定位与文档关系

`NexusFramework.Config` 是**能力库**，不是编排层。它只回答三个问题：

1. 一张表的行数据怎么进来（注册）；
2. 多来源的同表行怎么合并（覆盖 / 追加 / 废除）；
3. 合并后的表怎么查（类型化只读视图 + 跨表引用）。

它**不负责**：内容包的发现与生命周期、契约版本、资源与本地化、程序集加载、服务覆盖。这些属于内容包模型与编排层（`NexusFramework.ContentPacks`，见 [内容包模型 C9/C10](../brainstorms/2026-08-01-content-pack-quadruple-kickoff.md)）。

分层职责：

```text
NexusFramework.ContentPacks（编排，可选）
        ↓ 调用
NexusFramework.Config（本库：表存储 · 合并 · 查询 · 加载抽象）
        ↓ 依赖
NexusFramework（Core）+ Luban.Runtime
```

**单包模式**是分层正确性的检验标准：不引入 `ContentPacks` 时，Config 仍能完成"注册 → 合并 → 查询"的全部功能（RP38）。

> **本版为重写。** 需求条目编号保持不变（RP1–RP38），以便既有文档引用继续有效；内容按新分层校正：移除了契约包与交付形态（归内容包模型），补充了单包模式、行类型来源与 API 契约约束。

---

## 一、背景

框架 Core 已具备 Carrier / Trait / Model / 事件 / IOC 等原语，但**配置表能力只存在于 GAS 私有链路**：`IConfigLoader` + `ConfigModel`（五个 GAS 专属字典）+ Demo 层生成的 Luban 桥接代码。后果有两个：

1. **非 GAS 模块拿不到配置表**：Tag、物品、任务等模块若要读表，只能复制 GAS 的私有链路或绕道 Demo；
2. **配置数据没有进入框架模式**：`ConfigModel` 是 GAS 专属形状，无法以统一方式获取，也没有多源合并能力。

多源（本体 / DLC / mod）带来的第一性问题不是"怎么合"，而是**行的身份**：数字主键只在单一数据源内唯一，两个包各自使用 `1001` 时，合并层无法区分"新增"还是"覆盖"。CarrierId 的命名空间化重构与 Minecraft 1.13 扁平化都是同一判断的先例。本库的行键因此定为**字符串全局键**。

---

## 二、目标

1. **可独立使用**：业务层只依赖 Core + Config，即可获得"注册 → 合并 → 查询"的完整表能力，零 GAS 引用。
2. **多源可合并**：多个来源可按声明优先级注册，按全局行键覆盖、追加、废除，本体代码无需改动。
3. **数据格式规范化**：表结构与生成产物的约定由本库定义，Luban 工具链产物可直接接入。
4. **失败可诊断**：注册与查询失败不抛异常，返回结构化状态与字段级诊断。

---

## 三、术语

| 术语 | 定义 |
|---|---|
| **数据包**（DataPack） | 本库的**注册单位**：一个来源标识 + 优先级 + 一组表声明。它是内容包"数据子包"的运行时投影，二者一一对应（一个内容包至多一个数据包） |
| **来源标识**（`packId`） | 命名空间式字符串（`core`、`com.acme.emberheart`），声明该数据包的归属 |
| **行键**（Row Key） | 行的全局唯一标识，格式 `<packId>:<localKey>`；合并、引用、废除都以它为准 |
| **行类型** | 表行在 C# 中的类型（Luban 生成的 `class`，字段只读） |
| **存储类型** | 某张表在 `TableModel` 中的实际行类型 = 最低优先级来源提供的行类型 |
| **覆盖** | 高优先级来源以**相同行键**写入完整新行，整行替换 |
| **废除行** | `is_void = true` 的行；写入后该行键在查询与迭代中不可见，可被更高优先级来源反转 |
| **约定内省** | 按固定字段名/方法名反射提取主键、废除列、反序列化器，不依赖编译期泛型约束 |

---

## 四、需求

### A. 模块定位与边界

- **RP1.** `NexusFramework.Config` 程序集只依赖 Core（`NexusFramework`）与 `Luban.Runtime`；**不得**引用 GAS、Demo、Inventory 或任何具体表类型所在程序集。
- **RP2.** Config **不认识"内容包"**：它只接受"来源标识 + 优先级 + 表声明"，不知道内容包的发现、启用、卸载与四子包结构。内容包到数据包的映射由编排层完成。
- **RP3.** Config 不解析文本 key 与资源 key：表中字符串列原样存取；本地化解析与资源加载归 `NexusFramework.Localization` 与 `NexusFramework.Assets`。
- **RP4.** Config 不承担：内容包发现与生命周期、契约版本校验、程序集加载、服务注册与覆盖。这些归编排层与 Core 的 IOC。
- **RP5.** **单包模式必须可用**：不引入编排层时，业务层直接构造 `IDataPackEntry` 并调用 `RegisterDataPack`，即可完成注册、合并与查询；本库不得假定编排层存在。

### B. 表结构与约定

- **RP6.** 数据包由四部分组成：表结构定义、行类型所在程序集（可选）、Luban 导出的行数据（JSON）、表声明。**行类型程序集是可选的**：纯数据来源不携带行类型，只能扩展已注册表（RP23）。
- **RP7.** 表声明至少含：表名、行类型（`Type`）、数据名。声明可由生成器产出（可编译来源），也可由编排层按清单构造（纯数据来源）。
- **RP8.** `packId` 是命名空间式标识，不是显示名：
  - `core` 为游戏本体的保留标识，第三方不得使用；
  - 第三方至少两级、以点分隔（`com.acme.emberheart`），推荐反向域名；
  - 字符集为小写字母、数字、点、下划线、连字符，长度 1..64；
  - 同一 `packId` 第二次注册整包拒绝。
- **RP9.** 表结构基于 Luban 工具链（xlsx + `luban.conf` + gen 脚本）。**约定只有四个来源**，由单一常量类持有，实现与文档不得另立第二套：
  1. 主键字段名 `ID`（大写、`string`、单字段、全局唯一行键）；
  2. 废除列 `is_void`（`bool`）；
  3. 行反序列化器 `Deserialize{TypeName}(JSONNode)`（Luban 生成）；
  4. 表名与数据名：全小写、不含命名空间前缀（`tbitem` 而非 `dpack.core.Tbitem`），**数据名 = 表名**。

  不满足约定的表在注册期被拒绝并给出诊断，**不做静默兼容**（int 主键的既有 GAS 表不纳入本规范，见 §六）。
- **RP9a.** **行键规则**：
  - 格式为 `<packId>:<localKey>`——**冒号是唯一分隔符**（命名空间式键，形式取自 Minecraft 1.13 扁平化的 `namespace:path`）：`packId` 可含点，但不得含冒号；`localKey` 由包作者自定，同样不得含冒号；从第一个冒号处切分即得命名空间与路径，切分唯一；
  - 命名空间段即**归属声明**：等于本包 `packId` 的行是**新增行**；指向另一个**已注册来源**的行是**覆盖行**（键相等即覆盖）；
  - 命名空间不是任何已注册来源时拒绝该行并诊断（防止拼写错误静默产生"新命名空间"）；
  - 包内重复行键拒绝注册；跨包同键即覆盖意图，不构成错误；
  - 行键全小写；比较为**精确字符串匹配**，不做归一化；
  - 行键一旦发布即视为稳定的外部契约（存档、事件、跨表引用都会引用它），改键等同删行 + 新增行。
- **RP10.** 参与合并的每个数据包的表必须含 `is_void` 列。已发布且不含该列的表，不支持覆盖方的删除语义。
- **RP11.** 生成器至少产出两类代码，均由 gen 模板后处理生成、业务不手写：
  - `IDataPackEntry` 实现（来源标识、优先级、表声明列表）；
  - 启动注册引导类（静态入口，接收 `TableModel` 并完成本包注册）。

  纯数据来源没有生成代码，其 `IDataPackEntry` 由编排层构造（RP7）。
- **RP12.** 提供骨架模板资产（`luban.conf`、表结构骨架 xlsx、gen 脚本）与一键复制工具；模板零配置开箱即用，配置仅作覆盖项。
- **RP13.** 提供 schema 差异校验脚本：输出两包 schema 的**列差异**与**行键差异**（"消失的键 / 新增的键"）；列差异即拒绝依据，键差异作为"误改键"的告警。相关 Luban 约束（实测）：分表数据文件**不得使用 `#` 前缀**（否则注册成独立表）；数据文件必须位于主工程 `dataDir` 内；`input` 不支持通配符与 `@sheet`。

### C. 运行时存储与查询

- **RP14.** `TableModel : AbstractModel` 是所有表的统一运行时存储，经 `GetModel<TableModel>()` 获取，与框架既有 Model 链路一致。
- **RP15.** 查询形状为 `GetTable<TRow>(string tableName)`：行类型经 `Type` 传入，框架零表类型知识；行类型必须**等于**表存储类型（最低优先级来源的行类型），不等时返回 `null` + 警告，**不做转换**。
- **RP16.** `ITableReader<TRow>` 至少提供：`TableName`、`Count`（有效行数）、`TotalCount`（含废除行）、`Contains`、`IsVoid`、`TryGet`、`Get`、`All`、`Pairs`；**查询键类型为 `string`（行键）**。
  - `Count` / `All` / `Pairs` / `Contains` **过滤废除行**；
  - `Get(key)` 在"不存在"与"已废除"两种情况下都返回 `default`，并在命名上与 Luban 的 `Tb*.Get`（缺失抛异常）保持区分。
- **RP17.** `ITableView` 提供合并后视图的跨表引用解析：按 `(tableName, string rowKey)` 取行，缺失返回 `null`；降级策略由消费方决定，但框架**必须记录一次可定位的警告**（表名 + 键 + 引用列），不得静默为 `null`。
- **RP18.** 大表枚举不得每次调用产生不可控分配：`All` / `Pairs` 提供缓存只读视图，或提供 `CopyRowsTo(List<TRow>)` 形态；具体形态在实现阶段定稿。
- **RP19.** `TableModel` **只由业务层或编排层注册**在自身架构上；GAS asmdef 对 `NexusFramework.Config` 保持零引用。

### D. 注册与合并

- **RP20.** 注册入口为包级 `RegisterDataPack(IDataPackEntry entry, IDataPackDataSource source = null)`，并提供表级原语 `RegisterTable(DataPackTableRegistration)`（供诊断、测试与新数据源使用）。
  - 注册与查询**不抛异常**，失败经结果类型 + 诊断文本返回；
  - 构造期参数校验（如非法 packId）可抛 `ArgumentException`，属唯一例外。
- **RP21.** 同一 `packId` 重复注册 → 整包拒绝（`AlreadyRegistered`）。包内逐表失败**不回滚**已成功的表，结果按表逐项报告状态。
- **RP22.** 优先级只影响合并顺序：**调用方必须按 priority 升序注册**；同优先级按注册顺序，后注册者胜。实现侧以顺序校验兜底——检测到乱序注册时拒绝并给出"实际顺序 vs 应然顺序"诊断。
- **RP23.** 同表判定与行类型来源：
  - 同表判定：表名一致**且**列集合一致（列名、列类型、主键字段与其类型、废除列均不可变）；列集合不同 → 拒绝并入并输出字段级差异；覆盖表不得引入新列或新的多态类型；
  - **行类型来源**：来源自带行类型时，该表按该类型新建（可全新表名）；来源不带行类型时，只能指向已注册表，并复用其存储类型。指向未注册表 → 拒绝并诊断。
- **RP24.** 行级合并是**整行替换**：合并键为该行的 `ID` 字符串，键相等即同一行；覆盖行必须是**完整行**（Luban 反序列化器对每列做类型断言，缺列即失败）；高优先级行经 JSON 重解析（复用存储类型的反序列化器）写入；实现不得反射写入只读字段。
- **RP25.** 废除语义：`is_void = true` 的行写入后，该行键的查询与迭代均不可见；更高优先级来源提供同键的 `is_void = false` 完整行即可反转。
- **RP26.** 包内重复行键必须拒绝注册（独立状态 `DuplicateKey`，诊断给出重复键值），不得归入"反序列化失败"。
- **RP27.** 注册期以覆盖来源的行 JSON 试反序列化到存储类型，校验结构兼容；失败即拒绝该表并输出表名与字段差异。

### E. 加载抽象

- **RP28.** **加载能力属于本库，加载选择属于数据来源**：Config 提供 `IDataPackDataSource`（纯 I/O，不持有缓存）与一组内置实现；每个数据包在注册时提供自己的 source 或选择内置实现，**核心不规定**数据放在哪里。

  ```csharp
  JSONNode Load(DataPackId packId, string dataName);
  ```

  `packId` 参数保留以支持多个来源共享同一个源；`IDataPackDataSource` **不要求是全局 IOC 单例**——注册时显式传入，未指定时才回落到可选的默认 source。
- **RP29.** 内置实现至少覆盖：`ResourcesDataPackSource`（`Resources/DataPacks/{packId}/{dataName}.json`，参考实现）、`FileDataPackSource`、`AddressablesDataPackSource`、`MemoryDataPackSource`（测试与嵌入数据）。**Resources 目录只是其中一种实现，不是规范**。
- **RP30.** 数据源是扩展点：远程表源、AssetBundle 表源、热更表源由来源自行实现 `IDataPackDataSource` 并注册，或由框架后续补充内置实现；**不改** `TableModel` 与合并代码。

### F. 质量与验证

- **RP31.** 存在仅引用 Core + Config 的测试程序集（零 GAS 引用），覆盖：注册 → 合并（覆盖/追加/废除/反转）→ 查询 → 诊断路径。
- **RP32.** 以静态注册模拟三来源共存（`core` / `dlc.*` / `mod.*`），验证注册、合并与查询；合并实现基于约定内省，**可编译来源与纯数据来源共享同一合并代码**——差异只在"行类型从哪来"。
- **RP33.** 注册期不做整表全量校验（只做结构校验与逐行反序列化）；失败表不影响已注册表的数据完整性。
- **RP34.** **单包模式测试**：不引入编排层，直接构造 `IDataPackEntry` 并注册，完成"注册 → 查询 → 废除"全链路；该测试是 RP5 的验收形式。
- **RP35.** Config 子系统**不构造 Luban 的 `cfg.Tables`**：表容器由 `TableModel` 自行构建，行反序列化只使用行类型的静态 `Deserialize{TypeName}(JSONNode)`。两套 loader 语义不得混用——`cfg.Tables` 的 loader 要求缺表返回 `[]`，而 `IDataPackDataSource.Load` 以 `null` 表示缺表。
- **RP36.** 字符串行键必须附带成本验证：字典哈希/比较成本、每行内存增量、大表（≥10 万行）加载时间，与数字主键基线对比；结果写入进度文档。超出预算时评估"内部数字代理 + 外部字符串"优化（不改变对外契约）。
- **RP37.** **公共 API 必须契约友好**：接口、值类型与事件不泄漏实现细节（不出现 `Luban.SimpleJSON` 之外的内部类型、不出现具体表类型），以便整体进入 SDK 契约包供第三方引用；哪些类型最终进入契约包由契约划分决定（[内容包模型 C5](../brainstorms/2026-08-01-content-pack-quadruple-kickoff.md)）。
- **RP38.** **平台无关**：本库不涉及程序集加载、不依赖 IL2CPP 差异；`TableModel` 在 Mono 与 IL2CPP 上行为一致。运行期代码加载属于编排层，与本库无关。

---

## 五、验收示例

- **AE1. 覆盖 / 追加（RP9a, RP22–RP25）**：`core` 与 `com.acme.qol` 注册同一张表。本体提供 `core:item.potion`、`core:item.tonic`……（共 10 行）；mod 提供 `core:item.tonic`（覆盖）与 `com.acme.qol:item.elixir`（新增）后：查询 `core:item.tonic` 返回 mod 行，查询 `core:item.potion` 返回本体行，查询 `com.acme.qol:item.elixir` 返回新增行，`Count` 为 11。
- **AE2. 同号不同包（RP9a）**：mod A、mod B 与本体各自定义 `com.a:item.ring`、`com.b:item.ring`、`core:item.ring`；三者共存互不覆盖，`Count` 为 3。
- **AE3. 统一查询（RP14–RP16）**：两来源按升序注册后，业务执行 `GetModel<TableModel>().GetTable<TRow>("tbitem")` 得到合并结果，无需感知来源。
- **AE4. 废除与反转（RP25）**：mod A 将 `core:item.tonic` 覆盖为 `is_void = true` 的完整行 → 查询返回 `default`，`Count` 减一，`All()` 不含它；mod B（更高优先级）提供同键的 `is_void = false` 完整行 → 查询恢复。
- **AE5. 零 GAS 引用（RP31）**：测试程序集仅引用 `NexusFramework` 与 `NexusFramework.Config`，全链路测试通过，且不出现任何 GAS 类型引用。
- **AE6. 从零接入（RP12）**：空项目执行一键复制 → 定义一张业务表 → 运行 gen → 业务读取，端到端可运行，全程无手写加载或注册代码（定性验收）。
- **AE7. 拒绝路径（RP8, RP9a, RP21–RP23, RP26–RP27）**：同 `packId` 二次注册 → 整包拒绝；行键命名空间不是已注册来源（如拼错为 `cor:item.tonic`）→ 拒绝并诊断；覆盖包缺一列 → 拒绝并列出字段差异；包内重复行键 → 拒绝并给出键值；上述失败均不改变已注册表的数据。
- **AE8. 纯数据来源（RP23, RP32）**：不带行类型的来源只能注册已存在的 `tbitem`，行 JSON 反序列化为本体类型；尝试注册一张未在本体定义的新表 → 拒绝并诊断"表未注册"。
- **AE9. 单包模式（RP5, RP34）**：不引入编排层，业务直接构造 `IDataPackEntry` 注册一张表并查询，全链路通过。

---

## 六、范围边界

### 移出本模块的条目（归属已裁决）

| 条目 | 归属 | 说明 |
|---|---|---|
| 内容包发现、清单、生命周期、四子包编排 | **`NexusFramework.ContentPacks`（编排）** | 本库只接受"来源标识 + 优先级 + 表声明" |
| 契约包 / SDK、版本治理、服务覆盖 | **内容包模型 C4/C5 + Core IOC** | 本库只保证 API 契约友好（RP37） |
| 程序集加载与交付通道（数据 / 编译期 / 运行期） | **内容包模型 C3 + 编排层** | 本库与平台无关（RP38） |
| 资源包 / 本地化包的能力 | `NexusFramework.Assets` / `Localization` | 本库只存 key（RP3） |
| GAS 私有 `ConfigModel` 重构 | 独立任务（本库完成后） | 验收：GAS asmdef 对 Luban 零引用 |
| 存档落盘、版本迁移 | 存档子系统（未建） | 表数据为只读配置，不入存档 |
| 既有 int 主键表的迁移 | 各表所属模块 | 10 张 GAS 表保持现状；接入 `TableModel` 时需重建为字符串键或由所属模块提供映射 |

### 明确的非目标

- Luban 工具链本体的分发与管理。
- 远程 / 热更表源的**实现**（只保留扩展点）。
- 非 Luban 表源（手写 JSON / ScriptableObject）。
- 行键的自动生成、重命名迁移、别名机制。

---

## 七、依赖与假设

| # | 依赖 / 假设 | 状态 |
|---|---|---|
| D1 | Luban UPM 包已安装，提供 `Luban.Runtime` / `Luban.Editor` | 已核实 |
| D2 | `Tools/Luban/Luban.dll` 与 Agent / MCP 工具链可用 | 已落地（T004） |
| D3 | Luban 版本在 `manifest.json` 中**钉住** | ❌ 未完成——当前为裸 URL，仅 lock 记录 hash。约定内省依赖生成形状，开工前必须钉版 |
| D4 | Demo 启动与 `ArchitectureFactory` 架构 ID 分配兼容 | ❌ 未处理——开工前调用 `ArchitectureFactory.ReserveId(1)` |
| D5 | **Luban 支持 `string` 类型 index** | ❌ **未验证**——现有 10 张表全部为 `int` 主键；[多源验证](../brainstorms/2026-11-17-luban-multi-source-capability.md) §7 V2 给出方法。不支持时回退为"表不设 Luban index、由 `TableModel` 自建字符串字典"（不影响对外契约） |
| D6 | 物品子系统是第一个消费者 | 已确认（R007），定义表使用字符串行键 |
| D7 | 框架为源码级目录结构 | 已确认 |
| D8 | 多源能力边界以 [Luban 多源数据能力验证](../brainstorms/2026-11-17-luban-multi-source-capability.md) 为准 | 已实测（5.1.0）：无 patch、字段无 `json`、数据文件限 `dataDir`、跨程序集行类型不可互操作、运行期强类型新表不可行 |
| D9 | 编排层 `ContentPacks` 可选择性依赖本库 | 未建；本库不得假定其存在（RP5） |

---

## 八、待决问题

### 阻塞开工

- **Q1. 优先级契约的落实方式**：A. 保留逐包注册 + 调用方升序契约（实现做顺序校验）；B. 增加 `RegisterDataPacks(列表)` 批量入口由实现排序。默认建议 A。
- **Q2. Luban 版本钉定**（D3）——UPM 依赖改为钉 commit，并在约定常量类注明对应版本。
- **Q3. Luban 字符串 index 验证**（D5）——把 `index` 改为 string 键跑 `validate`，确认生成形状与反序列化器签名是否随之变化。

### 规划阶段定

- **Q4. 表名的命名空间**：表名跨来源共享（同名同列即同表）。是否要求新表名带领域前缀以降低意外同名概率？本期不强制。
- **Q5. `packId` 与程序集名 / 命名空间的对应关系**。
- **Q6. `Resources/DataPacks` 的打包策略**：Resources 全量进包 vs Addressables；影响 RP29 的参考实现，不阻塞接口。
- **Q7. `ITableReader.Get` 的最终语义**：`Get` 与 `GetOrDefault` 都返回 `default` 属重复；保留哪个名字、是否提供"缺失抛异常"版本，需与 Luban 使用习惯对齐。
- **Q8. 大表枚举形态**（RP18）：缓存只读视图 vs `CopyRowsTo`，明确 Unit 1 交付哪一项。

---

## 九、关联文档

- 前身需求：[2026-08-01 数据包需求（归档）](../brainstorms/2026-08-01-data-pack-requirements.md)
- 内容包模型：[内容包模型（C1–C10）](../brainstorms/2026-08-01-content-pack-quadruple-kickoff.md)
- 能力基线：[Luban 多源数据能力验证](../brainstorms/2026-11-17-luban-multi-source-capability.md)
- 边界裁决：[Config 与物品需求边界分析](../brainstorms/2026-11-17-config-item-boundary-analysis.md)
- 接口设计：[D006 数据包接口契约](../design/D006-data-pack-interface-design.md)（键类型与数据源语义待同步）
- 实施计划：[2026-08-01-001 数据包接口 plan](../plans/2026-08-01-001-feat-data-pack-interfaces-plan.md)
- 复核：[plan 时效性核查](../progress/2026-11-17-data-pack-plan-review.md)、[D006/D007 接口审查](../progress/2026-11-17-d006-d007-design-review.md)
- 消费方需求：[R007 物品与存储](R007-item-storage.md)
