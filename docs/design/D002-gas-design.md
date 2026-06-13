---
layer: design
status: draft
task: T001
created: 2026-06-11
updated: 2026-06-11
---

# 设计：NexusFramework.GAS 游戏能力系统

## 方案概述

NexusFramework.GAS（Gameplay Ability System）解决 Unity 中技能、Buff、属性系统的常见问题：状态同步困难、效果叠加逻辑混乱、视觉反馈与逻辑耦合。

设计目标：
- **ECS 驱动**：基于 Unity Entities 1.0，利用组件化 + System 管线处理游戏效果
- **可配置化**：所有技能、效果、Cue 通过配置（JSON）驱动，逻辑代码与数据分离
- **参考 UE GAS 概念**：Ability / GameplayEffect / GameplayTag / GameplayCue / Attribute 映射到 ECS 实现
- **架构桥接**：通过 `CarrierId ⇔ Entity` 映射，桥接上层 `NexusFramework` 数据载体和底层 ECS

## 架构设计

### 整体架构

```plaintext
NexusFramework (上一层)
   │
   └── CarrierManager → CarrierId
                              │
GASArchitecture               │
   ├── GASEntityMapModel ─────┤ mapping: CarrierId ↔ Entity
   │                           │
   ├── ConfigModel ────────────┤ config: JSON → Component 配置库
   │
   ├── Services ───────────────┤ API 入口
   │   ├── WorldService ───────┼── ECS World 生命周期
   │   ├── TagService ─────────┼── 标签增删查
   │   ├── EffectService ──────┼── 效果施加
   │   ├── AbilityService ─────┼── 能力授权/激活/取消
   │   ├── CueService ─────────┼── Cue 播放/停止
   │   ├── AttributeService ───┼── 属性读写
   │   ├── TimerService ───────┼── 定时器
   │   └── EventBridgeService ─┼── 事件桥接
   │
   └── ECS World (副 World)
        └── Entity → [动态/静态组件]
              ├── Static (初始化时配置)
              │   ├── BAbility (能力清单 Buffer)
              │   ├── BGameplayEffect (效果清单 Buffer)
              │   ├── BFixedTag (固定标签 Buffer)
              │   └── BEAttrSet (属性集 Buffer, 含 NativeList)
              │
              └── Dynamic (运行时添加/移除)
                    ├── CAbilityActive / CAbilityInTryActivate
                    ├── CEffectInUsage / CEffectDestroy / CEffectApplied
                    ├── CAttributeIsDirty
                    └── BTemporaryTag (临时标签 Buffer)
```

### GameplayEffect 状态机

这是 GAS 最核心的设计——一个 GameplayEffect 经历 7 个阶段，每个阶段由独立的 System 驱动：

