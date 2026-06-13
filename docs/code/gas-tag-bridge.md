---
layer: code
status: draft
task: T001
source_files:
  - Assets/NexusFramework.GAS/ECS/Tag/GameplayTag.cs
  - Assets/NexusFramework.GAS/ECS/Tag/Component/BFixedTag.cs
  - Assets/NexusFramework.GAS/ECS/Tag/Component/BTemporaryTag.cs
  - Assets/NexusFramework.GAS/ECS/Tag/Component/SingletonGameplayTagMap.cs
  - Assets/NexusFramework.GAS/ECS/Tag/GameplayTagChangeEvent.cs
  - Assets/NexusFramework.GAS/ECS/Tag/TagRequirementData.cs
  - Assets/NexusFramework.GAS/ECS/Bridge/GASEntityRef.cs
  - Assets/NexusFramework.GAS/ECS/Bridge/IGASEntityResolver.cs
  - Assets/NexusFramework.GAS/ECS/Bridge/GASEvents.cs
  - Assets/NexusFramework.GAS/ECS/Bridge/GASInternalBridge.cs
  - Assets/NexusFramework.GAS/ECS/Bridge/SEventForwarder.cs
  - Assets/NexusFramework.GAS/Services/TagService.cs
  - Assets/NexusFramework.GAS/Services/EventBridgeService.cs
  - Assets/NexusFramework.GAS/Events/GASEvents.cs
created: 2026-06-12
updated: 2026-06-12
---

# 编码文档：GAS Tag 与 Bridge

## 概述

Tag 系统提供层级标签的增删查能力，贯穿 GE 的条件检查、Ability 的激活限制、Cue 的播放条件等所有子系统。Bridge 模块负责 ECS 世界与 NexusFramework 架构层之间的 GameObject 绑定和事件转发。

---

## 一、Tag 系统

### GameplayTag 层级结构

```csharp
public struct GameplayTag
{
    public int Code;          // 标签码
    public int[] Parents;     // 父标签数组（运行时不使用，仅配置时构造层级）
    public int[] Children;    // 子标签数组

    public bool HasTag(int code);  // 检查自身 code 或任意 parent code 是否匹配
}
```

层级关系示例：
```
1 "State" → children: [10, 20]
  10 "State.Debuff" → children: [11, 12]
    11 "State.Debuff.Stun"
    12 "State.Debuff.Slow"
  20 "State.Buff"
100 "Damage" → children: [101, 102]
  101 "Damage.Fire"
  102 "Damage.Ice"
```

### ECS 存储

| Buffer | 元素 | 说明 |
|--------|------|------|
| `BFixedTag` | `int tag` | 永久标签，创建时设定，一般不改变 |
| `BTemporaryTag` | `int tag` + `Entity source` | 临时标签，由 GE 授予/移除，带来源 Entity |

### SingletonGameplayTagMap

全局标签映射表，存储所有标签的层级关系，用于运行时快速查询父子关系：

```csharp
// NativeHashMap<int, ComGameplayTag>
// Key: tag Code, Value: 标签的 Parents/Children 信息
```

### TagService API

```csharp
var tagService = arch.GetService<TagService>();

// 检查 Carrier 是否有某个标签（含层级匹配）
bool isStunned = tagService.HasTag(heroId, tagCode: 11);
// 如果 hero 有标签 11 (State.Debuff.Stun)，返回 true
// 如果 hero 有标签 10 (State.Debuff)，且查询 11 → 通过层级匹配确认

// 添加/移除标签
tagService.AddFixedTag(heroId, tagCode: 42);
tagService.RemoveFixedTag(heroId, tagCode: 42);
tagService.AddTemporaryTag(heroId, tagCode: 99, source: geEntity);
tagService.RemoveTemporaryTag(heroId, tagCode: 99);
```

### TagRequirementData

```csharp
public struct TagRequirementData
{
    public NativeArray<int> all;   // 必须全部满足（AND）
    public NativeArray<int> any;   // 至少满足一个（OR）
    public NativeArray<int> none;  // 全部不能有（NOT）
}
```

### 标签在 GE 管线中的角色

