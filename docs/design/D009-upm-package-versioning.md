---
layer: design
status: draft
created: 2026-11-19
updated: 2026-11-20
---

# 设计：UPM 包版本号分割方案

## 0. 范围与术语

本方案只覆盖 **UPM 包（Unity Package Manager package）的版本号**，即 `package.json` 中的 `version` 字段、包间依赖声明与发布标签。

“版本号分割”在本方案中拆成三件事，下文按顺序展开：

1. **版本轴分割**：哪些东西各占一条版本线，互不派生；
2. **发布单元分割**：仓库里的哪些目录构成一个包，包的边界与程序集边界如何对齐；
3. **版本号语义分割**：`MAJOR.MINOR.PATCH[-PRE]` 每一段表达什么，什么时候递增。

明确不属于本方案的内容：

| 版本类型 | 归属 | 与包版本的关系 |
|---|---|---|
| 契约版本 `contractVersion`（内容包清单） | R008 / 内容包体系 | 独立递增，不跟随包版本 |
| Luban 配置数据版本 | 数据包 / 配置加载 | 独立递增，不跟随包版本 |
| 存档格式版本 | 存档子系统 | 独立递增，不跟随包版本 |
| 应用版本（PlayerSettings `bundleVersion`） | 产品工程 | 独立递增，可以引用包版本作参考，但不相等 |
| 包内单个程序集版本 | Unity 不使用 | 不维护；包版本是唯一版本轴 |

**核心原则：包版本只描述“包的公开 API 与运行时行为”，不承载数据格式与产品发布节奏。** 把包版本写进存档或配置格式，会让两类变更互相绑架。

---

## 1. 现状与问题

### 1.1 现状

| 事实 | 位置 |
|---|---|
| 两个包：`com.nexusframework.core`、`com.nexusframework.gas`，`package.json` 中暂为 `1.0.0`，**从未发布**，仅作占位；首个正式版本定为 `2.0.0`（见 §5.2） | `Assets/NexusFramework/package.json`、`Assets/NexusFramework.GAS/package.json` |
| 包目录位于 `Assets/` 下，本地开发是“源码内嵌”，Unity 不按包依赖解析 | `Assets/NexusFramework*` |
| GAS 依赖 Core，但依赖关系只体现在 asmdef GUID 引用上 | `NexusFramework.GAS.asmdef` → `GUID:0de6792a94c3d1943a2a75abf64597f1` |
| GAS 的 `package.json` **没有**声明 `com.nexusframework.core` | `Assets/NexusFramework.GAS/package.json` |
| 无 git tag、无 CHANGELOG、无版本校验 | 仓库现状 |
| 远程仓库只有 `main` 分支、没有 tag（截至 2026-11-20）；已通过 GitHub URL 导入，但引用的是分支或 commit | `git ls-remote --tags origin` |
| 两个包都没有 `README.md`、`Documentation~`、`Samples~` | 包根目录 |
| Editor 程序集 `NexusFramework.GAS.Editor` 与运行时同包，作为内容模块的子模块，不独立成包 | `Assets/NexusFramework.GAS/Editor/` |

### 1.2 问题

1. **发布形态与开发形态脱节**：对外通过 git URL 安装时，GAS 无法解析到 Core；本地能编译只是因为两者都在 `Assets/` 下。
2. **还没有正式发布**：没有 tag 与 CHANGELOG，`package.json` 里的 `1.0.0` 只是占位，不是已承诺的 API；首个正式版本定为 `2.0.0`（§5.2）。
3. **没有递增规则**：当前仓库正在做 `CarrierId` 移除、Identity 重构等破坏性变更，如果没有版本规则，下游无法判断升级风险。
4. **包边界没有约束**：`Demo`、`Tests`、`BurstRepro` 与两个包同仓，但发布包不能引用包外代码，目前没有机制保证。

---

## 2. 决策摘要

| 编号 | 决策 | 结论 |
|---|---|---|
| V1 | 多包版本是否锁步 | **不锁步**：每包独立 SemVer，Core 为兼容锚点 |
| V2 | 版本格式 | `MAJOR.MINOR.PATCH[-exp.N\|-pre.N\|-rc.N]`，不用 `+build` |
| V3 | 递增判据 | 以**公开 API 与运行时行为**为准，见 §5 |
| V4 | 依赖声明 | UPM 只支持精确版本；依赖方声明**最低兼容版本**，升级时取最高 |
| V5 | Editor 程序集 | 永不独立成包；作为内容模块的子模块随宿主包发布 |
| V6 | Demo/Tests | 不属于任何包，发布包内不得引用包外代码 |
| V7 | 版本真源 | `package.json` 为唯一真源；CHANGELOG 必须同步 |
| V8 | 发布标识 | 每包独立 tag：`core-v2.2.0` / `gas-v2.2.0` |
| V9 | 兼容窗口 | 只维护最新 `MINOR` 的 patch；上一条 `MINOR` 仅接受严重修复 |
| V10 | 破坏性变更的缓冲 | **过渡 API**：源码级断裂先以 `MINOR` 提供兼容入口，删除在 `MAJOR`，见 §5.4 |
| V11 | 过渡 API 标记 | 使用 `[TransitionalApi]` 自定义特性 + `[Obsolete]`；数量不设上限，README 区块由扫描器全量生成 |
| V12 | 分发渠道 | **不使用 scoped registry / npm registry**；只走 git URL + tag |
| V13 | 首发版本 | 首个正式版本定为 `2.0.0`（一次性吸收当前全部破坏性变更），其后按 §5.1 递增 |

---

## 3. 发布单元分割（包边界）

### 3.1 包与目录