```plaintext
Request (ApplyEffect)
    │
    ▼
┌──────────────────────────────────────────────────────────┐
│ ① CREATE                                               │
│    SInstantiateEffect                                    │
│    创建 GE Entity + 挂载静态组件 + 添加 WipInstantiateEffect│
└──────────────────────────────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────────────────────────────┐
│ ② CHECK APPLY                                          │
│    SCheckApplicationRequiredTags → 检查 ApplicationRequired │
│    SCheckImmunityTags → 检查免疫标签                      │
│    SCheckApplyEnd → 任一失败 → 添加 WipRemoveEffect       │
└──────────────────────────────────────────────────────────┘
    │
    ├─── 即时效果 (Instant) ───────────────────┐
    │                                          │
    ▼                                          │
┌──────────────────┐    ┌──────────────────────────────────┐
│ ③ APPLY         │    │ ③ APPLY                         │
│ SPlayCueOnApply  │    │ SAddEffectToAscBuffList (加入 ASC)│
│ SApplyEnd        │    │ SPlayCueOnAdd                    │
│ (标记 Apply 完成)│    └──────────────────────────────────┘
└──────────────────┘              │
    │                             ▼
    ▼                  ┌─────────────────────────────────────┐
┌──────────────────┐    │ ④ ACTIVATE                         │
│ 即时效果分支      │    │ SAddModifiers → 应用 Modifier      │
│                  │    │ SEffectAddGrantedTags → 赋予标签     │
│ SExecuteModifiers │   │ SAddGrantedAbility → 授权技能       │
│ → 执行修饰器      │    │ SPlayCueOnActivate → 播放激活 Cue  │
│ → 触发回调        │    │ SSetEffectActive → 标记 Active     │
│ → 进入 DESTROY    │    │ SPlayCueOnTick → 启动周期 Cue     │
└──────────────────┘    └─────────────────────────────────────┘
                               │
                          ┌────┴────┐
                          ▼         ▼
                   ┌──────────┐  ┌──────────┐
                   │ ⑤ TICK   │  │⑥ DEACTIVATE│
                   │ Duration  │  │ Duration  │
                   │ 计时到期  │  │ 结束      │
                   │ Period    │  │ SRemoveModifiers│
                   │ 周期触发  │  │ SRemoveGranted  │
                   │ Stacking  │  │ Tags/Ability    │
                   │ 维护      │  │ SPlayCueOnDeact │
                   └──────────┘  │ SSetEffectDeact │
                        │        └────────────────┘
                        ▼              │
                   ┌──────────┐        │
                   │ ⑦ REMOVE │◄───────┘
                   │ SRemoveFrom       │
                   │  ASC Buff List    │
                   │ SPlayCueOnRemove  │
                   │ Destroy Entity    │
                   └──────────┘
```

### 堆叠策略

```plaintext
ApplyEffect(configId, target, source)
    │
    ├── CStacking.StackingType == None
    │   → 每次创建独立 GE Entity
    │
    ├── CStacking.StackingType == AggregateByTarget
    │   → 按 (configId, targetId) 聚合，StackCount++
    │
    └── CStacking.StackingType == AggregateBySource
        → 按 (configId, sourceId) 聚合，StackCount++

StackCount ≥ CStacking.LimitCount → 新施加被拒绝 (denyOverflow)
```

### 标签系统

```plaintext
GameplayTag (struct)
  ├── Code: int
  ├── Parents: int[] (父标签数组，运行时不使用，仅用于配置时构造层级)
  ├── Children: int[]
  └── HasTag(code): 检查自身 code 或任意 parent code 是否匹配

ECS 存储:
  ├── BFixedTag: DynamicBuffer<BFixedTag> (永久标签，初始化添加)
  └── BTemporaryTag: DynamicBuffer<BTemporaryTag> (临时标签，GE 授予)

全局索引:
  └── SingletonGameplayTagMap: NativeHashMap<int, ComGameplayTag>
      用于运行时快速查询标签层级关系和父/子遍历
```

标签在 GE 管线中的作用：

| 标签类型 | 检查阶段 | 说明 |
|---------|---------|------|
| ApplicationRequiredTags | CheckApply | 目标必须有这些标签 |
| ApplicationImmunityTags | CheckApply | 目标有任一免疫标签 → GE 被拒绝 |
| OngoingRequiredTags | Tick | 持续期间目标必须保持这些标签 |
| CEffectGrantedTags | Activate | GE 激活后授予目标的临时标签 |
| CRemoveEffectWithTags | Apply | 目标有这些标签 → GE 触发移除 |

### Ability 生命周期

```plaintext
GrantAbility(carrier, abilityCode)
    │
    ▼
BAbility Buffer (挂载在 Owner Entity 上)
    │
    ▼
TryActivate(carrier, abilityCode)
    │
    ├── 检查: 是否已有同 Code 的 CAbilityActive? → 拒绝
    ├── 检查: Owner 是否含有 CBlockAbilityWithTags? → 拒绝
    ├── 检查: 冷却中? → 拒绝
    ├── 检查: 消耗是否足够? → 拒绝
    │
    └── 成功 → 添加 CAbilityInTryActivate
                │
                ▼ STryActivateAbility System
                CAbilityActive (标记激活)
                │
                ├── STryEndAbility → CAbilityInTryEnd → 移除 Active
                └── STryCancelAbility → CAbilityInTryCancel → 移除 Active
```

