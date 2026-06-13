---
layer: code
status: draft
task: T001
source_files:
  - Assets/NexusFramework.GAS/ECS/Effect/Component/**/*.cs
  - Assets/NexusFramework.GAS/ECS/System/SGEffect*.cs
  - Assets/NexusFramework.GAS/ECS/System/SInstantiateEffect.cs
  - Assets/NexusFramework.GAS/ECS/System/SGCheckApplyEffect.cs
  - Assets/NexusFramework.GAS/ECS/System/SGApplyEffect.cs
  - Assets/NexusFramework.GAS/ECS/System/SGActivateEffect.cs
  - Assets/NexusFramework.GAS/ECS/System/SGDeactivateEffect.cs
  - Assets/NexusFramework.GAS/ECS/System/SGRemoveEffect.cs
  - Assets/NexusFramework.GAS/ECS/System/SGRunningEffect.cs
  - Assets/NexusFramework.GAS/Services/EffectService.cs
created: 2026-06-12
updated: 2026-06-12
---

# 编码文档：GAS Effect 管线

## 概述

GameplayEffect 是 GAS 中最核心的系统。一个 GE 从施加到销毁经历 **7 个阶段**，每个阶段由独立的 System Group 驱动。管线通过 WIP（Work In Progress）标记组件实现阶段间状态传递。

## GE 类型

| 类型 | CDuration 状态 | 行为 |
|------|---------------|------|
| **Instant（即时）** | 无 CDuration 组件 | 立即执行 Modifier → 立即销毁 |
| **Durational（持续）** | duration > 0 | 持续 duration 帧/回合，然后自动结束 |
| **Infinite（无限）** | duration ≤ 0 | 永久持续，直到手动移除 |

---

## 七阶段状态机

```
Request (EffectService.ApplyEffect)
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
│ SApplyEnd        │    │ SCheckEffectStacking (检查堆叠)   │
│ (标记 Apply 完成)│    │ SPlayCueOnAdd                    │
└──────────────────┘    └──────────────────────────────────┘
    │                             │
    ▼                             ▼
┌──────────────────┐    ┌─────────────────────────────────────┐
│ ④ EXECUTE        │    │ ④ CHECK ACTIVE → ACTIVATE          │
│ SExecuteInstant  │    │ SCheckEffectActive                  │
│  EffectModifiers │    │ SSetEffectActive → 标记 Active      │
│ → 执行修饰器      │    │ SAddModifiers → 注册 Modifier      │
│ → 触发回调        │    │ SEffectAddGrantedTags → 赋予标签    │
│ → 进入 DESTROY    │    │ SAddGrantedAbility → 授权技能      │
└──────────────────┘    │ SPlayCueOnActivate → 激活 Cue      │
                         │ SPlayCueOnTick → 启动周期 Cue     │
                         └─────────────────────────────────────┘
                                    │
                               ┌────┴────┐
                               ▼         ▼
                        ┌──────────┐  ┌──────────┐
                        │ ⑤ TICK   │  │⑥ DEACTIVATE│
                        │ Duration  │  │ Duration  │
                        │ 计时到期  │  │ 结束      │
                        │ Period    │  │ SRemoveModifiers│
                        │ 周期触发  │  │ SEffectRemove   │
                        │ Stacking  │  │  GrantedTags    │
                        │ 维护      │  │ SRemoveGranted  │
                        └──────────┘  │  Ability         │
                             │        │ SPlayCueOnDeact │
                             ▼        │ SSetEffectDeact │
                        ┌──────────┐  └────────────────┘
                        │ ⑦ REMOVE │◄───────┘
                        │ SRemoveFrom       │
                        │  ASC Buff List    │
                        │ SPlayCueOnRemove  │
                        │ Destroy Entity    │
                        └──────────┘
```

---

## EffectService API

```csharp
var effectService = arch.GetService<EffectService>();

// 施加 GE（ConfigModel 中查找 configId → 创建 ECS Entity → 进入管线）
effectService.ApplyEffect(
    configId: 1,      // 从 ConfigModel 中查找的 GE 配置 ID
    target: enemyId,  // 目标 Carrier
    source: heroId    // 来源 Carrier
);
```

---

## CDuration 详解

```csharp
public struct CDuration : IComponentData
{
    public int duration;                        // 持续时间（≤0 = 无限）
    public TimeUnit timeUnit;                   // Frame 或 Turn
    public bool ResetStartTimeWhenActivated;    // 每次激活时刷新起始时间
    public bool StopTickWhenDeactivated;        // 失活时暂停计时

    // 运行时数据
    public int activeTime;       // 开始计时的时间点
    public bool active;           // 是否处于激活状态
    public int lastActiveTime;   // 上次激活时间（用于暂停后恢复）
    public int remianTime;       // 剩余持续时间（用于暂停后恢复）
}
```