```text
Assets/NexusFramework/          → com.nexusframework.core
├── package.json
├── README.md                  （必需，含过渡 API 清单）
├── CHANGELOG.md               （必需）
├── Documentation~/            （本包独立，按需）
├── Samples~/                  （本包独立，按需）
├── NexusFramework.asmdef
└── ...（运行时源码与 .meta）

Assets/NexusFramework.GAS/      → com.nexusframework.gas
├── package.json
├── README.md                  （必需，含过渡 API 清单）
├── CHANGELOG.md               （必需）
├── Documentation~/            （本包独立，按需）
├── Samples~/                  （本包独立，按需）
├── NexusFramework.GAS.asmdef           （运行时）
├── Editor/NexusFramework.GAS.Editor.asmdef  （Editor-only，随包发布）
└── ...（源码与 .meta）

Assets/Demo/、Assets/Tests/、Assets/BurstRepro/、Assets/DataGenerated/、
Assets/Resources/、Assets/Scenes/、Assets/Settings/、Assets/Editor/  → 不属于任何包
```

规则：

1. **一个包 = 一个目录 + 一份 `package.json`**。包的根目录就是 `package.json` 所在目录，`?path=` 指向它。
2. **包内的 `.meta` 必须随包发布**。缺少 asmdef 的 `.meta` 会导致 GUID 引用失效。
3. **包内代码不得引用包外代码**（白名单：Unity 官方包与 `package.json` 中声明的依赖）。`Demo`/`Tests` 可以引用包，反向不允许。
4. **文档与示例按包独立**：`Documentation~/`、`Samples~/` 各自建在包根目录内，不设仓库级共享目录，也不跨包共用。不需要时可以不建；需要时必须随包发布。详见 §3.4。
5. **每个包必须有 `README.md`**，并随版本变更按需更新（§3.4.1）。不要用仓库根 README 代替包 README。
6. **Editor 程序集永不拆包**：`NexusFramework.GAS.Editor` 是内容模块的子模块，与运行时同包、同版本发布；它不是独立发布单元，也不占用独立版本号。同一包内可以有多个 Editor 子模块（`Editor/` 下按功能分目录），它们共享宿主包的版本。
7. 未来新包命名统一为 `com.nexusframework.<module>`，目录同名，从 `0.1.0` 起版；对外承诺时通常升到 `1.0.0`，但若承诺时已积累破坏性变更，也按 §5.2 的同类逻辑直接定为首个 `MAJOR`。
8. **包目录内不得残留内部文档**：当前 Core 包根的 `NexusFrameworkDevelopmentRules.md` 若面向外部，应移入 `Documentation~/`；若属内部规则，应移出包目录（如仓库 `docs/`）。GAS 的 `Generated/`（运行时代码）随包发布；`Config/` 若为编辑器配置应改为 `Config~/` 或移出包。`Assets/DataGenerated/`（Luban 产物）不属于任何包。

### 3.2 程序集引用方式

- 包内 asmdef 引用包内程序集：使用**名称引用**而非 GUID。GUID 可读性差，且在合并、迁移、复制目录时容易失效；名称引用跨包可解析。
- 当前 `NexusFramework.GAS.asmdef` 对 Core 使用 `GUID:0de6792a94c3d1943a2a75abf64597f1`，应改为 `"NexusFramework"`；`NexusFramework.GAS.Editor.asmdef` 已是名称引用，无需改动。
- 不要用 `versionDefines` 在框架源码里做“按 Core 版本条件编译”。版本兼容应由依赖声明保证，条件编译会让同一份源码产生多种行为，破坏可测试性。

### 3.3 包间依赖声明

GAS 的 `package.json` 必须声明：

```json
"dependencies": {
  "com.nexusframework.core": "2.0.0",
  "com.unity.entities": "1.3.15",
  "com.unity.mathematics": "1.3.2",
  "com.unity.collections": "2.5.7",
  "com.unity.burst": "1.8.19"
}
```

说明：

- UPM **不支持** `^`、`~`、范围表达式；写什么版本就解析什么版本。
- 这里写的是**最低兼容版本**，它在两种来源下语义不同：
  - **registry 来源（当前不采用，见 V12）**：多个间接依赖请求同一包的不同版本时，UPM 取最高版本；因此写最低版本即可。
  - **git URL 来源（当前采用）**：版本由宿主 `manifest.json` 里的 `#<tag>` 决定，本字段**不参与自动解析**，只作文档声明。Unity 采用“直接依赖优先（direct dependency wins）”，宿主显式声明的 Core 会覆盖 GAS 的间接要求；**宿主必须保证自己写的 Core tag ≥ 这里声明的版本**，否则 GAS 在用到新 API 时会编译失败且 UPM 不会纠正。
- 只有当 GAS **实际使用了 Core 新增的 API** 时，才提升这里的版本，并在兼容矩阵中登记。

### 3.4 README、文档与示例

三者职责不同，不能互相替代。**`package.json` 的 `description`/`displayName` 与 `samples[].displayName/description` 会显示在 Unity Package Manager UI 中，必须使用英文**；README、Documentation~、CHANGELOG 面向开发者，可用中文。

| 载体 | 定位 | 是否必需 | 位置 |
|---|---|---|---|
| `README.md` | 包的入口：定位、安装、依赖、最小示例 | **必需** | 包根目录 |
| `Documentation~/` | 完整使用文档与迁移指南 | 按需，每包独立 | 包根目录 |
| `Samples~/` | 可运行的验证示例 | 按需，每包独立 | 包根目录 |

#### 3.4.1 README.md（必需）

每个包根目录必须有 `README.md`，至少包含：

1. 包名与一句话定位；
2. 安装方式（git URL + `?path=`，版本写 `<version>` 占位或指向最新 tag，不写死过期版本）；
3. 依赖与最低 Unity 版本；
4. 最小可运行示例（可直接复制的代码块）；
5. 文档、示例、CHANGELOG 的链接；
6. **过渡 API 清单**：若包内存在过渡 API，必须全部列出；该清单由扫描器生成（格式见 §5.4.6），不得手写、省略或只写“见代码”；
7. 与关联包的版本对应关系（GAS 需写明兼容的 Core 范围）。