### MMC 修饰器强度计算

```plaintext
ModMagnitudeCalculationBase (抽象)
  ├── MMCNone          ────── 固定值 (直接使用 Magnitude)
  ├── MMCScalableFloat ────── 可缩放: BaseValue × (1 + Level * Scalable)
  ├── MMCAttributeBased ──── 基于属性: 捕获 Source/Target 属性值 + 系数
  └── MmcParaFloatScale ──── 参数浮点缩放: 基于 XParam 参数值

每次属性重算时, 遍历目标所有活跃 GE 的 MCModifiers Buffer:
  ┌────────────────────────────────────────────┐
  │ 属性 CurrentValue = BaseValue             │
  │   + Σ(Add 型 Modifier.Magnitude)          │
  │   × Π(Multiply 型 Modifier.Magnitude)     │
  │   = 最终 CurrentValue                     │
  └────────────────────────────────────────────┘
  Dirty 标记 + SUpdateAttributeCurrentValue System 触发重算
```

### Cue 管线

```plaintext
GE 配置中的 CCueOnActivate / CCueOnAdd / CCueOnTick / CCueOnDeactivate / CCueOnRemove
    │
    ▼ 触发
GameplayCueConfig → CueHelper.TryCreateCue → GameplayCueBase
    │
    ├── 创建 ECS Cue Entity
    │   ├── ECCuePlayable (enable/disable 控制播放/停止)
    │   ├── ECCuePlaying (标记正在播放)
    │   ├── ECKillCue (标记需要销毁)
    │   └── MCCue (Cue 类型和参数配置 Buffer)
    │
    ├── SCueStart → ECCuePlayable true → ECCuePlaying true
    ├── SCueTick → 更新 Cue 状态
    ├── SCueEnd → ECCuePlayable false → ECCuePlaying false
    └── SCueDestroy → ECKillCue true → 销毁 Entity

Cue 具体类型:
  ├── CueLog: Debug.Log
  ├── CueMountPrefab: 实例化 Prefab (挂载到目标 GameObject)
  ├── CuePlayAnimator: 播放 Animator 动画
  └── CuePlaySound: 播放 AudioSource
```

## ECS 系统组拓扑

```
PlayerLoop
  ├── InitializationSystemGroup
  ├── SimulationSystemGroup
  │   ├── FixedStepSimulationSystemGroup (FixedRateSimpleManager)
  │   │   └── SGLogic (ComponentSystemGroup)
  │   │       ├── SGlobalTimer
  │   │       ├── SGAbility → STryActivate / STryCancel / STryEnd / SAbilityTick
  │   │       ├── SGAttribute → SUpdateAttributeCurrentValue
  │   │       └── SGEffect
  │   │           ├── SGEffectCreate → SGInstantiateEffect
  │   │           ├── SGEffectOperation
  │   │           │   ├── SGCheckApplyEffect
  │   │           │   ├── SGApplyEffect (含 SGInstantEffect + SGDurationalEffect)
  │   │           │   ├── SGCheckActivateEffect
  │   │           │   ├── SGActivateEffect
  │   │           │   ├── SGDeactivateEffect
  │   │           │   └── SGRemoveEffect
  │   │           ├── SGEffectDestroy → SDestroyEffects
  │   │           └── SGEffectTick → SGRunningEffect
  │   │
  │   └── PresentationSystemGroup
  │       └── SysGrpDisplay → SCueStart / SCueTick / SCueEnd / SCueDestroy / SEventForwarder
  │
  └── PresentationSystemGroup (Unity 原生)
```

