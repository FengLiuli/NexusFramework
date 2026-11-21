---
task: 数据包子系统（plan 2026-08-01-001）
task_status: 复核完成（等待修订后开工）
created: 2026-11-17
updated: 2026-11-17
sessions: 1
---

# 复核记录：NexusFramework.Config 实施计划时效性核查

> 复核对象：[docs/plans/2026-08-01-001-feat-data-pack-interfaces-plan.md](../plans/2026-08-01-001-feat-data-pack-interfaces-plan.md)
> 复核原因：plan 写于 2026-08-01，距今 3.5 个月；期间框架层有多次提交，需确认 plan 的技术假设是否仍成立。
> 复核方法：逐条核对 plan 中引用的文件、类型、签名与程序集边界；核对 2026-08-01 之后的 git 提交影响面。

---

## 结论

**plan 可以开工，但需先完成 2 项阻塞修订和 5 项低成本修订。**

- 引用事实核对：**12/12 文件存在**，关键类型与签名全部成立；
- 阻塞项 2 个（R-2 Luban 版本未钉、R-4 架构 ID 分配器与 Demo 启动冲突）；
- 低成本修订 5 个（R-1、R-3、R-5、R-6、R-7）；
- 无任何一项推翻 plan 的核心设计（三层存储、行级合并、约定内省、零 GAS 测试链）。

---

## 一、核对通过项

| # | plan 中的假设 | 核对结果 |
|---|---|---|
| 1 | 12 个引用文件存在（Framework.cs、IConfigLoader.cs、ConfigModel.cs、LubanConfigLoaderGenerator.cs、Tbunit.cs、Tbability.cs、Tables.cs、unit.cs、LubanConfigLoaderGeneratorTests.cs、tbunit.json、DemoGameArchitecture.cs、manifest.json） | ✅ 全部存在 |
| 2 | `AbstractModel : IModel` 存在 | ✅ `Framework.cs:1004`（plan 写 973，行号漂移） |
| 3 | `GetModel<T>()` 为容器查表、不自动创建 | ✅ `FrameworkArchitecture.cs:331` → `mContainer.Get<T>()`；`IOCContainer.Get` 未命中返回 `null`，不抛异常、不创建 |
| 4 | `ConfigModel` 为 GAS 专属字典 + Register/Get | ✅ 私有 `Dictionary<int, ...>` × 5，GAS 类型硬编码 |
| 5 | Luban 生成形状：`Tables(Func<string, JSONNode>)` | ✅ `Tables.cs:28` |
| 6 | 行类型：静态 `Deserializeunit(JSONNode)` | ✅ `unit.cs` |
| 7 | 主键：`_dataMap.Add(_v.ID, _v)`，int 大写 `ID` | ✅ `Tbunit.cs:32`（注：`TbgameplayTags` 用**小写 `id`**，见 R-8） |
| 8 | GAS asmdef 零 Luban 引用、GUID 引 Core | ✅ `GUID:0de6792a94c3d1943a2a75abf64597f1`，引用表无 Luban |
| 9 | `Tests.asmdef` 用 `UNITY_INCLUDE_TESTS` + 预编译 nunit | ✅ 结构一致 |
| 10 | Luban UPM 包已安装 | ✅ manifest.json 第 4 行 |
| 11 | `Tools/Luban/Luban.dll` 缺失 | ❌ 已存在（T004 接入），见 R-3 |
| 12 | 合并/内省/撤销语义不依赖编译期泛型 | ✅ 与框架现状无冲突 |

---

## 二、阻塞修订项

### R-2 Luban 版本未钉（风险兜底缺失）

- **plan 原文**（Dependencies / Risks）："manifest.json 应钉住 Luban 版本，升级 Luban 时重跑验证集。"
- **实际**：`Packages/manifest.json` 中为 `"https://github.com/focus-creative-games/luban_unity.git"`，**无 tag、无 commit**。
- **影响**：plan 的核心实现（约定内省：主键字段、`is_void` 列、静态 `Deserialize{TypeName}(JSONNode)` 签名）全部依赖 Luban 生成形状。上游一改生成模板，合并器与内省器会静默失配。
- **建议**：开工前把 URL 改为钉 commit 的 Git 依赖（`...git#<commit>`），并在 `DataPackConventions` 上方注明"对应 Luban 版本"。这是 Unit 3 的隐性前置。

### R-4 架构 ID 分配器与 Demo 启动方式冲突（新发现）

- **背景**：提交 `a31094f` 引入 `ArchitectureFactory` 架构 ID 位图（`mIdOccupied[256]`）。`CreateArchitecture(type, null)` 从 1 起自动分配；显式 ID 会检查占用并拒绝重复。
- **现状**：Demo 启动走 `DemoSceneManager.cs:92` → `new DemoGameArchitecture()`，**完全绕过工厂**；`DemoGameArchitecture` 的无参构造里写死 `ArchitectureId = 1`。
- **两个后果**：
  1. 工厂位图不知道 1 已被占用 → 后续任何自动分配都可能把 1 分给别人，造成架构 ID 撞号（CarrierId 高 8 位是架构 ID，撞号即载体空间污染）；
  2. `DemoGameArchitecture` 没有 `(byte id)` 构造函数 → `CreateArchitecture` 用 `Activator.CreateInstance(type, id)` 调用会抛 `MissingMethodException`，即该架构**无法经工厂创建**。