**更新规则：每次发布前必须分析本次变更，再决定是否更新 README。**

分析依据是本次 `CHANGELOG.md` 的条目与 §5.1 的递增判定，逐段判断：

| README 段落 | 触发更新的变更 |
|---|---|
| 包名与定位 | 包重命名、职责范围变化 |
| 安装方式 | 包名、依赖声明、tag 规则、最低 Unity 版本变化 |
| 最小示例 | 公开 API 改名/签名变化、语义变化、引入过渡 API |
| 链接 | `Documentation~/`、`Samples~/` 增删或改名 |
| 过渡 API 清单 | 新增/删除过渡 API，替代 API 或移除版本变化（重新生成区块，勿手改） |
| 兼容关系 | 兼容矩阵（§7）变化 |

- 受影响 → 必须在同一发布提交中更新；
- 不受影响 → 在发布记录（Release 说明或发布 PR 描述）中写一行 `README: no change required`，作为已分析的证据；
- **不得**为了“看起来有更新”而做无意义改动，也不得跳过分析。

#### 3.4.2 Documentation~/（每包独立）

- 固定放在包根目录 `Documentation~/`。目录名以 `~` 结尾，Unity 不导入，但会随 git 包分发；
- **每包独立**：不建仓库级共享文档目录，不把一个包的文档放进另一个包的 `Documentation~`；
- 最低结构：`Documentation~/index.md`，其余按主题分文件（安装、核心概念、常用任务、迁移指南）；
- 内容面向使用者，不写内部实现细节，不写“未来计划”（计划放 issue / CHANGELOG）；
- 文档中的 API 名、版本号必须与代码及 `package.json` 一致；
- 每次 `MAJOR` 发布必须在文档中补一节“从上一 MAJOR 迁移”，迁移清单可直接引用 README 的过渡 API 表；若本包尚未建立 `Documentation~/`，迁移说明写在 `CHANGELOG.md` 中；
- 跨包引用使用 GitHub 上发布后的稳定链接，或相对路径，不假设宿主工程中的目录布局。

#### 3.4.3 Samples~/（每包独立，按需）

- 固定放在包根目录 `Samples~/<SampleName>/`；
- 必须在本包 `package.json` 的 `samples` 数组登记：

```json
"samples": [
  {
    "displayName": "Minimal GAS Setup",
    "description": "Minimal runnable example initializing a GAS entity and attributes",
    "path": "Samples~/MinimalGASSetup"
  }
]
```

- 示例只能引用本包与 `package.json` 中声明的依赖，**不得引用 `Assets/Demo`、`Assets/Tests` 或另一个包的示例**；
- 示例必须能在干净工程中导入并编译通过；发布前按 §8.1 验证；
- 示例随包版本维护：API 变更导致示例失效时，必须同步修改或删除，不允许留下编不过的示例；
- **不需要就不建目录**，不提交空目录，也不为了凑数提供示例。

#### 3.4.4 三者一致性

- README 的最小示例与 `Samples~/` 中的示例必须能对应上；API 变更时一起改；
- Documentation~ 的安装说明与 README 的安装说明必须一致；
- 三者都随包发布，版本与包版本绑定，不单独版本化。

---

## 4. 版本号格式

### 4.1 格式

```text
MAJOR . MINOR . PATCH [ - PRE_TAG . N ]
```

| 段 | 含义 | 递增条件（摘要） |
|---|---|---|
| `MAJOR` | 公开 API 的**不兼容**变更边界 | 删除已过过渡期的 API、无法过渡的语义变更（时序/异常/线程模型）、提升最低 Unity 版本、依赖包大版本升级导致宿主必须改动 |
| `MINOR` | **向后兼容**的能力新增 | 新增公开 API、新增过渡 API（§5.4）、新增可选行为、新增依赖但默认无影响 |
| `PATCH` | **向后兼容**的修复 | Bug 修复、性能优化、文档修正、无公开 API 变化的内部重构 |
| `PRE_TAG` | 预发布通道 | `exp`（实验）/ `pre`（预览）/ `rc`（候选），见 §6 |

规则：

1. 三段都是非负整数，禁止前导零；禁止 `1.0`、`1`、`v1.0.0`。
2. **不使用 `+build` 元数据**：UPM 官方生态与 git tag 的排序语义都不依赖它，容易制造“版本相同但内容不同”的困惑。构建编号放进 git commit / Release 说明。
3. 版本号只在**发布时**递增；日常提交不修改 `package.json`。

> **为什么不用四段版本号（如 `重大.MAJOR.MINOR.PATCH`）**：UPM 要求 `version` 是合法 SemVer，四段会被判定为非法版本，包无法被安装或依赖。因此“重大变更”不作为独立版本段；破坏性变更通过**过渡 API**（§5.4）缓冲到下一次 `MAJOR`，从而降低 `MAJOR` 的实际出现频率。若未来确需区分“世代”，只能使用包重命名或 `MAJOR` 跳档，且都不改变三段格式。

### 4.2 包的“公开 API”定义

框架的公开 API 边界是**程序集 + `public` 可见性**：

- `NexusFramework` 程序集中所有 `public` 类型与成员，包括 `TransitionalApiAttribute` 本身；
- `NexusFramework.GAS` 程序集中所有 `public` 类型与成员；
- `NexusFramework.GAS.Editor` 仅对 Editor 使用者可见，但**同样**受版本规则约束（Editor 工具也是下游依赖）；
- `internal`、`private`、程序集内实现细节不构成承诺，可自由重构——但 `InternalsVisibleTo` 暴露给 `Tests` 的成员不在承诺范围内。
- **发布包内的 `InternalsVisibleTo` 必须使用唯一程序集名**（如 `NexusFramework.Tests`、`NexusFramework.GAS.Tests`），不得使用 `Tests` 这类通用名：包头部的 `AssemblyInfo.cs` 会随包进入每个宿主，通用名可能与宿主的同名程序集冲突并意外开放 internal 访问。当前 `Assets/NexusFramework/AssemblyInfo.cs` 使用 `"Tests"`，需一并修正。