注意：SGLogic 放在 `PresentationSystemGroup` 的子组而不是 `FixedStepSimulationSystemGroup`，这意味这 GAS 管线与帧率同步而非固定时间步。这是当前代码的选择——所有 System 在 `SGLogic` 组中通过手动 `_world.Update()` 推进，与固定时间步解耦。

## 配置系统

```
IConfigLoader (接口)
  ├── JsonConfigLoader (实现)
  │   从 JSON 文件读取配置 → 解析为 GameplayEffectComponentConfig[] / AbilityComponentConfig[] / GameplayCueConfig[]
  │
  └── MockConfigLoader (测试用)
      代码中直接填充 ConfigModel

ConfigModel
  ├── EffectConfigs: Dictionary<int, GameplayEffectComponentConfig[]>
  ├── AbilityConfigs: Dictionary<int, AbilityComponentConfig[]>
  └── CueConfigs: Dictionary<int, GameplayCueConfig[]>

GameplayEffectComponentConfig → GEConfigHelper.CreateGameplayEffectEntity → 挂载 ECS 组件
AbilityComponentConfig → 授权时创建 Ability Entity + 挂载组件
GameplayCueConfig → CueHelper.TryCreateCue → 创建 Cue Entity
```

### XParam 泛型配置

`XParam` 系统解决同一 AbilityLogic 类型需要不同参数的问题：

```plaintext
ALApplyEffect (AbilityLogic)
  └── XParamApplyEffects (XParam 子类)
        ├── effectIDs: int[] (要应用的效果 ID 列表)
        └── targetStrategy: enum (施法者/目标/区域...)
```

通过 `BeanPolymorphicFieldAttribute` 标记，实现 JSON → XParam 多态反序列化。

## 替代方案

| 方案 | 优点 | 缺点 | 弃用/选用理由 |
|------|------|------|-------------|
| **Unreal GAS 原生** | 功能完整、经过验证 | C++ 依赖、不能直接用于 Unity | 不可用 |
| **纯 MonoBehaviour GAS** | 实现简单、调试方便 | 性能差、复杂效果难以管理、无 ECS 优势 | 弃用：ECS 方案提供更好的性能和架构 |
| **PlayMaker / 可视化脚本** | 无需编码、设计师友好 | 版本控制困难、复杂逻辑难以维护 | 弃用 |
| **NexusFramework.GAS (当前)** | ECS 驱动、配置化、UE GAS 概念映射、与 NexusFramework 集成 | 开发工作量大、需熟悉 ECS | **选用**：适合需要完整 GAS 的中大型 Unity 项目 |

## 风险与权衡

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| ECS Entities 1.0 的学习曲线 | 新开发者需要理解 ECS 概念和 Best Practice | 编码文档提供足够的指引 |
| World Update 手动调用而非挂载到 PlayerLoop | 忘记调 `_world.Update()` 会导致管线不执行 | 通过 WorldService 统一管理 |
| NativeArray/NativeHashMap 回收 | 内存泄漏风险（已在测试中发现） | Dispose 检查、MemorySafetyTests 覆盖 |
| 配置 JSON 与代码 Component 字段同步 | 配置字段改名容易导致运行时静默失败 | ConfigLoader 加载时做字段校验 |
| AbilityLogic 和 TargetCatcher 可扩展性 | 新逻辑需要继承基类，理解工厂模式 | 通过 AbilityLogicFactory + XParam 降低扩展门槛 |
| 多层 System 组嵌套导致调试困难 | 一次 GE 流程经过 20+ System | 每个 System 职责单一，通过 Log 可追踪 |

## 关联

- 核心框架：[核心框架设计文档](D001-core-framework.md)
- 编码实现：[GAS 编码文档](../code/nexusframework-gas.md)
- 测试覆盖：[测试计划](../tests/T001-gas-test-suite.md)