---

## CStacking 堆叠策略

### 堆叠类型

```csharp
public enum EffectStackType
{
    AggregateBySource,  // 按来源聚合：同一来源多次施加合并为一个堆叠
    AggregateByTarget   // 按目标聚合：不同来源的 GE 合并为一个堆叠
}
```

### 堆叠过期策略

| 策略 | 行为 |
|------|------|
| `ClearEntireStack` | 过期清除整个堆叠 |
| `RemoveSingleStackAndRefreshDuration` | 过期移除一层并刷新持续时间 |
| `RefreshDuration` | 仅刷新持续时间 |

### 堆叠配置

```csharp
public struct CStacking : IComponentData
{
    public EffectStackType StackType;
    public int StackingCode;            // 堆叠匹配码（相同码的 GE 视为同一堆叠）
    public int LimitCount;              // 堆叠层数上限
    public EffectDurationRefreshPolicy EffectDurationRefreshPolicy;
    public EffectPeriodResetPolicy EffectPeriodResetPolicy;
    public EffectExpirationPolicy EffectExpirationPolicy;
    public bool denyOverflowApplication;  // 达到上限时拒绝额外施加
    public bool clearStackOnOverflow;     // 达到上限时清除全部堆叠
    public NativeArray<Entity> overflowEffects;  // 溢出时触发的额外 GE

    // 运行时
    public int StackCount;               // 当前堆叠层数
}
```

### 堆叠示例

```
ApplyEffect(configId, target, source)
    │
    ├── StackingType == None
    │   → 每次创建独立 GE Entity
    │
    ├── StackingType == AggregateByTarget
    │   → 按 (configId, targetId) 聚合，StackCount++
    │
    └── StackingType == AggregateBySource
        → 按 (configId, sourceId) 聚合，StackCount++

StackCount ≥ LimitCount → 新施加被拒绝 (denyOverflow)
```

---

## CPeriod 周期触发

```csharp
public struct CPeriod : IComponentData
{
    public int Period;                              // 触发间隔
    public bool ResetTimeCountWhenDeactivated;       // 失活后重新激活时是否重置计时
    public NativeArray<Entity> GameplayEffects;      // 每次周期触发时执行的 GE 列表

    // 运行时
    public int StartTime;  // 周期计时起始时间
}
```

---

## MCGrantedAbility 授予能力

GE 激活时可自动授予 Ability，失活/移除时自动回收：

```csharp
public struct GrantedAbility
{
    public AbilityConfig AbilityConfig;
    public int Level;
    public GrantedAbilityActivationPolicy ActivationPolicy;     // 授予时是否自动激活
    public GrantedAbilityDeactivationPolicy DeactivationPolicy; // GE 失活时的处理
    public GrantedAbilityRemovePolicy RemovePolicy;             // GE 移除时的处理
}
```

| 策略枚举 | 值 | 说明 |
|---------|---|------|
| `ActivationPolicy` | None / OnGranted | 是否自动激活 |
| `DeactivationPolicy` | None / EndAbility | GE 失活时的处理 |
| `RemovePolicy` | None / EndAbility / CancelAbility | GE 移除时的处理 |

---

## 关键实现说明

1. **WIP 标记驱动**：管线通过 `WipXxx` 标记组件实现阶段间松耦合——每个 System 只检查它关心的 WIP 标记，不依赖前一阶段的执行顺序
2. **即时效果跳过中间阶段**：无 `CDuration` 的 GE 直接走 Apply → Execute → Destroy，不经过 Activate/Tick/Deactivate
3. **堆叠发生在 Apply 阶段**：`SCheckEffectStacking` 在 `SGDurationalEffect` 中处理，决定是创建新 Entity 还是增加 StackCount
4. **NativeArray 生命周期**：GE 组件中的 `NativeArray`（如 overflowEffects、GameplayEffects）需在 GE 销毁时正确 Dispose

## 关联

- 设计文档：[GAS 设计 - Effect 状态机](../design/D002-gas-design.md)
- ECS 组件：[GAS ECS 组件清单](gas-ecs-components.md)
- 架构：[GAS 架构与服务层](gas-architecture.md)
- Cue 管线：[GAS Cue 管线](gas-cue-pipeline.md)
- 属性/MMC：[GAS 属性与 MMC](gas-attribute-mmc.md)
- Tag 系统：[GAS Tag 与 Bridge](gas-tag-bridge.md)