行为兼容性按同样标准判断：公开方法的可观察行为（返回值、事件时机、异常类型）改变，即使签名不变，也属于不兼容变更。

---

## 5. 递增规则

### 5.1 判定表

| 变更 | 版本段 | 示例 |
|---|---|---|
| 删除、重命名公开类型/成员 | **删除动作**在 `MAJOR`；此前用过渡 API（§5.4）在 `MINOR` 提供兼容入口 | 删除 `CarrierId`：先在 2.x 标记 Obsolete 并提供 `CarrierKey`，3.0.0 删除 |
| 修改公开成员签名 | 保留旧签名重载并转发：`MINOR`；不保留：`MAJOR` | `TryGet(ushort)` → `TryGet(int)`，旧重载保留 |
| 新增过渡 API（Obsolete 转发、适配方法） | `MINOR` | 旧名成员标记 `[TransitionalApi]` + `[Obsolete]` 并转发到新名 |
| 修改公开 API 的可观察语义 | `MAJOR`（不得用过渡 API 伪装成兼容） | 事件从“同步派发”改为“下一帧派发” |
| 修改 `package.json` 的 `unity`/`unityRelease`（提升最低 Unity 版本） | `MAJOR` | 2022.3 → 2023.1 |
| 依赖包发生 `MAJOR` 且宿主需要改动 | `MAJOR` | Entities 1.x → 2.x |
| 新增公开 API、新增可选配置、新增包 | `MINOR` | 新增 `Identity` 子系统接口 |
| 新增依赖但宿主无需改动 | `MINOR` | GAS 新增 Burst 依赖 |
| Bug 修复、性能优化 | `PATCH` | 修复 Trait 反序列化顺序 |
| 无公开 API 变化的内部重构 | `PATCH` | Carrier 索引内部结构调整 |
| 仅文档、注释、测试 | 不发版 | 合并进下一个版本 |
| 修改 `package.json` 描述、关键词、文档链接 | `PATCH`（可攒批） | 修正 `documentationUrl` |

### 5.2 首个正式版本：`2.0.0`

- **已发布**的定义：仓库存在对应 tag（`core-v2.0.0` / `gas-v2.0.0`）且有 GitHub Release。
- 当前未提交状态是一次性吸收大量破坏性变更（`CarrierId` 移除、Identity 重构等）的**首个正式版本**，定为 `2.0.0`：
  - 首发之前没有任何已发布版本，因此这些破坏性变更不构成“对下游的破坏”；
  - 直接用 `2.0.0` 而非 `1.0.0`，是为了让 `MAJOR=2` 明确表示“第二个世代”，避免把“首个正式版本”与“对 1.0.0 的破坏升级”混为一谈；
  - `package.json` 中的 `1.0.0` 只是占位，发布时一并改为 `2.0.0`。
- **首个正式版本不从 `1.0.0` 起算**：一旦 `core-v2.0.0` / `gas-v2.0.0` 的 tag 存在，之后严格按 §5.1 递增（下一个 `MAJOR` 为 `3.0.0`），不回溯修改首个版本的内容。
- 本项目**不使用 `0.x` 阶段，也不使用 `1.x` 阶段**。§5.4.8 的 `0.x` 规则保留给未来从零开始的新包，不适用于当前两个包。

### 5.3 依赖版本与宿主 Unity 版本

- `com.unity.*` 依赖升级：若 API 不变，随 `MINOR`；若要求宿主改代码，随 `MAJOR`。
- 任何**缩小“可安装 Unity 版本集合”**的改动一律 `MAJOR`，即使代码本身没变——它决定了包能否被安装。`unity` 字段（如 `2022.3` → `2023.1`）与 `unityRelease` 字段（如 `0f1` → `0f3`）同级处理。
- 仅当源码确实用到更高 patch 才有的 API 时才提升 `unityRelease`；不得为了“用上最新编辑器”而随手提升。
- 同一发布批次中，若 Core 只是 `PATCH`，GAS 不需要因 Core 发版而发版；GAS 的版本只由 GAS 自身变更决定。

### 5.4 过渡 API（Transitional API）约定

#### 5.4.1 定义

**过渡 API 是在不兼容变更发生时，为旧调用点保留的、可编译可运行的兼容入口。** 它的作用是把“源码断裂”从 `MINOR` 推迟到下一次 `MAJOR`，从而让 `MINOR` 发布始终保持可升级。

过渡 API 必须同时具备三个属性：

1. **可编译**：旧调用点不改一行代码即可通过编译；
2. **行为等价**：转发到新实现，行为与新 API 一致；
3. **有终点**：明确写出移除它的 `MAJOR` 版本，不是永久的兼容层。

#### 5.4.2 成立条件

一个兼容入口只有**全部满足**以下条件，才算过渡 API：

| # | 条件 | 判定方式 |
|---|---|---|
| 1 | 旧调用点源码不改即可编译 | 用旧调用样例编译通过 |
| 2 | 行为与新 API 等价 | 同一输入产出相同结果/事件 |
| 3 | 有机器可读的替代指引 | `[TransitionalApi].Replacement` 与 `[Obsolete]` 消息一致 |
| 4 | 有明确的移除版本 | `[TransitionalApi].RemoveVersion` 为具体 `MAJOR`，且与 `[Obsolete]` 消息一致 |
| 5 | 有测试覆盖转发路径 | 旧入口与新入口跑同一组断言 |