- **影响 plan 的位置**：Unit 6「Modify DemoGameArchitecture.cs（启动引导）」。plan 假设可以平滑地在这里加三步注册，但没意识到启动路径本身要改。
- **建议（二选一，开工前定）**：
  - 方案 A（改动小）：保留 `new DemoGameArchitecture()`，但在使用前调用 `ArchitectureFactory.ReserveId(1)`，让工厂知道 1 被占用；
  - 方案 B（更统一）：给 `DemoGameArchitecture` 补 `(byte id) : base(id)` 构造，统一走 `ArchitectureFactory.CreateArchitecture("Demo", 1)`。
  - 推荐 **A**：本次目标是 Config 子系统，不要顺带重构架构生命周期；A 是一行改动。

---

## 三、低成本修订项

### R-1 行号漂移

plan 引用 `Framework.cs:973`、`FrameworkArchitecture.cs:292`，实际为 `:1004`、`:331`。建议改为符号引用（"`AbstractModel`（`Framework.cs`）"），避免下次再漂。

### R-3 Luban CLI 前置已解除

- **plan 原文**：`Tools/Luban/Luban.dll` 当前缺失，AE5 以人工导入生成产物先行验证。
- **实际**：T004 已接入 Luban AI 工具链，`Tools/Luban/` 完整，Luban MCP `list-tables` 实测可用。
- **建议**：删除 Deferred 中的"Luban CLI 下载与钉版"小任务；Unit 6 的"人工导入替代"分支可删；AE5 可直接走完整 CLI 路径。

### R-5 TableModel 的注册位置与"零 GAS"边界未写明

- **现状**：`GASArchitecture.OnInit` 已注册 `ConfigModel` 与 `IConfigLoader`（作为 Utility）。
- **plan 未写**：`TableModel` 注册在哪一层。若放进 `GASArchitecture`，则 GAS 程序集必须引 `NexusFramework.Config`，与"GAS asmdef 对 Luban 完全无感知"的最终目标直接冲突。
- **建议**：明确写成 **`TableModel` 只由 Demo 层（或更上层业务）注册**，`GASArchitecture` 保持零 Config 引用。Unit 6 的"Modify DemoGameArchitecture"应写明是在 `base.OnInit()` 之后追加，而不是改基类。

### R-6 数据源目录约定未定，但下游已产生依赖

- **plan 原文**：`Resources/DataPacks/{PackId}/` 列为"实现期定稿"。
- **问题**：物品系统的物品定义表将成为第一个消费者，其 Demo 数据必须落到同一约定下。
- **建议**：在 Unit 4 开工前直接定为 `Resources/DataPacks/{packId}/{dataName}.json`（小写包名、表名不带 `tb` 前缀），写入 `DataPackConventions`。

### R-7 现有表全部没有 `is_void`，Unit 6 不能"复用现有表结构"

- **plan 原文**（Unit 6）："至少一张可复用现有 GAS 表结构语义的业务表"。
- **问题**：现有 10 张表（`#unit.xlsx` 等）均无 `is_void` 列；而 R9 规定"已发布且不含该列的旧包不支持覆盖方删除语义（注册期拒绝）"。
- **结论**：语义可复用，**结构必须新建**（用模板生成含 `is_void` 的骨架表）。
- **建议**：改写 Unit 6 措辞为"以现有 `unit` 表语义为蓝本，用模板**新建**含 `is_void` 的表结构"。

### R-8 主键大小写不是全局约定

- **实测**：`exgas.TbgameplayTags` 的主键是**小写 `id`**，其余表是**大写 `ID`**。
- **影响**：`DataPackConventions.ID = "ID"` 的常量在遇到该表时会失效。
- **建议**：约定收敛为"物品/新数据包统一使用大写 `ID`"，并在内省器中显式拒绝非 `ID` 主键（给出诊断），而不是静默兼容。这条与物品系统直接相关——物品定义表必须用大写 `ID`。

---

## 四、三个月内框架变更的影响面

| 变更 | 对 plan 的影响 |
|---|---|
| `feat: 引入 Carrier 索引表注册机制`（D005 落地） | 无直接影响；`TableModel` 不涉及 Carrier。**对物品系统有正面影响**（容器归属可直接用索引） |
| `refactor: GASEntityMapModel 改用 Carrier 索引` | plan 的 Context 未引用该文件，无影响 |
| `feat: ArchitectureFactory 引入架构ID分配器与 Carrier 命名空间隔离` | **见 R-4**，影响 Demo 启动路径 |
| `refactor: Tag 职责从 CarrierManager 剥离` | 无影响 |
| `chore: GAS 包依赖的 Burst 版本调整为 1.8.19` | 无影响 |

---

## 五、建议的修订顺序

1. **R-2**（钉 Luban 版本）—— 5 分钟，但影响 Unit 3 的形状假设；
2. **R-4**（架构 ID）—— 决定 Unit 6 的启动代码写法；
3. R-1 / R-3 / R-5 / R-6 / R-7 / R-8 —— 文档层面同步修订，可一次性完成；
4. 修订完成后勾选 **Unit 1**（接口契约）开工，Unit 1 结束后进入接口评审门。

---

## 六、未决项（本次复核无法回答，需要业务确认）

- 数据包 `PackId` 的命名空间是否要与程序集名一致（`dpack.core` vs `Demo.DataPack`）——plan 给了命名空间，未规定与 asmdef 的对应关系。
- `Resources/DataPacks` 是否会在打包时被裁剪（Resources 全量进包 vs Addressables）——影响 R-6 的最终形态，但不阻塞接口。
