---
layer: code
status: draft
task: T001
source_files:
  - Assets/NexusFramework.GAS/GASArchitecture.cs
  - Assets/NexusFramework.GAS/Services/WorldService.cs
  - Assets/NexusFramework.GAS/Services/TimerService.cs
  - Assets/NexusFramework.GAS/Services/EventBridgeService.cs
  - Assets/NexusFramework.GAS/Models/GASEntityMapModel.cs
  - Assets/NexusFramework.GAS/Models/ConfigModel.cs
created: 2026-06-11
updated: 2026-06-12
---

# 编码文档：GAS 架构与服务层

## 概述

NexusFramework.GAS 以 `GASArchitecture`（继承自 `Architecture`）为入口，在 `OnInit()` 中自动注册 8 个 Service 和 2 个 Model，托管一个独立的 ECS World 来运行所有 GAS 管线。

核心流程：**Ability（技能）→ GameplayEffect（效果）→ Modifier（属性修饰器）+ Cue（视觉反馈）**

## 架构总览

```
GASArchitecture (继承自 Architecture)
    │
    ├── Services（服务层）
    │   ├── WorldService     — 托管 ECS World 和 EntityManager
    │   ├── TimerService     — 全局定时器
    │   ├── EventBridgeService — ECS ↔ 框架 事件桥接
    │   ├── TagService       — 标签管理
    │   ├── EffectService    — GameplayEffect 生命周期
    │   ├── AbilityService   — Ability 生命周期
    │   ├── CueService       — GameplayCue 生命周期
    │   └── AttributeService — 属性管理
    │
    ├── Models
    │   ├── GASEntityMapModel — CarrierId ↔ Entity ↔ GameObject 三向映射
    │   └── ConfigModel       — GAS 配置数据缓存
    │
    └── ECS Systems Pipeline
        └── SGLogic (ComponentSystemGroup)
            ├── SGlobalTimer
            ├── SGAbility     → 技能激活/取消/结束 + Tick
            ├── SGAttribute   → 属性值更新
            └── SGEffect      → 效果全生命周期
```

## 关键类

### `GASArchitecture`

**职责**：GAS 系统架构基类，统一注册 ECS 运行时所需的所有服务和模型。

**关键方法**：

| 方法 | 说明 |
|------|------|
| `CreateGASCarrier(typeName, GameObject?)` | 创建 Carrier 并绑定 ECS Entity，初始化所有必备组件 |
| `DestroyGASCarrier(CarrierId)` | 销毁 Carrier，清理 ECS Entity 和 AttributeSet 的 NativeContainer |
| `BindGameObjectForCarrier(CarrierId, GameObject)` | 将 GameObject 绑定到已有 Carrier 的 Entity |

### `WorldService`

**职责**：ECS World 生命周期管理。负责创建 World、注册所有系统组、将 SGLogic 附加到 Unity PlayerLoop。

**创建的系统组层级**：

```
PlayerLoop
 └── SimulationSystemGroup
      ├── FixedStepSimulationSystemGroup
      │    └── SGLogic (自定义系统组)
      └── PresentationSystemGroup
           └── SysGrpDisplay (Cue 渲染 + 事件转发)
```

**内部方法**：

| 方法 | 说明 |
|------|------|
| `SetupGASEntity(Entity)` | 为 ASC Entity 添加所有必要 Buffer/Component |
| `Run()` / `Stop()` | 启停 ECS World |

### `GASEntityMapModel`

**职责**：维护 `CarrierId ↔ Entity` 和 `Entity ↔ GameObject` 的双向映射，实现 `IGASEntityResolver` 接口。

| 映射方向 | 方法 |
|---------|------|
| CarrierId → Entity | `GetGASEntity(CarrierId)` |
| Entity → CarrierId | `GetCarrierId(Entity)` |
| Entity → GameObject | `GetGameObject(Entity)` |
| GameObject → Entity | `GetEntity(GameObject)` |
| 建立绑定 | `BindGameObject(Entity, GameObject)` |
| 移除绑定 | `UnbindGameObject(Entity)` |
| 状态查询 | `IsEntityBound(Entity)` / `IsGameObjectBound(GameObject)` |

### `ConfigModel`

**职责**：GAS 配置数据缓存，存储所有 GE/Ability/Cue/MMC/Tag 的运行时配置。

| 方法 | 说明 |
|------|------|
| `RegisterEffect(configId, configs)` | 注册 GE 配置 |
| `RegisterAbility(code, configs)` | 注册 Ability 配置 |
| `RegisterCues(configs)` / `RegisterMmcs(configs)` | 注册 Cue/MMC 配置 |
| `LoadEffect(loader, configId, path)` | 从 IConfigLoader 加载单个 Effect |
| `LoadAbility(loader, code, path)` | 从 IConfigLoader 加载单个 Ability |
| `LoadEffectsFromDir(loader, dir)` | 批量加载目录中的所有 Effect JSON |
| `GetGameplayEffectConfig(id)` / `GetAbilityConfig(code)` | 查询已加载配置 |

## 系统组运行顺序

```
SGLogic
 ├── SGlobalTimer
 ├── SGAbility
 │    ├── STryActivateAbility
 │    ├── STryCancelAbility
 │    ├── STryEndAbility
 │    └── SAbilityTick
 ├── SGAttribute
 │    └── SUpdateAttributeCurrentValue
 └── SGEffect
      ├── SGEffectCreate
      │    └── SGInstantiateEffect → SInstantiateEffect
      ├── SGEffectOperation
      │    ├── SGCheckApplyEffect → SCheckApplicationRequiredTags → SCheckImmunityTags → SCheckApplyEnd
      │    ├── SGApplyEffect → SPlayCueOnApply → SRemoveEffectWithTags → ... → SGInstantEffect / SGDurationalEffect
      │    ├── SGCheckActivateEffect → SCheckEffectActive
      │    ├── SGActivateEffect → SSetEffectActive / SAddModifiers / SAddGrantedAbility / SEffectAddGrantedTags / ...
      │    ├── SGDeactivateEffect → SSetEffectDeactive / SRemoveModifiers / ...
      │    └── SGRemoveEffect → SRemoveEffectFromAscBuffList / ...
      ├── SGEffectDestroy → SDestroyEffects
      └── SGEffectTick
          └── SGRunningEffect → SEffectDurationTick / SEffectPeriodTick / SEffectStackingTick
```

> **显示系统组**（PresentationSystemGroup 下）
> `SysGrpDisplay` → SCueStart → SCueTick → SCueEnd → SCueDestroy → SEventForwarder

## 关联

- 设计文档：[GAS 设计文档](../design/D002-gas-design.md)
- 核心框架：[核心框架编码文档](nexusframework-core.md)
- ECS 组件：[GAS ECS 组件清单](gas-ecs-components.md)
- Effect 管线：[GAS Effect 管线](gas-effect-pipeline.md)
- Ability 管线：[GAS Ability 管线](gas-ability-pipeline.md)