任一条不满足，它就是**破坏性变更**，必须按 §5.1 升 `MAJOR`，不得占用 `MINOR`。

#### 5.4.3 生命周期

```text
2.4.0  MINOR  新 API 上线；旧 API 保留并标记 Obsolete(warning)，消息写 "Removed in 3.0.0"
2.5.0  MINOR  旧 API 仍可用（可在 CHANGELOG 中再次提醒）
3.0.0  MAJOR  删除旧 API 与过渡代码
```

规则：

1. **引入过渡 API 是 `MINOR`**（新增公开兼容入口），不是 `PATCH`；
2. **删除过渡 API 是 `MAJOR`**，且必须与 §5.4.6 的到期审查同步；
3. 过渡期至少跨一个 `MINOR` 发布，禁止“引入后下一个 `PATCH` 就删”；
4. **禁止在 `MINOR` / `PATCH` 中使用 `[Obsolete(..., true)]`**：它会让下游编译失败，等于把 `MAJOR` 级断裂伪装成兼容版本。`Obsolete(error)` 只能与删除动作同版本发布（即 `MAJOR`）；
5. 若过渡 API 引入了错误，修复它按 `PATCH` 处理，不得借机缩短过渡期。

#### 5.4.4 形态规范

| 变更形态 | 过渡方式 | 注意事项 |
|---|---|---|
| 方法/属性/事件改名 | 旧成员保留，转发到新成员 | 旧成员不得复制实现，避免两份逻辑漂移 |
| 修改方法签名 | 保留旧签名重载，转发到新重载 | 防止与新重载产生重载歧义 |
| 类型改名 | 旧类型保留为 Obsolete 包装；可继承时继承新类型 | 必须评估 `is` 判断、序列化、`Equals`/`GetHashCode` 差异；值类型与 `sealed` 类型不能用继承，改用静态转发或扩展方法 |
| 字段改属性 | 改为同名属性 | 源码兼容；但会破坏 `ref`/`out` 传参、字段初始化器与按字段名反射，确认无这些用法或过渡期保留方法；二进制兼容不承诺（UPM 以源码分发，可接受） |
| 枚举成员改名 | 旧成员保留并标记 Obsolete | **枚举值不得改变**，否则存档/配置反序列化会错位 |
| 接口新增成员 | 新增**可选接口**或扩展方法 | 不使用默认接口实现（DIM）做过渡：Unity/IL2CPP 支持不稳定 |
| 泛型约束、基类、类型层级变化 | 无可靠过渡方式 | 直接 `MAJOR` |

#### 5.4.5 标记与组织

过渡 API 使用**两个标记**：`[Obsolete]` 负责编译器提示，自定义 `[TransitionalApi]` 负责机器可读元数据。两者必须同时存在。

特性定义放在 Core 包内：

```csharp
// Assets/NexusFramework/Annotations/TransitionalApiAttribute.cs
using System;
using System.Diagnostics;

namespace NexusFramework
{
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum |
        AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field |
        AttributeTargets.Event | AttributeTargets.Interface,
        Inherited = false)]
    [Conditional("UNITY_EDITOR")]
    public sealed class TransitionalApiAttribute : Attribute
    {
        public string Replacement { get; }
        public string RemoveVersion { get; }
        public string Migration { get; }

        public TransitionalApiAttribute(string replacement, string removeVersion, string migration = null)
        {
            Replacement = replacement;
            RemoveVersion = removeVersion;
            Migration = migration;
        }
    }
}
```

调用方式：

```csharp
[TransitionalApi("Damage", "3.0.0", "rename the call")]
[Obsolete("Use Damage instead. Removed in 3.0.0. Migration: rename the call.", false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public float Attack
{
    get => Damage;
    set => Damage = value;
}
```

约束：

- **消息格式固定**：`Use {新 API} instead. Removed in {MAJOR}. Migration: {一句话}`。`TransitionalApi` 的三个参数必须与 `Obsolete` 消息逐项一致，由扫描器校验；
- 缺少移除版本的 `[Obsolete]` 或缺少 `[TransitionalApi]`，都视为无效过渡 API；
- `TransitionalApiAttribute` 属于 Core 的公开 API：新增特性类型是 `MINOR`，修改其构造签名是 `MAJOR`；
- 特性标注 `[Conditional("UNITY_EDITOR")]`：Player 构建中编译器会忽略特性应用，不进入运行时程序集；因此 `[TransitionalApi]` **只是 Editor 期元数据，运行时不可读取**，任何运行时逻辑都不得依赖它（`[Obsolete]` 不受影响，Player 中仍生效）；
- **特性源文件必须留在包内**（否则包内代码无法编译），但扫描器、Editor 菜单、报告生成等工具全部放在项目代码 `Assets/Editor/NexusFrameworkValidation/`，不随包发布；
- 统一放在包内 `Obsolete/` 目录或 `#region Obsolete`，便于整体删除与静态扫描；
- 过渡 API **不得调用另一个过渡 API**，不得为新功能提供入口；
- 过渡 API 不得改变线程、时序或异常类型；这类差异属于语义变更，直接 `MAJOR`；
- `[EditorBrowsable(Never)]` 只影响编辑器提示，不改变编译行为；不得用它替代 `[Obsolete]`（使用它需要 `using System.ComponentModel;`）。

#### 5.4.6 维护与清理

- **代码是过渡 API 的唯一真源，README 中的清单纯属生成物**：不再单独维护 `OBSOLETE.md`，也不由人手写维护 README 清单。README 必须包含一个受控区块，由扫描器从 `[TransitionalApi]` 生成并整体替换：

```markdown
<!-- BEGIN TRANSITIONAL APIS（由扫描器生成，勿手改） -->
## Transitional APIs

| Deprecated API | Replacement | Since | Remove in |
|---|---|---|---|
| `CarrierManager.TryGet(ushort, out Carrier)` | `TryGet(int, out Carrier)` | 2.0.0 | 3.0.0 |
<!-- END TRANSITIONAL APIS -->
```

