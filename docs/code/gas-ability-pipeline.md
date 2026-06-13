---
layer: code
status: draft
task: T001
source_files:
  - Assets/NexusFramework.GAS/ECS/Ability/Component/**/*.cs
  - Assets/NexusFramework.GAS/ECS/Ability/TargetCatcher/**/*.cs
  - Assets/NexusFramework.GAS/ECS/System/SGAbility*.cs
  - Assets/NexusFramework.GAS/ECS/System/STryActivateAbility.cs
  - Assets/NexusFramework.GAS/ECS/System/STryCancelAbility.cs
  - Assets/NexusFramework.GAS/ECS/System/STryEndAbility.cs
  - Assets/NexusFramework.GAS/ECS/System/SAbilityTick.cs
  - Assets/NexusFramework.GAS/Services/AbilityService.cs
created: 2026-06-12
updated: 2026-06-12
---

# 编码文档：GAS Ability 管线

## 概述

Ability 代表角色可执行的动作（攻击、技能、法术等）。每个 Ability 通过配置（`AbilityComponentConfig`）定义其行为，运行时由 `AbilityLogicBase` 子类驱动具体逻辑。

## Ability 生命周期

```
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

---

## AbilityService API

```csharp
var abilityService = arch.GetService<AbilityService>();

// 授予能力
abilityService.GrantAbility(carrier: heroId, abilityCode: 1001, architecture: arch);

// 激活能力
bool success = abilityService.TryActivate(carrier: heroId, abilityCode: 1001, param: xParam);

// 检查是否激活中
bool isActive = abilityService.IsActive(heroId, abilityCode: 1001);

// 结束 / 取消 / 移除
abilityService.TryEnd(heroId, 1001);
abilityService.TryCancel(heroId, 1001);
abilityService.RemoveAbility(heroId, 1001);
```

---

## AbilityLogicBase 自定义逻辑

```csharp
public abstract class AbilityLogicBase : IBelongToArchitecture
{
    // ── 必须实现 ──
    public abstract void ActivateAbility(GlobalTimer timer);  // 激活时调用
    public abstract void CancelAbility(GlobalTimer timer);    // 取消时调用
    public abstract void EndAbility(GlobalTimer timer);       // 结束时调用
    public abstract void AbilityTick(GlobalTimer timer);      // 每帧 Tick

    // ── 辅助方法 ──
    public virtual void TryEndSelf();                         // 主动结束自己
    public virtual void SetParam(XParam param);               // 设置参数
    public Entity GetOwnerAscEntity();                        // 获取 Owner ASC Entity
    protected Entity CreateGameplayEffectEntity(GameplayEffectComponentConfig[] configs);
    protected void ApplyGameplayEffectTo(Entity ge, Entity target, Entity source);
    protected void RemoveGameplayEffect(Entity ge);
}

// 泛型版本（推荐使用）
public abstract class AbilityLogicBase<T> : AbilityLogicBase where T : XParam
{
    protected T _param;
}
```

### 内置 AbilityLogic

| 类 | 参数类型 | 行为 |
|---|---------|------|
| `ALApplyEffect` | `XParamEffectIDs` | 激活时根据 EffectCode 列表创建并施加 GE 到 Owner |
| `ALDebugLog` | `XParamString` | 在各生命周期阶段输出 Debug.Log |

### AbilityLogicFactory

```csharp
// 手动注册
AbilityLogicFactory.Register("ALFireball", typeof(ALFireball));

// 设置 EntityManager（由 AbilityService 自动设置）
AbilityLogicFactory.SetEntityManager(em);

// 创建实例（由 ECS System 调用）
var logic = AbilityLogicFactory.TryCreateAbilityLogic("ALFireball", abilityEntity);
```

---

## ECS System 管线

```
STryActivateAbility
    ├── 检查 RequiredTags / BlockedTags（在 SAbilityTick 中处理）
    ├── 检查 Cooldown
    ├── 执行 Cost
    ├── ActivateAbility() [调用 AbilityLogic]
    └── 添加 CAbilityActive

STryCancelAbility
    ├── CancelAbility() [调用 AbilityLogic]
    └── 移除 CAbilityActive

STryEndAbility
    ├── EndAbility() [调用 AbilityLogic]
    └── 移除 CAbilityActive

SAbilityTick
    └── 对每个 CAbilityActive 的 Ability 调用 AbilityTick()
```

---

## TargetCatcher 目标捕获

TargetCatcher 负责在 Ability 激活时捕获目标 Entity 列表。

### TargetCatcherBase

```csharp
public abstract class TargetCatcherBase
{
    public Entity Owner;
    protected IGASEntityResolver _entityResolver;

    public virtual void Init(Entity owner);
    public void SetEntityResolver(IGASEntityResolver resolver);
    public virtual void InitParameters(XParam parameter);

    // 捕获目标
    public void CatchTargetsNonAllocSafe(Entity mainTarget, ref List<Entity> results);
    protected abstract void CatchTargetsNonAlloc(Entity mainTarget, List<Entity> results);
}
```

### 内置 TargetCatcher

| 类 | 参数类型 | 行为 |
|---|---------|------|
| `CatchSelf` | `XParamNone` | 返回 [Owner] |
| `CatchTarget` | `XParamNone` | 返回 [mainTarget] |
| `CatchAreaBox3D` | `XParamCatchAreaBox3D` | Physics.OverlapBoxNonAlloc → 通过 `_entityResolver` 反查 Entity |
| `CatchAreaBox2D` | `XParamCatchAreaBox2D` | Physics2D.OverlapBoxAll |
| `CatchAreaCircle2D` | `XParamCatchAreaCircle2D` | Physics2D.OverlapCircleAll |

### 便捷方法（TargetCatcherBase 提供）

```csharp
// 内部调用 _entityResolver，内置空值保护
protected GameObject ResolveGameObject(Entity entity);
protected Entity ResolveEntity(GameObject go);
```

---

## 冷却和消耗

### CAbilityCooldown

```csharp
public struct CAbilityCooldown : IComponentData
{
    public int Cooldown;                          // 冷却帧数
    public Entity ProtoGameplayEffectCooldown;    // 冷却 GE 原型
    public NativeArray<int> CooldownTags;          // 冷却标签（从 GE 的 GrantedTags 提取）
}
```

冷却实现为 **GE 原型模式**：激活时 Instantiate 原型 GE → 施加到 Owner → 冷却 GE 授予冷却标签 → 标签阻止重复激活。

### CAbilityCost

```csharp
public struct CAbilityCost : IComponentData
{
    public Entity ProtoGameplayEffectCost;         // 消耗 GE 原型
}
```

消耗同样使用 GE 原型模式：Instantiate → AddModifier（如扣 MP）→ 立即执行。

---

## 关联

- ECS 组件：[GAS ECS 组件清单](gas-ecs-components.md)
- Effect 管线：[GAS Effect 管线](gas-effect-pipeline.md)
- 架构：[GAS 架构与服务层](gas-architecture.md)
- Cue 管线：[GAS Cue 管线](gas-cue-pipeline.md)
- Tag 系统：[GAS Tag 与 Bridge](gas-tag-bridge.md)
