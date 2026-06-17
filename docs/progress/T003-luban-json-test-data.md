---
task: T003
task_status: 进行中（实现运行时 ASC 创建管道）
created: 2026-06-14
updated: 2026-06-16
sessions: 1
---

# 进度记录：Luban JSON 测试数据创建

> **追加不覆盖**：每次会话在末尾追加新条目，不修改已有条目。

---

## [2026-06-14 19:18] 会话 #1 — 创建 10 个 GAS Luban JSON 测试数据文件

### 背景

用户需求："你可不可以修改表格数据，写一些合适的测试数据验证gas模块的功能完整性"。
需要创建 10 个 Luban JSON 文件，覆盖 GAS 全部模块的功能测试。

### 已完成工作

#### 1. 文档调研
- 读取了 10 个 TableClass 生成文件（`asc.cs`, `ability.cs`, `gameplayEffect.cs`, `gameplayCue.cs`, `mmc.cs`, `timelineAbility.cs`, `attribute.cs`, `attributeSet.cs`, `gameplayTags.cs`, `unit.cs`）
- 梳理了所有 struct 类型（`TagRequirementData`, `Modifier`, `Duration`, `Period`, `GrantedAbility`, `Stacking`, `AttributeInSet`, `Track`, `TaskClip`）
- 确认了多态继承体系：AbilityLogicBase(11 种)、GameplayCueBase(5 种)、ModMagnificationCalculationBase(3 种)、AbilityTaskBase(6 种)、TargetCatcherBase(3 种)
- 确认 XParam 嵌套多态（18 种 XParam 类型）
- 确认 gameplayTags 使用 `_buf["id"]`（小写）而其他表用 `_buf["ID"]`（大写）
- 确认 unit 表在 Tables.cs 中的加载文件名为 `tbunit.json`（无 exgas_ 前缀）
- 确认 Duration/Period/Stacking/TagRequirementData 可为 null（JSON 中可省略或 null）
- 确认 Modifier/GrantedAbility/AttributeInSet 为数组（JSON 中必须提供）

#### 2. 已创建的 JSON 文件

| # | 文件 | 条目 | 关键特性 |
|---|------|------|---------|
| 1 | `exgas_tbgameplaytags.json` | 10 | 标签层级覆盖 Effect/Damage/State/Ability/Gameplay 命名空间 |
| 2 | `exgas_tbattribute.json` | 5 | Health, Mana, Attack, Defense, Speed |
| 3 | `exgas_tbattributeset.json` | 2 | Warrior(Attack+Defense), Mage(Health+Mana+Attack) |
| 4 | `exgas_tbmmc.json` | 3 | MMCNone, MMCAttributeBased(Attack K=1), MMCScalableFloat(K=1.5 B=10) |
| 5 | `exgas_tbgameplayeffect.json` | 8 | 瞬时/持续/周期/堆叠/授予技能/标签需求/免疫/移除效果 |
| 6 | `exgas_tbability.json` | 3 | ALAApplyEffect x2 + ALDebugLog + 冷却/消耗/标签需求 |
| 7 | `exgas_tbgameplaycue.json` | 3 | CuePlaySound + CueLog + CueLogging(带 RequiredTag/ImmunityTag) |
| 8 | `exgas_tbasc.json` | 2 | PlayerASC(多属性集+多技能), EnemyASC |
| 9 | `exgas_tbtimelineability.json` | 1 | 2 Tracks x 6 Clips, 覆盖 TaskApplyEffects+CatchSelf, TaskPlayCue+XParamCue, TaskDebug, TaskDoCooldown, TaskDoCost, TaskDoNothing |
| 10 | `tbunit.json` | 2 | Player+Enemy, 关联 ASC |

#### 3. 数据验证结果
- 通过 `execute_code` 运行 `new cfg.Tables(...)` 加载所有 10 个 JSON 文件
- **全部加载成功**，无反序列化错误
- 所有多态 `$type` 正确解析：`MMCNone`, `MMCAttributeBased`, `MMCScalableFloat`, `ALApplyEffect`, `ALDebugLog`, `CuePlaySound`, `CueLog`, `CueLogging`, `TaskApplyEffects`, `TaskPlayCue`, `TaskDebug`, `TaskDoCooldown`, `TaskDoCost`, `TaskDoNothing`, `CatchSelf`, `XParamEffectIDs`, `XParamString`, `XParamApplyEffects`, `XParamCue`, `XParamPlaySound`, `XParamLogging`, `XParamNone`, `XParamCatchAreaBox3D`（在 Timeline 的 TargetCatcher 中使用过—实际用了 CatchSelf+XParamNone）