- 清单与代码中的 `[TransitionalApi]` **一一对应**由生成器保证：每次生成都会整体替换受控区块，缺失或多余条目在生成时就报错，正常运行不会出现偏差；
- **每次 `MAJOR` 发布前必须清理到期条目**：`RemoveVersion` ≤ 本次 `MAJOR` 的过渡 API 一律删除，重新生成 README 区块，不得延期；
- 每次 `MINOR` 发布前审查：新增的过渡 API 是否已进入生成的 README 区块（重新生成即可）；是否有条目已到期未删；
- **数量不设硬性上限**，但清单必须全量记录。README 清单的长度本身就是可见性约束：如果它在快速膨胀，说明公开 API 正在反复漂移，应主动冻结设计并集中做一次 `MAJOR`；
- `CHANGELOG.md` 使用 `Deprecated` / `Removed` 段记录引入与删除。

#### 5.4.7 不能使用过渡 API 的情形

以下变更**必须**直接升 `MAJOR`，即使旧签名还能保留：

1. 可观察语义变化：事件时机、执行顺序、并发/线程模型、异常类型；
2. 提升最低 Unity 版本或依赖包 `MAJOR`；
3. 泛型约束、基类、类型层级变化；
4. 安全相关行为变化——宁可断裂并强制迁移，也不静默保持旧行为；
5. 旧行为本身是缺陷，保留它会继续产生错误数据。

#### 5.4.8 与 `0.x` 的关系

> 本项目首个正式版本即 `2.0.0`，本节不适用；规则保留给未来从零开始的新包。

- `0.x` 不强制提供过渡 API；`MINOR` 本身允许断裂，可直接删除旧 API，但应在 CHANGELOG 写清迁移方式；
- 推荐仍用 `[Obsolete]` 标注并至少保留一个 `MINOR`，让下游有机会发现；
- 进入 `1.0.0` 后，过渡 API 规则从“推荐”变为“必须”。

#### 5.4.9 反模式

| 反模式 | 为什么禁止 |
|---|---|
| 过渡 API 没有移除版本，长期沉留 | 兼容层会变成事实上的公开 API，后续更难删除 |
| `[Obsolete("Deprecated")]` 不含替代与迁移方式 | 下游无法自助迁移，等于制造支持负担 |
| 用 `Obsolete(warning)` 掩盖语义变化 | 代码能编译但行为已变，是最危险的“伪兼容” |
| 在 `MINOR` 中使用 `Obsolete(error)` | 版本号承诺兼容，实际编译失败 |
| 过渡 API 没有测试 | 转发逻辑出错不会被发现，旧调用点静默失效 |
| 在过渡 API 中修复 Bug 但新 API 不修 | 新旧行为分叉，迁移后问题复现 |
| README 未记录过渡 API | 下游看不到迁移窗口，台账与代码脱节 |

#### 5.4.10 发布验收

发布前逐项确认：

1. 旧调用样例工程不改代码可编译；
2. 每个过渡 API 有测试，且与新 API 断言一致；
3. README 的过渡 API 区块已由扫描器重新生成，与代码中的 `[TransitionalApi]` 一一对应；
4. 到期条目已在本次 `MAJOR` 删除；
5. `CHANGELOG.md` 的 `Deprecated` / `Removed` 段完整。

---

## 6. 预发布通道

| 后缀 | 用途 | 依赖方可用性 |
|---|---|---|
| `-exp.N` | 实验性 API，可能整体推翻 | 仅实验工程显式引用 |
| `-pre.N` | 功能完成、等待反馈 | 早期接入方显式引用 |
| `-rc.N` | 候选发布，只修阻断问题 | 发布验证 |

规则：

1. 预发布版本**不进入**任何稳定包的 `dependencies`；`gas` 的稳定版不得依赖 `core` 的预发布版。
2. 预发布递增不推进主版本号，例如 `2.1.0-exp.1 → 2.1.0-exp.2 → 2.1.0-rc.1 → 2.1.0`。
3. 转正决策：

   | 转正前情况 | 处理 |
   |---|---|
   | 无破坏性变更 | 去掉后缀，数字不变：`2.1.0-rc.1 → 2.1.0` |
   | 发现破坏性变更、仍要按兼容线发布 | **作废当前预发布号，升 `MINOR` 重走通道**：`2.1.0-exp.N` → `2.2.0-exp.1`；不允许原地改号 |
   | 破坏性变更决定进入新的 `MAJOR` | 升 `MAJOR`：`3.0.0-exp.1` |

4. 预发布之间**不承诺兼容**；只有无后缀的正式版才进入兼容承诺。
5. UPM 默认不会为宿主自动选择预发布版本；引用时必须写完整版本号。

---

## 7. 兼容矩阵

Core 是版本锚点。每次任一包发布，更新下表（以发布时的实际状态为准）：

| GAS 版本 | 依赖 Core（最低） | 已验证 Core 范围 | Unity 最低 | 备注 |
|---|---|---|---|---|
| `2.0.0` | `2.0.0` | `2.0.x` | 2022.3 | 待首次发布 |

维护规则：

- “已验证范围”只写**实际跑过测试**的版本组合，不写推测。
- 若 GAS 新版依赖 Core 新 API，最低版本同步提升；若只是修复，不提升。
- **矩阵只维护当前受支持窗口**（最新 `MINOR` + 上一条 `MINOR`，见下方兼容窗口）；更老的行移入 CHANGELOG 的发布历史，避免矩阵无限膨胀。
- 兼容矩阵与 CHANGELOG 不一致时，以兼容矩阵为准并立即修正 CHANGELOG。

兼容窗口：