| 标签类型 | 检查阶段 | 说明 |
|---------|---------|------|
| `CApplicationRequiredTags` | CheckApply | 目标必须满足这些标签要求，GE 才能施加 |
| `CEffectImmunityTags` | CheckApply | 目标有任一免疫标签 → GE 被拒绝 |
| `COngoingRequiredTags` | Tick | 持续期间目标必须保持这些标签，否则 GE 失活 |
| `CEffectGrantedTags` | Activate | GE 激活后授予目标的临时标签 |
| `CRemoveEffectWithTags` | Apply | 目标有这些标签 → 触发 GE 移除 |
| `CEffectAssetTags` | - | GE 实体自身携带的标签（用于被其他 GE 查询） |

### 标签在 Ability 管线中的角色

| 标签类型 | 说明 |
|---------|------|
| `CAbilityActivationRequiredTags` | Owner 必须满足才能激活 |
| `CAbilityActivationBlockedTags` | Owner 有这些标签时无法激活 |
| `CAbilityActivationOwnedTags` | Ability 激活后授予 Owner |
| `CAbilityAssetTags` | Ability 自身标签 |
| `CBlockAbilityWithTags` | 激活后阻止其他 Ability |
| `CCancelAbilityWithTags` | 激活后取消其他 Ability |

---

## 二、Bridge 模块

### IGASEntityResolver 接口

```csharp
public interface IGASEntityResolver
{
    GameObject GetGameObject(Entity entity);           // Entity → GameObject
    Entity GetEntity(GameObject go);                    // GameObject → Entity
    void BindGameObject(Entity entity, GameObject go);  // 建立双向绑定
    void UnbindGameObject(Entity entity);               // 移除绑定
    bool IsEntityBound(Entity entity);
    bool IsGameObjectBound(GameObject go);
}
```

实现类：`GASEntityMapModel`。

### GASEntityRef（MonoBehaviour）

挂载在 GameObject 上，由 `GASEntityMapModel.BindGameObject()` 自动添加。提供 Collider → Entity 的反向查找：

```csharp
void OnTriggerEnter(Collider other)
{
    var gasRef = other.GetComponentInParent<GASEntityRef>();
    if (gasRef != null)
    {
        Entity hitEntity = gasRef.Entity;
        // 处理命中逻辑...
    }
}
```

### 事件系统

#### ECS 内部事件（GASEvents.cs）

| 事件 | 触发时机 |
|------|---------|
| `GEAppliedEvent` | GE 成功施加到目标 |
| `GEActivatedEvent` | GE 激活（持续型生效） |
| `GERemovedEvent` | GE 从目标移除 |
| `AttributeChangedEvent` | 属性当前值变化 |
| `AttributeBaseChangedEvent` | 属性基础值变化 |
| `AbilityActivatedEvent` | Ability 激活 |
| `AbilityEndedEvent` | Ability 结束 |
| `AbilityCancelledEvent` | Ability 取消 |
| `EffectStackChangedEvent` | 效果堆叠层数变化 |

#### 架构层事件（Events/GASEvents.cs）

| 事件 | 说明 |
|------|------|
| `GASAttributeChangedEvent` | Carrier 属性变化（含 CarrierId） |
| `GASEffectAppliedEvent` | GE 施加（含 Target/Source CarrierId） |
| `GASAbilityActivatedEvent` | Ability 激活结果 |
| `GASCarrierCreatedEvent` | Carrier 创建 |
| `GASCarrierDestroyedEvent` | Carrier 销毁 |

### 事件桥接流程

```
ECS System (Job)
    │
    ▼
GASInternalBridge.Enqueue(ECS event)
    │
    ▼
SEventForwarder (PresentationSystemGroup)
    │
    ▼
GASInternalBridge.Drain()
    │
    ▼
EventBridgeService.Dispatch(object evt)
    │ Switch on event type
    ▼
this.SendEvent(ECS event)  ──►  NexusFramework TypeEventService
    │
    ▼
游戏层 RegisterEvent<T>() 监听
```

### GASInternalBridge

```csharp
// ECS System 中排队事件
GASInternalBridge.Enqueue(new AttributeChangedEvent { ... });

// 也可以排队任意 Action
GASInternalBridge.Enqueue(() => Debug.Log("ECS System callback on main thread"));

// 在所有 Update 之后排空
GASInternalBridge.Drain();
```

---

## 关联

- 架构：[GAS 架构与服务层](gas-architecture.md)
- ECS 组件：[GAS ECS 组件清单](gas-ecs-components.md)
- Effect 管线：[GAS Effect 管线](gas-effect-pipeline.md)
- Ability 管线：[GAS Ability 管线](gas-ability-pipeline.md)