#### 4. 文档更新
- 更新了 `docs/tests/T003-luban-config-loader-generator.md`，添加"测试数据"章节和 MAP 用例对应关系表

### 待做事项

- **执行 MAP-01 ~ MAP-07 测试用例**：编写运行时代码，加载 JSON 数据后调用 `LubanConfigLoader.GetEffectConfig()` 等方法验证字段映射正确性
- **修正漏掉的 `_tables` null 保护**：`GetMmcConfig()` 和 `GetAscConfig()` 缺少 null 检查（运行时可能 NPE）

### 设计决策记录

1. **Cost/CdEffect 引用**：使用 `Cost=8` 引用 `GE_CostMana`（ID=8），`CdEffect=1` 引用 `GE_InstantDamage`（ID=1）。注意 Ability 的 Cost 和 CdEffect 字段是 int 而非 Effect ID 列表，因此表现为消耗效果和冷却效果的 ID 引用。
2. **标签 ID 分配**：1-10 跨多种语义域（Effect.Buff=1, Effect.Debuff=2, Effect.Debuff.Fire=3, Ability.Fire=4, State.Stun=5, Damage.Physical=6, Damage.Magic=7, Gameplay.Immune=8, Effect.Buff.AttackUp=9, Effect.Debuff.DOT=10），避免重叠的 ID。
3. **Timeline 不使用 TimelineAbilityLogic**：TimelineAbility 有单独的表格 `tbtimelineability`，不通过 AbilityLogic 多态体系。

---

## [2026-06-16] 会话 #2 — 实现运行时从 Luban 配置创建 ASC 对象

### 背景

用户提出"运行时新建 ASC 对象"的需求。现有 `CreateGASCarrier(type, go)` 只搭 ECS 骨架，等级/标签/属性集/技能需要调用方手动填充。通过讨论确定走 `IConfigLoader` 标准管道。

### 已完成工作

#### 1. 文档链
- 创建需求文档 `docs/requirements/R004-runtime-asc-creation-from-luban.md`（审核通过）
- 更新设计文档 `docs/design/D003-luban-config-bridge.md`（新增"运行时 ASC 创建"章节）
- 更新编码文档 `docs/code/gas-config.md`（新增 ASC 运行时创建章节）
- 更新测试文档 `docs/tests/T003-luban-config-loader-generator.md`（新增 ASC 配置加载 / 运行时创建测试组）

#### 2. 代码实现

| 文件 | 改动 |
|------|------|
| `Config/IConfigLoader.cs` | + `AscConfigData` / `AttrSetDef` / `AttrInitDef` 三个 struct；+ `GetAscConfig` / `GetAttrSetDef` 接口方法 |
| `Models/ConfigModel.cs` | + ASC/属性集缓存的 `Register*` / `Get*` / `Load*` 方法 |
| `GASArchitecture.cs` | + `CreateGASCarrier(type, ascId, go)` 重载：自动完成等级/标签/属性集初始化/技能授予 |
| `Config/JsonConfigLoader.cs` | + `GetAscConfig` / `GetAttrSetDef` 占位实现（返回 null） |
| `Editor/CodeGen/LubanConfigLoaderGenerator.cs` | + `WriteAscConfigMethod` / `WriteAttrSetDefMethod` 生成显式接口实现 |

#### 3. 关键设计决策

1. **走 `IConfigLoader` 标准管道** — ASC 配置与 Effect/Ability 配置走同一条路，不引入额外耦合
2. **保留 `ConfigModel` 缓存层** — 保持一致性，为 JsonConfigLoader 等非全量加载场景预留扩展点
3. **直接使用 Luban 的 `AttributeInSet` 数据** — 属性初始值/Clamp 已在 `cfg.AttributeInSet` 中定义，无需另建类型
4. **`CreateGASCarrier(type, ascId, go)` 重载而非默认参数** — 避免 `go=null` 时误传 ascId

### 待做事项

- **Unity Editor 编译验证**：确认 GASArchitecture.cs、IConfigLoader.cs、ConfigModel.cs 编译通过
- **重新生成 LubanConfigLoader**：在 Editor 中执行 `NF.GAS/Generate/LubanConfigLoader` 确认新方法正确生成
- **验证生成器输出编译**：确认 LubanConfigLoader.cs 编译通过
- **执行 MAP-08 ~ MAP-11 测试用例**：验证 ASC 配置加载和运行时创建的正确性