- 只对最新 `MINOR` 提供 `PATCH` 修复；
- 上一条 `MINOR` 只接受“无 workaround 的严重问题”修复，且最多一个 `PATCH`；
- 更老的版本不再支持，升级路径写进 CHANGELOG。

---

## 8. 发布流程

### 8.1 发布前检查清单

1. 工作区干净，EditMode / PlayMode 测试通过。
2. `package.json` 的 `version` 已按 §5 递增，且为合法 SemVer。
3. `CHANGELOG.md` 顶部有该版本的条目（格式见 §8.3）。
4. `dependencies` 中的每个 `com.nexusframework.*` 版本都存在对应 tag。
5. 包内没有引用包外代码；新增 `.cs`/`.asmdef` 的 `.meta` 已提交。
6. 在**干净工程**中通过 git URL 安装并编译通过，且覆盖三种版本组合（见 §8.2）：宿主 Core 版本 **=** / **>** / **<** GAS 声明的 Core 版本，分别记录实际行为；其中 `<` 必须为硬失败并给出明确报错。
7. 兼容矩阵已更新。
8. 过渡 API 验收通过：旧调用样例可编译、转发有测试、README 区块由扫描器生成且与 `[TransitionalApi]` 一致、到期条目已删除（§5.4.10）。
9. README 影响分析已完成：按 §3.4.1 更新，或在发布记录中标记 `README: no change required`。
10. 若存在 `Documentation~/`：`index.md` 存在，安装说明与当前版本一致；本次为 `MAJOR` 时已补迁移指南。
11. 若存在 `Samples~/`：每个示例都在 `package.json` 的 `samples` 中声明，且能在干净工程导入并编译。
12. 打包/发布后再检查一次 `package.json` 未被误改。

### 8.2 引用与发布形式

对外引用（宿主工程 `Packages/manifest.json`）：

```json
{
  "dependencies": {
    "com.nexusframework.core": "https://github.com/FengLiuli/NexusFramework.git?path=/Assets/NexusFramework#core-v2.2.0",
    "com.nexusframework.gas": "https://github.com/FengLiuli/NexusFramework.git?path=/Assets/NexusFramework.GAS#gas-v2.2.0"
  }
}
```

说明：

- `#<revision>` 支持 tag、分支或 commit；**发布引用必须用 tag**，不用分支。
- 两个包共用仓库，因此 tag 必须带包前缀，否则无法区分该 tag 对应哪个包的哪个版本。
- 本地该仓库自身是“源码内嵌”形态，不经过 UPM 解析；因此本地编译通过**不能**证明发布包自洽，§8.1 第 6 项不可省略。
- **git 依赖不会自动解析同仓库的另一个包**：UPM 无法从 `?path=` 推断 `com.nexusframework.core` 也来自同一仓库。宿主必须把每个 Nexus 包都显式写成 git 依赖，否则 GAS 声明的 `com.nexusframework.core` 会被当作 registry 包解析并报找不到。若未来需要“安装 GAS 自动带上 Core”，只能发布到 scoped registry，或提供 package 聚合包（不推荐）。
- **宿主的 Core tag 必须 ≥ GAS 声明的 Core 版本**：git 模式下版本由 `#<tag>` 决定，GAS `package.json` 里的 semver 只是文档声明，不参与自动解析；Unity 采用“直接依赖优先（direct dependency wins）”，宿主显式写的 Core 会覆盖 GAS 的间接要求。若宿主写低了，GAS 在用到新 API 时会编译失败，且 UPM 不会自动纠正。
- 宿主切换 tag 后，`Packages/packages-lock.json` 会记录新的 revision；提交锁文件，避免团队内解析到不同提交。
- 发布 tag 以 **git 仓库**为准（`origin`：`github.com/FengLiuli/NexusFramework`）。其他本地版本控制（如历史遗留的 Plastic 工作区）只作本地镜像，不作为发布标识。

#### git URL 与 scoped registry 的取舍

**scoped registry 是什么**：UPM 的包源机制。在宿主 `Packages/manifest.json` 中为某个包名前缀（scope）指定一个 npm 风格的 registry 地址，之后该前缀下的包就可以像官方包一样用版本号声明：

```json
{
  "scopedRegistries": [
    {
      "name": "NexusFramework",
      "url": "https://<registry-host>/",
      "scopes": ["com.nexusframework"]
    }
  ],
  "dependencies": {
    "com.nexusframework.core": "2.2.0",
    "com.nexusframework.gas": "2.2.0"
  }
}
```

它和 git URL 的本质区别是：**版本号解析与依赖传递由 registry 完成**，而不是由 URL 字符串表达。

| 维度 | git URL + tag（当前） | scoped registry |
|---|---|---|
| 搭建与维护 | 零成本，推 tag 即可 | 需要 registry 服务、发布流水线、认证配置 |
| 依赖传递 | 不支持，宿主必须显式列出每个包 | 支持，GAS 声明 Core 后自动解析 |
| 版本写法 | URL 里带 tag，冗长 | 只写版本号 |
| 访问控制 | 跟随仓库权限 | 由 registry 控制，可只给部分人 |
| 版本不可变 | 依赖“不移动 tag”的纪律 | 已发布版本通常不可覆盖 |
| 适用规模 | 包少、接入方少 | 包多、接入方多、需要对外分发 |

**决策：不使用 scoped registry / npm registry。** 两个包、少量接入工程，git URL + tag 足够；分发只走 GitHub 仓库。以下是未来若规模变化时重新评估的条件，当前不建设：

1. Nexus 包数量达到 4 个以上；
2. 需要接入的工程达到 3 个以上，或接入方无法访问本仓库；
3. “宿主忘记声明 Core 导致解析失败”开始反复出现；
4. 需要按包做权限控制或对外分发。

**实现选型注意**：GitHub Packages 的 npm registry 要求包名带 `@owner/` 前缀，与 UPM 的 `com.nexusframework.*` 包名不兼容，不能直接使用；可选自建 Verdaccio、Cloudsmith，或 OpenUPM（适合公开包）。选型时必须确认 registry 接受 `com.*` 形式的包名。

### 8.3 CHANGELOG

每个包根目录维护 `CHANGELOG.md`，只记录该包的变更：

```markdown
# Changelog

## [2.1.0] - 2026-11-19
### Added
- Identity 子系统：KeyIndex / IdAllocator / TypedAllocator

### Changed
- CarrierManager 类型 id 改由 KeyIndex 分配（不兼容：见 3.0.0）

### Fixed
- 修复 Trait 反序列化顺序导致的状态丢失

## [2.0.0] - 2026-10-01
### Added
- 初始版本
```

规则：

- 条目按版本倒序；未发布变更先写在 `## [Unreleased]` 下，发布时改成版本号与日期。
- 只写“对使用者有影响”的内容；内部重构最多一行。
- 破坏性变更必须写明**迁移方式**。

### 8.4 Tag 与 Release

- 轻量 tag 或附注 tag 均可，命名固定为 `<package>-v<version>`：
  - `core-v2.2.0`
  - `gas-v2.2.0`
  - `gas-v2.2.0-rc.1`
- 同一批兼容发布可以共用同一提交，打两个 tag。
- GitHub Release 标题使用 tag 名，正文复用对应 CHANGELOG 段落。
- 不移动、不删除已发布 tag；发布错误只能通过新版本修正。

---

## 9. 版本一致性自动化

`package.json` 是唯一真源。校验器与扫描器放在**项目代码**，不进入任何发布包：

- 位置：`Assets/Editor/NexusFrameworkValidation/`，独立 Editor-only asmdef，`autoReferenced: false`；
- 入口：`Tools/NexusFramework/Validate Packages` 菜单；同一套检查逻辑可由 CI 复用；
- 包内只保留运行时代码与 `TransitionalApiAttribute` 的定义；扫描器、报告生成、菜单等工具代码一律放在项目代码中。

检查项：

1. `Assets/*/package.json` 的 `name` 唯一、`version` 为合法 SemVer；
2. 每个包的 `CHANGELOG.md` 包含当前版本条目；
3. `dependencies` 中的每个 `com.nexusframework.*` 版本都能在兼容矩阵中查到对应组合（不能简单比较大小：Core 的 `MAJOR` 可能高于 GAS）；宿主工程显式声明的 Core 版本必须 ≥ 依赖方声明的最低版本（§8.2）；
4. 包内 asmdef 的 `references` 不指向包外程序集（允许 Unity 官方包与声明依赖）；
5. 当前 `HEAD` 若带 `core-v*` / `gas-v*` tag，则对应包的 `package.json` 版本与 tag 一致；
6. 运行时暴露只读版本常量（如 `NexusFrameworkInfo.PackageVersion`）：**从 `package.json` 生成**常量文件（字符串形式），由校验器确保与 `package.json` 一致，运行时与 Editor 都读取该常量。**不要**手写维护两份版本号；
7. 过渡 API（§5.4）：扫描包内 `[TransitionalApi]` 与 `[Obsolete]`，检查两者同时存在、参数与消息一致；重新生成 README 区块并确认与代码一一对应；`RemoveVersion` ≤ 当前 `MAJOR` 的条目是否已删除；`MINOR` / `PATCH` 版本中不得出现 `[Obsolete(..., true)]`；
8. 每个包根目录存在 `README.md` 与 `CHANGELOG.md`；
9. `Samples~/` 下每个一级子目录与 `package.json` 的 `samples[].path` 一一对应，不多不少；
10. 若存在 `Documentation~/`，必须存在 `index.md`。

同一版本号会出现在以下位置，发布时必须同步：`package.json`、包内 `CHANGELOG.md`、git tag、GitHub Release 标题、兼容矩阵；宿主侧还会写进 `Packages/packages-lock.json`（由 UPM 自动维护，不要手改）。

自动化不是首次发布的前置条件，但应在包数量超过 2 个之前落地。

---

## 10. 落地清单

按顺序执行：

1. **确认首个正式版本**：当前未提交状态定为首发 `2.0.0`（§5.2）；发布时把两个 `package.json` 的 `version` 从占位的 `1.0.0` 改为 `2.0.0`，打 `core-v2.0.0` / `gas-v2.0.0` tag 后严格按规则递增。
2. **补齐 GAS 依赖**：在 `Assets/NexusFramework.GAS/package.json` 增加 `com.nexusframework.core`（§3.3）。
3. **改 asmdef 引用**：`NexusFramework.GAS.asmdef` 的 Core 引用从 GUID 改为名称 `"NexusFramework"`（§3.2）。
4. **建立 CHANGELOG**：两个包各新增 `CHANGELOG.md` 与初始条目（§8.3）。
5. **建立包 README**：两个包各新增 `README.md`，按 §3.4.1 的最小结构写（安装、依赖、最小示例、链接、过渡 API 清单）。
6. **建立过渡 API 基础设施**：Core 新增 `Assets/NexusFramework/Annotations/TransitionalApiAttribute.cs`；在 README 建立空的过渡 API 清单；在 `Assets/Editor/NexusFrameworkValidation/` 实现扫描器骨架（不进包）。
7. **补包元数据**：为两个 `package.json` 增加 `documentationUrl`、`changelogUrl`（可选 `licensesUrl`），保持描述准确。
8. **按需创建文档与示例**：`Documentation~/`、`Samples~/` 按 §3.4 在包根独立创建，不作为首批发布的强制项。
9. **首次发布**：执行 §8.1 检查，打 `core-v*` / `gas-v*` tag，创建 Release，更新兼容矩阵。
10. **落地完整校验器**：在包数量增长或首次被迫手工核对版本时补全 §9 的全部检查项。
