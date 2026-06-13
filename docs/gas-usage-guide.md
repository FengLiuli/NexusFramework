---
layer: guide
status: draft
task: T001
created: 2026-06-11
updated: 2026-06-12
---

# NexusFramework.GAS 完整使用指南

> NexusFramework.GAS 是一个基于 Unity DOTS/ECS 的 Gameplay Ability System，受 UE GAS 启发设计。
> 支持 能力激活、效果施加、属性计算、GameplayCue 表现、Tag 层级系统、堆叠、冷却、消耗等完整 GAS 管线。

## 关联

- 设计文档：[GAS 设计文档](design/D002-gas-design.md)
- 编码文档总索引：[GAS 编码文档](code/nexusframework-gas.md)
- 核心框架：[核心框架编码文档](code/nexusframework-core.md)

---

## 目录

1. [架构概览](#1-架构概览)
2. [初始化与生命周期](#2-初始化与生命周期)
3. [Carrier（载体）系统](#3-carrier载体系统)
4. [GameObject ↔ Entity 桥接](#4-gameobject--entity-桥接)
5. [Tag（标签）系统](#5-tag标签系统)
6. [Attribute（属性）系统](#6-attribute属性系统)
7. [GameplayEffect（效果）系统](#7-gameplayeffect效果系统)
8. [GameplayCue（表现提示）系统](#8-gameplaycue表现提示系统)
9. [Ability（能力）系统](#9-ability能力系统)
10. [TargetCatcher（目标捕获）系统](#10-targetcatcher目标捕获系统)
11. [MMC（数值计算）系统](#11-mmc数值计算系统)
12. [Event（事件）系统](#12-event事件系统)
13. [Config（配置）系统](#13-config配置系统)
14. [Timer & World（时间与世界）系统](#14-timer--world时间与世界系统)
15. [完整示例：火球技能](#15-完整示例火球技能)

---

## 1. 架构概览

```
┌─────────────────────────────────────────────────────────────────┐
│                     GASArchitecture                              │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐           │
│  │ Ability  │ │ Effect   │ │Attribute │ │  Cue     │           │
│  │ Service  │ │ Service  │ │ Service  │ │ Service  │           │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘           │
│       │             │            │            │                  │
│  ┌────┴─────────────┴────────────┴────────────┴─────┐           │
│  │              ECS World (DOTS)                      │          │
│  │  ┌─────────┐  ┌──────────┐  ┌─────────────────┐   │          │
│  │  │ SGLogic │  │SGAbility │  │ SGEffect (50+   │   │          │
│  │  │         │  │          │  │   Systems)      │   │          │
│  │  └─────────┘  └──────────┘  └─────────────────┘   │          │
│  └───────────────────────────────────────────────────┘           │
│  ┌──────────┐ ┌──────────┐ ┌──────────────┐ ┌────────────┐     │
│  │  Tag     │ │  Timer   │ │ EventBridge  │ │   World    │     │
│  │ Service  │ │ Service  │ │  Service     │ │  Service   │     │
│  └──────────┘ └──────────┘ └──────────────┘ └────────────┘     │
│  ┌──────────────┐ ┌──────────────────┐                         │
│  │ ConfigModel  │ │ GASEntityMapModel│                         │
│  └──────────────┘ └──────────────────┘                         │
└─────────────────────────────────────────────────────────────────┘
```

### 核心概念对应表

| NexusFramework.GAS | UE GAS | 说明 |
|---|---|---|
| `GASArchitecture` | `UAbilitySystemComponent` 的宿主 | 管理所有 GAS 子系统的架构 |
| `CarrierId` | ASC 标识 | 64 位紧凑 ID，关联 ECS Entity |
| `BAbility` | `FGameplayAbilitySpec` | ECS 实体上的能力 Buffer |
| `BGameplayEffect` | `FActiveGameplayEffect` | ECS 实体上的效果 Buffer |
| `BEAttrSet` | `UAttributeSet` | ECS 实体上的属性集 Buffer |
| `GameplayEffectComponentConfig` | `UGameplayEffect` CDO | 效果配置的抽象基类 |
| `AbilityComponentConfig` | `UGameplayAbility` CDO | 能力配置的抽象基类 |
| `GameplayCueBase` | `UGameplayCueManager` + `UGameplayCueNotify` | GameplayCue 表现基类 |
| `GameplayTag` | `FGameplayTag` | 层级标签 |
| `MMC` | `UGameplayModMagnitudeCalculation` | 数值计算 |

### 数据流向

```
配置数据 (JSON/Excel via Luban)
    │
    ▼
IConfigLoader.ParseXxx()  ──►  ConfigModel (缓存)
    │
    ▼
Config.LoadToGameplayXxxEntity()  ──►  ECS Entity + Components
    │
    ▼
ECS Systems (管线处理)  ──►  属性变更 / 事件触发
    │
    ▼
EventBridgeService  ──►  NexusFramework 事件系统
    │
    ▼
UI 绑定 / 游戏逻辑响应
```

---

## 2. 初始化与生命周期

### 2.1 创建 GASArchitecture 子类

```csharp
using NexusFramework;
using NexusFramework.GAS;
using NexusFramework.GAS.Config;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using NexusFramework.GAS.Services;

// 1. 创建自己的 GAS 架构子类
public class MyGameArchitecture : GASArchitecture
{
    // 可选：覆盖 ConfigLoader
    protected override IConfigLoader CreateConfigLoader()
    {
        return new JsonConfigLoader();
    }

    protected override void OnInit()
    {
        base.OnInit();  // 自动注册所有 8 个 Service + 2 个 Model

        // 注册 Carrier 类型
        GetCarrierManager().RegisterType("Hero");
        GetCarrierManager().RegisterType("Enemy");
        GetCarrierManager().RegisterType("Projectile");

        // 加载游戏配置
        var configModel = GetModel<ConfigModel>();
        var loader = GetUtility<IConfigLoader>();

        // 从 JSON 文件加载效果和能力配置
        configModel.LoadEffectsFromDir(loader, "Config/Effects/");
        configModel.LoadAbilitiesFromDir(loader, "Config/Abilities/");
        configModel.LoadTags(loader, "Config/tags.json");

        // 注册自定义 AbilityLogic 和 GameplayCue
        // CueService 和 AbilityService 的 ScanAndRegisterAll() 已自动扫描
    }
}

// 2. 在 Framework 中注册
ArchitectureFactory.RegisterArchitecture<MyGameArchitecture>("MyGame");

// 3. 创建实例
var arch = ArchitectureFactory.CreateArchitecture("MyGame", instanceId: 1) as MyGameArchitecture;
```

### 2.2 生命周期状态

```csharp
public enum ArchitectureState
{
    NotInitialized,  // 未初始化
    Initializing,    // 初始化中
    Initialized,     // 已初始化（正常运行）
    Paused,          // 已暂停
    Shutting,        // 关闭中
    Shutdown         // 已关闭
}

// 控制方法
arch.Initialize();   // 初始化
arch.Pause();        // 暂停
arch.Resume();       // 恢复
arch.Shutdown();     // 关闭（清理所有资源）
arch.Dispose();      // 等同于 Shutdown()
```

### 2.3 生命周期事件

```csharp
// 通过 NexusFramework 事件系统监听
arch.RegisterEvent<ArchitectureBeforeInitEvent>(e => Debug.Log($"初始化前: {e.ArchitectureId}"));
arch.RegisterEvent<ArchitectureAfterInitEvent>(e => Debug.Log($"初始化完成: {e.ArchitectureId}"));
arch.RegisterEvent<ArchitectureBeforePauseEvent>(e => Debug.Log($"暂停前: {e.ArchitectureId}"));
arch.RegisterEvent<ArchitectureBeforeResumeEvent>(e => Debug.Log($"恢复前: {e.ArchitectureId}"));
arch.RegisterEvent<ArchitectureBeforeShutdownEvent>(e => Debug.Log($"关闭前: {e.ArchitectureId}"));
arch.RegisterEvent<ArchitectureAfterShutdownEvent>(e => Debug.Log($"关闭后: {e.ArchitectureId}"));
```

### 2.4 8 个内置 Service

| Service | 职责 |
|---|---|
| `WorldService` | 管理 ECS World 和 EntityManager，创建所有 SystemGroup |
| `TimerService` | 全局计时器（Frame / Turn） |
| `EventBridgeService` | 将 ECS 内部事件桥接到 NexusFramework 事件系统 |
| `TagService` | Tag 查询（HasTag） |
| `AbilityService` | 能力授予、激活、取消、结束、移除 |
| `EffectService` | GameplayEffect 施加 |
| `AttributeService` | 属性读写 |
| `CueService` | GameplayCue 类型注册与扫描 |

### 2.5 2 个内置 Model

| Model | 职责 |
|---|---|
| `GASEntityMapModel` | CarrierId ↔ Entity ↔ GameObject 三向映射 |
| `ConfigModel` | GE/Ability/Cue/MMC/Tag 配置缓存 |

---

## 3. Carrier（载体）系统

Carrier 是 GAS 中的核心概念，代表"拥有 GAS 能力的东西"（等同于 UE 中的 ASC Owner）。

### 3.1 创建和销毁

```csharp
// 方式 1：仅创建 Carrier + Entity（无 GameObject 绑定）
CarrierId heroId = arch.CreateGASCarrier("Hero");

// 方式 2：创建 Carrier + Entity + GameObject 绑定
GameObject heroGo = Instantiate(heroPrefab);
CarrierId heroId = arch.CreateGASCarrier("Hero", heroGo);

// 方式 3：为已有 Carrier 后绑定 GameObject
arch.BindGameObjectForCarrier(heroId, heroGo);

// 销毁 Carrier（自动清理 Entity 和绑定）
arch.DestroyGASCarrier(heroId);

// 使用 DataCarrier Trait 存储额外数据（可选）
var trait = new GASEntityTrait { GasEntityRaw = entity.Index };
arch.GetCarrierManager().AddTrait(heroId, trait);
```

### 3.2 Carrier 创建后自动获得的 ECS Components

每次 `CreateGASCarrier` 都会自动为 ECS Entity 添加以下 Buffer/Component：

```csharp
// 自动添加的 Buffer：
EntityManager.AddBuffer<BEAttrSet>(entity);        // 属性集
EntityManager.AddBuffer<BGameplayEffect>(entity);  // 活跃 GameplayEffect 列表
EntityManager.AddBuffer<BAbility>(entity);         // 已授予 Ability 列表
EntityManager.AddBuffer<BFixedTag>(entity);        // 固有标签
EntityManager.AddBuffer<BTemporaryTag>(entity);    // 临时标签（带来源）

// 自动添加的 Component：
EntityManager.AddComponent<CAscBasicData>(entity);  // 基础数据（等级等）
```

### 3.3 CarrierId 位域设计

```
┌───────┬──────────┬────────────────────────────────────────┐
│ 8 bit │  16 bit  │               40 bit                    │
│FrameworkId│TypeId│              UniqueId                    │
│  (256)   │(65535)│        (1 万亿实例)                      │
└───────┴──────────┴────────────────────────────────────────┘
```

```csharp
CarrierId id = new CarrierId(frameworkId: 0, typeId: 1, uniqueId: 100);
byte fid = id.FrameworkId;   // 0
ushort tid = id.TypeId;       // 1
ulong uid = id.UniqueId;      // 100
bool valid = id.IsValid;      // true
```

---

## 4. GameObject ↔ Entity 桥接

### 4.1 IGASEntityResolver 接口

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

实际实现是 `GASEntityMapModel`。

### 4.2 GASEntityRef（MonoBehaviour 反向查找）

```csharp
// 挂载在 GameObject 上，由 GASEntityMapModel 自动添加
// 游戏层通过 Collider 反查 Entity
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

### 4.3 GameplayCue / TargetCatcher 中的使用

```csharp
// GameplayCueBase 子类中使用
public class MyCue : GameplayCueBase<MyParam>
{
    public override void OnAdd(float time)
    {
        var go = GetTargetAscGameObject();  // 内部调用 _entityResolver.GetGameObject(_targetAscEntity)
        if (go != null)
        {
            // 在 GameObject 上播放特效/音效
        }
    }
}

// TargetCatcherBase 子类中使用
public class MyCatcher : TargetCatcherBase<MyParam>
{
    protected override void CatchTargetsNonAlloc(Entity mainTarget, List<Entity> results)
    {
        var go = _entityResolver?.GetGameObject(mainTarget);
        // 使用 Transform 进行空间计算
    }
}
```

---

## 5. Tag（标签）系统

### 5.1 标签层级结构

```json
// tags.json 示例 (Luban 导出)
{
  "Tags": [
    {
      "Code": 1,
      "Name": "State",
      "Children": [10, 20]
    },
    {
      "Code": 10,
      "Name": "State.Debuff",
      "Children": [11, 12]
    },
    {
      "Code": 11,
      "Name": "State.Debuff.Stun"
    },
    {
      "Code": 20,
      "Name": "State.Buff"
    },
    {
      "Code": 100,
      "Name": "Damage",
      "Children": [101, 102]
    },
    {
      "Code": 101,
      "Name": "Damage.Fire"
    },
    {
      "Code": 102,
      "Name": "Damage.Ice"
    }
  ]
}
```

### 5.2 标签类型

```csharp
// BFixedTag —— 固有标签，创建时设定，一般不改变
public struct BFixedTag : IBufferElementData
{
    public int tag;
}

// BTemporaryTag —— 临时标签，由 GE 授予/移除，带来源 Entity
public struct BTemporaryTag : IBufferElementData
{
    public int tag;
    public Entity source;  // 标签来源（哪个 GE 授予的）
}
```

### 5.3 TagService 查询

```csharp
var tagService = arch.GetService<TagService>();

// 检查 Carrier 是否有某个标签（含层级匹配）
bool isStunned = tagService.HasTag(heroId, tagCode: 11);
// 如果 hero 有标签 11 (State.Debuff.Stun)，返回 true
// 如果 hero 有标签 10 (State.Debuff)，且查询 11，需要配置父子关系才返回 true
```

### 5.4 标签需求数据 (TagRequirementData)

```csharp
public struct TagRequirementData
{
    public NativeArray<int> all;   // 必须全部满足（AND）
    public NativeArray<int> any;   // 至少满足一个（OR）
    public NativeArray<int> none;  // 全部不能有（NOT）
}
```

### 5.5 标签在 GE 配置中的应用

```csharp
// CApplicationRequiredTags —— 施加条件标签
// 目标必须满足这些标签要求，GE 才能施加成功
_entityManager.AddComponent<CApplicationRequiredTags>(geEntity);
_entityManager.SetComponentData(geEntity, new CApplicationRequiredTags
{
    requirement = new TagRequirementData
    {
        all = new NativeArray<int>(new[] { 20 }, Allocator.Persistent),  // 必须有 State.Buff
        any = default,
        none = new NativeArray<int>(new[] { 11, 12 }, Allocator.Persistent) // 不能有 Stun 等
    }
});

// CEffectImmunityTags —— 免疫标签
// 如果目标有这些标签，GE 会被免疫
_entityManager.AddComponent<CEffectImmunityTags>(geEntity);

// CEffectAssetTags —— GE 自带标签
// GE 实体自身带有的标签，用于条件判断
_entityManager.AddComponent<CEffectAssetTags>(geEntity);

// CEffectGrantedTags —— GE 授予目标的标签
// GE 激活期间授予目标的临时标签
_entityManager.AddComponent<CEffectGrantedTags>(geEntity);

// COngoingRequiredTags —— 持续条件标签
// GE 激活期间目标必须持续满足的标签要求，否则 GE 失活
```

### 5.6 标签在 Ability 配置中的应用

```csharp
// CAbilityActivationRequiredTags —— 激活所需标签
// Owner 必须满足才能激活 Ability
_entityManager.AddComponent<CAbilityActivationRequiredTags>(abilityEntity);

// CAbilityActivationBlockedTags —— 激活阻止标签
// Owner 有这些标签时无法激活 Ability
_entityManager.AddComponent<CAbilityActivationBlockedTags>(abilityEntity);

// CAbilityActivationOwnedTags —— 激活拥有标签
// Ability 激活后授予 Owner 的标签
_entityManager.AddComponent<CAbilityActivationOwnedTags>(abilityEntity);

// CAbilityAssetTags —— Ability 自带标签
_entityManager.AddComponent<CAbilityAssetTags>(abilityEntity);

// CBlockAbilityWithTags —— 通过标签阻止其他 Ability
_entityManager.AddComponent<CBlockAbilityWithTags>(abilityEntity);

// CCancelAbilityWithTags —— 通过标签取消其他 Ability
_entityManager.AddComponent<CCancelAbilityWithTags>(abilityEntity);
```

---

## 6. Attribute（属性）系统

### 6.1 属性数据结构

```csharp
// 单个属性
public struct CAttributeData : IComponentData
{
    public int Code;           // 属性代码（如 1=HP, 2=MP, 3=Attack）
    public float BaseValue;    // 基础值（未经 GE 修改）
    public float CurrentValue; // 当前值（BaseValue + 所有活跃 GE Modifier）
    public bool IsClampMin;    // 是否限制最小值
    public bool IsClampMax;    // 是否限制最大值
    public float MinValue;     // 最小值
    public float MaxValue;     // 最大值
    public bool Dirty;         // 是否需要重算
}

// 属性集 Buffer —— 存在 ASC Entity 上
public struct BEAttrSet : IBufferElementData
{
    public int Code;                            // 属性集代码
    public NativeArray<CAttributeData> Attributes;  // 该集合中的所有属性
}
```

### 6.2 初始化属性

```csharp
// 为 Carrier 初始化属性集
var entity = arch.GetModel<GASEntityMapModel>().GetGASEntity(heroId);
var em = arch.GetService<WorldService>().EntityManager;
var attrSetBuf = em.GetBuffer<BEAttrSet>(entity);

// 创建属性集 1（基础战斗属性）
var combatAttrs = new NativeArray<CAttributeData>(3, Allocator.Persistent);
combatAttrs[0] = new CAttributeData
{
    Code = 1,          // HP
    BaseValue = 100f,
    CurrentValue = 100f,
    IsClampMin = true, MinValue = 0f,
    IsClampMax = true, MaxValue = 9999f,
    Dirty = false
};
combatAttrs[1] = new CAttributeData
{
    Code = 2,          // MP
    BaseValue = 50f,
    CurrentValue = 50f,
    IsClampMin = true, MinValue = 0f,
    IsClampMax = true, MaxValue = 9999f,
    Dirty = false
};
combatAttrs[2] = new CAttributeData
{
    Code = 3,          // Attack
    BaseValue = 10f,
    CurrentValue = 10f,
    Dirty = false
};

attrSetBuf.Add(new BEAttrSet { Code = 1, Attributes = combatAttrs });
```

### 6.3 AttributeService API

```csharp
var attrService = arch.GetService<AttributeService>();

// 获取当前值（经过 GE 修改后的）
float hp = attrService.GetCurrentValue(heroId, attrSetCode: 1, attrCode: 1);

// 获取基础值（未修改的裸值）
float baseAtk = attrService.GetBaseValue(heroId, attrSetCode: 1, attrCode: 3);

// 设置基础值（会标记 Dirty，触发 ECS 重算）
attrService.SetBaseValue(heroId, attrSetCode: 1, attrCode: 1, value: 150f);

// 设置当前值（不触发重算，慎用！通常只在初始化时使用）
attrService.SetCurrentValue(heroId, attrSetCode: 1, attrCode: 2, value: 0f);

// 检查属性是否存在
bool hasAttr = attrService.HasAttribute(heroId, attrSetCode: 1, attrCode: 5);
```

### 6.4 QueryAttributeValue（只读查询）

```csharp
// 使用 Query 模式查询属性（通过架构层，不直接访问 ECS）
float hp = arch.SendQuery(new QueryAttributeValue
{
    Target = heroId,
    AttrSetCode = 1,
    AttrCode = 1
});
```

### 6.5 GASAttributeBindable（UI 响应式绑定）

```csharp
using NexusFramework.GAS.Binding;

// 创建一个响应式属性绑定
var hpBindable = new GASAttributeBindable(
    heroId,
    attrSetCode: 1,
    attrCode: 1,
    defaultValue: 100f
);

// 绑定到架构事件（监听属性变化）
hpBindable.Bind(arch);

// UI 层订阅变化
hpBindable.RegisterWithInitValue(val =>
{
    hpText.text = $"HP: {val:F0}";
});

// 解绑
hpBindable.Unbind();
```

### 6.6 属性重算流程

```
SetBaseValue() / GE 激活 / GE 移除
    │
    ▼
CAttributeIsDirty 标记
    │
    ▼
SUpdateAttributeCurrentValue System
    │
    ▼
AttributeHelper.RecalculateCurrentValue()
    ├── 从 BaseValue 开始
    ├── 遍历所有活跃 GE 的 Modifier（Apply 顺序）
    ├── Clamp Min/Max
    ├── 清除 Dirty 标记
    └── 值变化时 Enqueue AttributeChangedEvent
    │
    ▼
SEventForwarder 在 PresentationSystemGroup 排空
    │
    ▼
EventBridgeService 转换为 GASAttributeChangedEvent
    │
    ▼
UI / 游戏逻辑响应
```

---

## 7. GameplayEffect（效果）系统

GameplayEffect 是 GAS 中最核心的系统。它定义了"对目标做什么"。

### 7.1 GE 类型

| 类型 | CDuration.duration | 行为 |
|---|---|---|
| **Instant（即时）** | 无 CDuration 组件 | 立即执行 Modifier → 立即销毁 |
| **Durational（持续）** | duration > 0 | 持续 duration 帧/回合，然后自动结束 |
| **Infinite（无限）** | duration ≤ 0 | 永久持续，直到手动移除 |

### 7.2 GE 组件配置体系

所有 GE 配置都继承自 `GameplayEffectComponentConfig`：

```csharp
public abstract class GameplayEffectComponentConfig
{
    protected static EntityManager _entityManager;
    public abstract void LoadToGameplayEffectEntity(Entity ge);
}
```

#### 核心组件

| Config 类 | Component | 说明 |
|---|---|---|
| `ConfDuration` | `CDuration` | 持续时间配置 |
| `ConfPeriod` | `CPeriod` | 周期触发配置 |
| `ConfStacking` | `CStacking` | 堆叠配置 |
| `MCConfModifiers` | `MCModifiers` | 属性修改器 |
| `MCConfGrantedAbility` | `MCGrantedAbility` | 授予能力 |
| `ConfEffectBasicInfo` | `CEffectBasicInfo` | 调试名称 |

#### 标签相关组件

| Config 类 | Component |
|---|---|
| `ConfApplicationRequiredTags` | `CApplicationRequiredTags` |
| `ConfEffectImmunityTags` | `CEffectImmunityTags` |
| `ConfEffectAssetTags` | `CEffectAssetTags` |
| `ConfEffectGrantedTags` | `CEffectGrantedTags` |
| `ConfOngoingRequiredTags` | `COngoingRequiredTags` |
| `ConfRemoveEffectWithTags` | `CRemoveEffectWithTags` |

#### Cue 相关组件

| Config 类 | Component |
|---|---|
| `ConfCueOnActivate` | `CCueOnActivate` |
| `ConfCueOnDeactivate` | `CCueOnDeactivate` |
| `ConfCueOnApply` | `CCueOnApply` |
| `ConfCueOnRemove` | `CCueOnRemove` |
| `ConfCueOnAdd` | `CCueOnAdd` |
| `ConfCueOnTick` | `CCueOnTick` |

### 7.3 创建 GE 配置

```csharp
// 示例：一个带持续时间的伤害 GE
public class FireDamageGE
{
    public static GameplayEffectComponentConfig[] Create(int damageAmount, int durationFrames)
    {
        return new GameplayEffectComponentConfig[]
        {
            // 1. 基本信息
            new ConfEffectBasicInfo { Name = "FireDamage" },

            // 2. 持续时间（durational）
            new ConfDuration
            {
                duration = durationFrames,
                timeUnit = TimeUnit.Frame,
                ResetStartTimeWhenActivated = false,
                StopTickWhenDeactivated = false
            },

            // 3. 自带标签
            new ConfEffectAssetTags
            {
                tags = new NativeArray<int>(new[] { 101 }, Allocator.Persistent) // Damage.Fire
            },

            // 4. 属性修改器（Modifier）
            new MCConfModifiers
            {
                modifierSettings = new[]
                {
                    new ModifierSetting
                    {
                        AttrSetCode = 1,
                        AttrCode = 1,       // HP
                        Operation = GEOperation.Minus,  // 减少
                        Magnitude = damageAmount,
                        MMC = new MMCConfig
                        {
                            MmcType = typeof(MMCScalableFloat),
                            MmcParameter = new MmcParaFloatScale(k: 1f, b: 0f)
                        }
                    }
                }
            },

            // 5. 施加上时的 Cue
            new ConfCueOnApply
            {
                cues = CreateCueEntities(em, "CuePlaySound", new XParamPlaySound { ... })
            }
        };
    }
}
```

### 7.4 EffectService API

```csharp
var effectService = arch.GetService<EffectService>();

// 施加 GE
effectService.ApplyEffect(
    configId: 1,      // 从 ConfigModel 中查找的 GE 配置 ID
    target: enemyId,  // 目标 Carrier
    source: heroId    // 来源 Carrier
);
```

### 7.5 GE 管线（完整生命周期）

```
┌──────────────────────────────────────────────────────────────┐
│  施加阶段 (EffectService.ApplyEffect)                        │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ SInstantiateEffect (WipInstantiateEffect → 实体化)   │    │
│  └────────────────────────┬─────────────────────────────┘    │
│                           ▼                                   │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ 检查施加阶段 (SGCheckApplyEffect)                     │    │
│  │ ├─ SCheckApplicationRequiredTags (标签条件检查)       │    │
│  │ ├─ SCheckImmunityTags           (免疫标签检查)        │    │
│  │ └─ SCheckApplyEnd              (检查结果标记)         │    │
│  └────────────────────────┬─────────────────────────────┘    │
│                           ▼                                   │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ 执行施加 (SGApplyEffect)                              │    │
│  │ ├─ SPlayCueOnApply              (施加 Cue)           │    │
│  │ ├─ SRemoveEffectWithTags        (移除冲突 GE)         │    │
│  │ ├─ SApplyEnd                    (施加结束标记)        │    │
│  │ │                                                      │    │
│  │ ├─ [瞬时] SGInstantEffect                             │    │
│  │ │   ├─ SExecuteInstantEffectModifiers (执行 Modifier) │    │
│  │ │   └─ SExecuteInstantEffectEnd      (标记销毁)       │    │
│  │ │                                                      │    │
│  │ └─ [持续] SGDurationalEffect                          │    │
│  │     ├─ SAddEffectToAscBuffList      (加入 ASC 列表)   │    │
│  │     ├─ SCheckEffectStacking         (检查堆叠)        │    │
│  │     └─ SPlayCueOnAdd               (添加 Cue)         │    │
│  └──────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│  激活阶段 (SGCheckActivateEffect + SGActivateEffect)          │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ SCheckEffectActive             (检查是否可激活)       │    │
│  └────────────────────────┬─────────────────────────────┘    │
│                           ▼                                   │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ SGActivateEffect                                      │    │
│  │ ├─ SSetEffectActive             (标记 active=true)    │    │
│  │ ├─ SActivateEnd                 (激活结束标记)        │    │
│  │ ├─ SAddGrantedAbility           (授予 Ability)        │    │
│  │ ├─ SAddModifiers                (注册 Modifier)       │    │
│  │ ├─ SEffectAddGrantedTags        (添加授予标签)        │    │
│  │ ├─ SPlayCueOnActivate           (激活 Cue)            │    │
│  │ └─ SPlayCueOnTick               (Tick Cue 启动)       │    │
│  └──────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│  运行阶段 (SGRunningEffect)                                   │
│  ├─ SEffectDurationTick           (持续时间递减)              │
│  ├─ SEffectPeriodTick             (周期 GE 触发)              │
│  └─ SEffectStackingTick           (堆叠更新)                  │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│  失活阶段 (SGDeactivateEffect)                                │
│  ├─ SDeactivateEnd                (失活结束标记)              │
│  ├─ SRemoveGrantedAbility         (移除授予的 Ability)        │
│  ├─ SRemoveModifiers              (注销 Modifier)             │
│  ├─ SEffectRemoveGrantedTags      (移除授予标签)              │
│  ├─ SPlayCueOnDeactivate          (失活 Cue)                  │
│  ├─ SSetEffectDeactive            (标记 active=false)         │
│  └─ SStopCueOnTick                (停止 Tick Cue)             │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│  移除阶段 (SGRemoveEffect)                                    │
│  ├─ SEffectRemoveEnd              (移除结束标记)              │
│  ├─ SPlayCueOnRemove              (移除 Cue)                  │
│  ├─ SRemoveEffectFromAscBuffList  (从 ASC 列表中移除)         │
│  └─ SRemoveGrantedAbilityOnRemove (移除授予的 Ability)        │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│  销毁阶段 (SGEffectDestroy)                                   │
│  └─ SDestroyEffects               (DestroyEntity)             │
└──────────────────────────────────────────────────────────────┘
```

### 7.6 CDuration（持续时间）详解

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

### 7.7 CStacking（堆叠）详解

```csharp
// 堆叠类型
public enum EffectStackType
{
    AggregateBySource,  // 按来源聚合：同一来源多次施加合并为一个堆叠
    AggregateByTarget   // 按目标聚合：不同来源的 GE 合并为一个堆叠
}

// 持续时间刷新策略
public enum EffectDurationRefreshPolicy
{
    NeverRefresh,                    // 不刷新
    RefreshOnSuccessfulApplication   // 每次成功施加时刷新
}

// 周期刷新策略
public enum EffectPeriodResetPolicy
{
    NeverRefresh,                   // 不重置
    ResetOnSuccessfulApplication    // 每次成功施加时重置周期计时
}

// 过期策略
public enum EffectExpirationPolicy
{
    ClearEntireStack,                    // 清除全部堆叠
    RemoveSingleStackAndRefreshDuration, // 移除一层并刷新持续时间
    RefreshDuration                      // 仅刷新持续时间
}

// 堆叠配置
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

### 7.8 CPeriod（周期触发）详解

```csharp
public struct CPeriod : IComponentData
{
    public int Period;                        // 触发间隔
    public bool ResetTimeCountWhenDeactivated; // 失活后重新激活时是否重置计时
    public NativeArray<Entity> GameplayEffects; // 每次周期触发时执行的 GE 列表

    // 运行时
    public int StartTime;  // 周期计时起始时间
}
```

### 7.9 授予能力 (MCGrantedAbility)

```csharp
// GE 激活时自动授予 Ability，失活/移除时自动移除
public struct GrantedAbility
{
    public AbilityConfig AbilityConfig;
    public int Level;
    public GrantedAbilityActivationPolicy ActivationPolicy;     // 授予时是否自动激活
    public GrantedAbilityDeactivationPolicy DeactivationPolicy; // GE 失活时的处理
    public GrantedAbilityRemovePolicy RemovePolicy;             // GE 移除时的处理
}

public enum GrantedAbilityActivationPolicy
{
    None,           // 不自动激活
    OnGranted       // 授予时自动激活
}

public enum GrantedAbilityDeactivationPolicy
{
    None,               // 不处理
    EndAbility          // 结束能力
}

public enum GrantedAbilityRemovePolicy
{
    None,               // 不处理
    EndAbility,         // 结束能力
    CancelAbility       // 取消能力
}
```

### 7.10 GE 运行时动态组件

```csharp
// 施加阶段 WIP（Work In Progress）标记
WipInstantiateEffect    // 等待实例化
WipApplyEffect          // 等待施加
WipCheckApplyEffect     // 等待检查施加条件
WipCheckActiveEffect    // 等待检查激活条件
WipActivateEffect       // 等待激活
WipDeactivateEffect     // 等待失活
WipRemoveEffect         // 等待移除

// 运行时状态
CEffectInUsage          // 正在使用中（包含 Source 和 Target Entity）
CEffectApplied          // 已成功施加标记
CEffectInstance         // 效果实例标记
CEffectDestroy          // 等待销毁标记
CCreatedByAbility       // 由哪个 Ability 创建的
CInApplicationProgress  // 正在施加过程中
```

---

## 8. GameplayCue（表现提示）系统

GameplayCue 负责所有视觉效果、音效、UI 反馈等表现层逻辑。

### 8.1 GameplayCueBase 生命周期

```csharp
public abstract class GameplayCueBase
{
    // 注入 Entity-GameObject 解析器（在 InitParameters 之前调用）
    public void SetEntityResolver(IGASEntityResolver resolver);

    // 获取目标 ASC 对应的 GameObject（内部处理了 null 检查和过期检测）
    protected GameObject GetTargetAscGameObject();

    // 参数初始化
    public abstract void InitParameters(XParam xParam);

    // ── 生命周期回调 ──
    public virtual void OnAdd(float time);           // Cue 被添加到目标 ASC 时
    public virtual void OnRemove(float time);         // Cue 从目标 ASC 移除时
    public virtual void OnActivate(float time);       // Cue 被激活（播放）时
    public virtual void OnDeactivate(float time);     // Cue 被停止时
    public virtual void OnTick(float time);           // 每帧/每回合 Tick
    public virtual void OnDestroy(float time);        // Cue 被销毁时

    // ── 控制方法 ──
    public void Play(bool replay = false);            // 播放 Cue
    public void Stop(bool immediate = false);         // 停止 Cue
    public void StopImmediate();                      // 立即停止
    public void KillSelf();                           // 标记销毁自己
    public void RemoveSelf();                         // 停止并从目标移除
    public virtual void Reset();                      // 重置状态

    // ── 关联查询 ──
    public Entity GetEffectEntity();                  // 获取来源 GE Entity
}
```

### 8.2 GameplayCue 泛型基类

```csharp
// 带参数的 Cue 基类
public abstract class GameplayCueBase<T> : GameplayCueBase where T : XParam
{
    public T Parameter { get; private set; }

    public override void InitParameters(XParam xParam)
    {
        if (xParam is T t) Parameter = t;
    }
}
```

### 8.3 内置 Cue 实现

#### CuePlaySound —— 音效播放

```csharp
public class CuePlaySound : GameplayCueBase<XParamPlaySound>
{
    // OnAdd: 加载 AudioClip，查找/创建 AudioSource
    // OnActivate: 播放音效
    // OnTick: 非循环音效播完后自动 RemoveSelf + KillSelf
    // OnDeactivate: 停止播放
    // OnRemove: 清理资源
}

// 参数
public class XParamPlaySound : XParam
{
    public string AudioClipPath;        // Resources 路径
    public string AudioSourceNodePath;  // AudioSource 挂载节点（空=根节点）
    public float Volume = 1f;
    public float Speed = 1f;            // 播放速度（影响 pitch）
    public bool Loop;
}
```

#### CueMountPrefab —— 预制体挂载

```csharp
public class CueMountPrefab : GameplayCueBase<XParamMountPrefab>
{
    // OnAdd: 查找挂载点
    // OnActivate: 加载并实例化 Prefab
    // OnTick: 处理延迟销毁
    // OnDeactivate: 停止粒子系统，按配置销毁
    // OnRemove: 强制销毁

    // 公开接口
    public GameObject Instance { get; }
    public Transform MountPoint { get; }
    public void SetPosition(Vector3 position);
    public void SetRotation(Quaternion rotation);
    public void SetScale(Vector3 scale);
    public void PlayParticles();
    public void StopParticles();
}

// 参数
public class XParamMountPrefab : XParam
{
    public string PrefabPath;              // Resources 路径
    public string MountPointPath;          // 挂载节点路径
    public Vector3 LocalPosition;
    public Vector3 LocalRotation;
    public Vector3 LocalScale = Vector3.one;
    public bool UseWorldSpace;             // 世界坐标 vs 本地坐标
    public bool FollowHost;                // 是否跟随宿主
    public bool DestroyWithHost;           // 宿主销毁时是否跟随
    public bool DestroyOnStop;             // 停止时是否销毁
    public float DestroyDelay;             // 延迟销毁时间
    public int Layer = -1;                 // Layer（-1 不设置）
    public bool RecursiveLayer;            // 递归设置子节点 Layer
    public int SortingOrder;
    public string SortingLayerName;
    public bool AutoPlayParticle = true;   // 自动播放粒子
    public bool StopParticleOnDeactivate = true;
    public ParticleSystemStopAction ParticleStopAction; // None/Disable/Destroy
}
```

#### CuePlayAnimator —— 动画播放

```csharp
public class CuePlayAnimator : GameplayCueBase<XParamAnimator>
{
    // 控制目标 ASC GameObject 上的 Animator 组件
}

// 参数
public class XParamAnimator : XParam
{
    public string StateName;       // Animation State 名称
    public int Layer;              // Animator Layer
    public float NormalizedTime;   // 起始时间点
    public float TransitionDuration; // 过渡时间
}
```

#### CueLog / CueLogging —— 日志输出

```csharp
public class CueLog : GameplayCueBase<XParamLogging>
{
    // 在 OnActivate 时输出日志
}

public class CueLogging : GameplayCueBase<XParamLogging>
{
    // 同上（不同注册名）
}
```

### 8.4 Cue 的 ECS 组件

```csharp
// MCCue —— Cue 的管理组件
public struct MCCue : IComponentData
{
    public GameplayCueBase cue;
}

// ECCuePlayable —— 是否可播放（Enableable Component）
public struct ECCuePlayable : IComponentData, IEnableableComponent { }

// ECCuePlaying —— 是否正在播放（Enableable Component）
public struct ECCuePlaying : IComponentData, IEnableableComponent { }

// ECKillCue —— 是否标记为销毁（Enableable Component）
public struct ECKillCue : IComponentData, IEnableableComponent { }

// CPlayRequiredTags —— 播放条件标签
public struct CPlayRequiredTags : IComponentData
{
    public TagRequirementData requirement;
}

// CPlayImmunitedTags —— 播放免疫标签
public struct CPlayImmunitedTags : IComponentData
{
    public TagRequirementData requirement;
}
```

### 8.5 Cue ECS System 管线

```
SCueStart    —— 检测 ECCuePlayable && !ECCuePlaying → OnActivate + Set ECCuePlaying=true
SCueTick     —— ECCuePlaying → OnTick
SCueEnd      —— !ECCuePlayable && ECCuePlaying → OnDeactivate + Set ECCuePlaying=false
SCueDestroy  —— ECKillCue → OnDestroy + OnRemove + DestroyEntity
```

### 8.6 注册自定义 Cue

```csharp
// 方式 1：自动扫描（推荐）
// CueService.ScanAndRegisterAll() 自动扫描 Architecture 所在程序集
// 所有 GameplayCueBase 子类自动注册，注册名为类型名

// 方式 2：手动注册
var cueService = arch.GetService<CueService>();
cueService.RegisterCueType("MyCustomCue", typeof(MyCustomCue), typeof(MyCustomParam));
// 泛型版本
cueService.RegisterCueType<MyCustomCue>("MyCustomCue", typeof(MyCustomParam));

// 方式 3：通过 CueHelper 直接注册
CueHelper.RegisterCue("MyCustomCue", typeof(MyCustomCue), typeof(MyCustomParam));
```

---

## 9. Ability（能力）系统

### 9.1 Ability 的 ECS 组件

#### Static 组件（配置时设定）

| Component | 说明 |
|---|---|
| `BAbility` | Buffer——挂载在 ASC 上的 Ability 列表 |
| `CAbilityBaseInfo` | Code, Level, Owner |
| `CAbilityCooldown` | 冷却时间 + 冷却原型 GE |
| `CAbilityCost` | 消耗原型 GE |
| `MCAbilityLogic` | 能力逻辑实例 |
| `CAbilityActivationRequiredTags` | 激活时需要的标签 |
| `CAbilityActivationBlockedTags` | 激活时阻止的标签 |
| `CAbilityActivationOwnedTags` | 激活后授予的标签 |
| `CAbilityAssetTags` | Ability 自带标签 |
| `CBlockAbilityWithTags` | 激活后阻止其他 Ability 的标签 |
| `CCancelAbilityWithTags` | 激活后取消其他 Ability 的标签 |

#### Dynamic 组件（运行时标记）

| Component | 说明 |
|---|---|
| `CAbilityInTryActivate` | 请求激活标记 |
| `CAbilityInTryCancel` | 请求取消标记 |
| `CAbilityInTryEnd` | 请求结束标记 |
| `CAbilityActive` | 已激活标记 |
| `CGrantedByEffect` | 由 GE 授予的标记 |
| `MCGrantedAbilityRuntime` | GE 授予 Ability 的运行时数据 |

### 9.2 AbilityLogicBase 自定义逻辑

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
    protected Entity CreateGameplayEffectEntity(GameplayEffectComponentConfig[] configs); // 创建 GE
    protected void ApplyGameplayEffectTo(Entity ge, Entity target, Entity source);        // 施加 GE
    protected void RemoveGameplayEffect(Entity ge);            // 移除 GE
}

// 泛型版本（推荐使用）
public abstract class AbilityLogicBase<T> : AbilityLogicBase where T : XParam
{
    protected T _param;
    // 自动将 XParam 转为具体类型
}
```

### 9.3 内置 AbilityLogic

#### ALApplyEffect —— 施加效果

```csharp
public class ALApplyEffect : AbilityLogicBase<XParamEffectIDs>
{
    // ActivateAbility: 根据配置的 EffectCode 列表，创建并施加 GE 到 Owner
    // EndAbility: 移除由该 Ability 创建的所有 GE
}
```

#### ALDebugLog —— 调试日志

```csharp
public class ALDebugLog : AbilityLogicBase<XParamString>
{
    // 在各个生命周期阶段输出 Debug.Log
}
```

### 9.4 自定义 AbilityLogic 示例

```csharp
// 自定义火球能力逻辑
public class ALFireball : AbilityLogicBase<XParamFireball>
{
    public ALFireball(Entity ability, IArchitecture architecture)
        : base(ability, architecture) { }

    public ALFireball(Entity ability, EntityManager em)
        : base(ability, em) { }

    public override void ActivateAbility(GlobalTimer timer)
    {
        var owner = OwnerEntity;
        var em = _entityManager;

        // 1. 创建伤害 GE
        var damageGE = CreateGameplayEffectEntity(new GameplayEffectComponentConfig[]
        {
            new ConfEffectBasicInfo { Name = "FireballDamage" },
            new MCConfModifiers
            {
                modifierSettings = new[]
                {
                    new ModifierSetting
                    {
                        AttrSetCode = 1, AttrCode = 1, // HP
                        Operation = GEOperation.Minus,
                        Magnitude = _param.Damage,
                        MMC = new MMCConfig { MmcType = typeof(MMCScalableFloat) }
                    }
                }
            }
        });

        // 2. 对目标施加
        // (TargetCatcher 已在 Ability 配置中定义)
        ApplyGameplayEffectTo(damageGE, _param.MainTarget, owner);

        // 3. 创建火球特效 Cue
        // 通过配置中的 Cue 自动处理

        // 4. 施加消耗（扣 MP）
        if (_entityManager.HasComponent<CAbilityCost>(_abilityEntity))
        {
            var cost = _entityManager.GetComponentData<CAbilityCost>(_abilityEntity);
            var costInstance = _entityManager.Instantiate(cost.ProtoGameplayEffectCost);
            ApplyGameplayEffectTo(costInstance, owner, owner);
        }

        // 5. 施加冷却
        if (_entityManager.HasComponent<CAbilityCooldown>(_abilityEntity))
        {
            var cd = _entityManager.GetComponentData<CAbilityCooldown>(_abilityEntity);
            var cdInstance = _entityManager.Instantiate(cd.ProtoGameplayEffectCooldown);
            ApplyGameplayEffectTo(cdInstance, owner, owner);
        }
    }

    public override void CancelAbility(GlobalTimer timer)
    {
        EndAbility(timer);
    }

    public override void EndAbility(GlobalTimer timer)
    {
        // 清理该 Ability 创建的所有 GE
        var ownerAsc = GetOwnerAscEntity();
        var geEntities = _entityManager.GetBuffer<BGameplayEffect>(ownerAsc);
        foreach (var be in geEntities)
        {
            var effect = be.GameplayEffect;
            if (_entityManager.HasComponent<CCreatedByAbility>(effect))
            {
                var createdBy = _entityManager.GetComponentData<CCreatedByAbility>(effect);
                if (createdBy.sourceAbility == _abilityEntity)
                    RemoveGameplayEffect(effect);
            }
        }
        TryEndSelf();
    }

    public override void AbilityTick(GlobalTimer timer)
    {
        // 持续型 Ability 的每帧更新
    }
}

// 火球参数
public class XParamFireball : XParam
{
    [BeanField(nameof(SetDamage))]
    public float Damage { get; private set; }

    [BeanField(nameof(SetSpeed))]
    public float Speed { get; private set; }

    public Entity MainTarget;  // 运行时设定

    public void SetDamage(float v) => Damage = v;
    public void SetSpeed(float v) => Speed = v;

#if UNITY_EDITOR
    public void DecodeExcelData(List<object> paramData) { /* Luban 解析 */ }
    public List<object> EncodeExcelData() => new() { Damage, Speed };
#endif
}
```

### 9.5 AbilityService API

```csharp
var abilityService = arch.GetService<AbilityService>();

// 授予能力
abilityService.GrantAbility(
    carrier: heroId,
    abilityCode: 1001,    // 火球技能 Code
    architecture: arch
);

// 激活能力
bool success = abilityService.TryActivate(
    carrier: heroId,
    abilityCode: 1001,
    param: new XParamFireball { Damage = 50f, Speed = 10f }
);

// 检查是否激活中
bool isActive = abilityService.IsActive(heroId, 1001);

// 结束能力
abilityService.TryEnd(heroId, 1001);

// 取消能力
abilityService.TryCancel(heroId, 1001);

// 移除能力（完全移除，不仅是结束）
abilityService.RemoveAbility(heroId, 1001);

// 扫描注册（自动调用，无需手动执行）
abilityService.ScanAndRegisterAll();
```

### 9.6 AbilityLogicFactory

```csharp
// 手动注册 AbilityLogic 类型
AbilityLogicFactory.Register("ALFireball", typeof(ALFireball));

// 设置 EntityManager（通常由 AbilityService 自动设置）
AbilityLogicFactory.SetEntityManager(em);

// 创建 AbilityLogic 实例（通常由 ECS System 自动调用）
var logic = AbilityLogicFactory.TryCreateAbilityLogic("ALFireball", abilityEntity);
```

### 9.7 Ability ECS System 管线

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

### 9.8 冷却和消耗

```csharp
// 冷却 —— 配置 Cooldown GE 原型
// CAbilityCooldown 包含一个 proto GE，激活时 Instantiate 并施加到 Owner
public struct CAbilityCooldown : IComponentData
{
    public int Cooldown;                              // 冷却帧数
    public Entity ProtoGameplayEffectCooldown;        // 冷却 GE 原型
    public NativeArray<int> CooldownTags;              // 冷却标签（从 GE 的 GrantedTags 提取）
}

// 消耗 —— 配置 Cost GE 原型
// CAbilityCost 包含一个 proto GE，激活时 Instantiate 并施加到 Owner
public struct CAbilityCost : IComponentData
{
    public Entity ProtoGameplayEffectCost;             // 消耗 GE 原型
}
```

---

## 10. TargetCatcher（目标捕获）系统

TargetCatcher 负责在 Ability 激活时捕获目标 Entity 列表。

### 10.1 TargetCatcherBase

```csharp
public abstract class TargetCatcherBase
{
    public Entity Owner;                                          // 技能拥有者
    protected IGASEntityResolver _entityResolver;                // Entity-GameObject 解析器

    public virtual void Init(Entity owner);
    public void SetEntityResolver(IGASEntityResolver resolver);
    public virtual void InitParameters(XParam parameter);

    // 捕获目标（建议使用 NonAlloc 版本避免 GC）
    [Obsolete] public List<Entity> CatchTargets(Entity mainTarget);
    public void CatchTargetsNonAllocSafe(Entity mainTarget, ref List<Entity> results);
    protected abstract void CatchTargetsNonAlloc(Entity mainTarget, List<Entity> results);
}

// 泛型版本
public abstract class TargetCatcherBase<T> : TargetCatcherBase where T : XParam
{
    public T Parameter { get; private set; }
}
```

### 10.2 内置 TargetCatcher

#### CatchSelf —— 捕获自己

```csharp
public sealed class CatchSelf : TargetCatcherBase<XParamNone>
{
    // 返回 [Owner]
}
```

#### CatchTarget —— 捕获指定目标

```csharp
public sealed class CatchTarget : TargetCatcherBase<XParamNone>
{
    // 返回 [mainTarget]
}
```

#### CatchAreaBox3D —— 3D 盒形区域捕获

```csharp
public sealed class CatchAreaBox3D : TargetCatcherBase<XParamCatchAreaBox3D>
{
    // 使用 Physics.OverlapBoxNonAlloc 扫描
    // 通过 _entityResolver 或 GASEntityRef 反向查找 Entity
}

public class XParamCatchAreaBox3D : XParam
{
    public bool isWorldSpace;       // 世界坐标 vs 本地坐标
    public Vector3 offset;           // 偏移
    public Vector3 size;             // 尺寸
    public Vector3 rotation;         // 旋转
    public LayerMask layer;          // Layer 过滤
}
```

#### CatchAreaBox2D —— 2D 盒形区域捕获

```csharp
public sealed class CatchAreaBox2D : TargetCatcherBase<XParamCatchAreaBox2D>
{
    // 使用 Physics2D.OverlapBoxAll
}
```

#### CatchAreaCircle2D —— 2D 圆形区域捕获

```csharp
public sealed class CatchAreaCircle2D : TargetCatcherBase<XParamCatchAreaCircle2D>
{
    // 使用 Physics2D.OverlapCircleAll
}
```

### 10.3 TargetCatcher 注册

```csharp
// 手动注册
TargetCatcherHelper.RegisterTargetCatcher("CatchAreaBox3D", typeof(CatchAreaBox3D), typeof(XParamCatchAreaBox3D));

// 创建实例
var catcher = TargetCatcherHelper.TryCreateTargetCatcher("CatchAreaBox3D", entityResolver);
catcher.Init(ownerEntity);
catcher.InitParameters(param);

// 使用
var targets = new List<Entity>();
catcher.CatchTargetsNonAllocSafe(mainTarget, ref targets);
foreach (var target in targets)
{
    // 对每个目标执行逻辑
}
```

---

## 11. MMC（数值计算）系统

Mod Magnitude Calculation —— 动态计算 Modifier 的数值。

### 11.1 ModMagnitudeCalculationBase

```csharp
public abstract class ModMagnitudeCalculationBase
{
    public string Description;

    // 初始化参数
    public abstract void InitParameters(XParam parameter);

    // 计算数值 —— 核心方法
    // magnitude: 配置的原始数值
    // 返回: 经过计算的最终数值
    public abstract float CalculateMagnitude(MmcContext mmcContext, float magnitude);

    // 生命周期：GE 添加/移除时通知
    public void OnAddMmc(Entity gameplayEffect, EntityManager em, int targetAttrSetCode, int targetAttrCode);
    public void OnRemoveMmc();
    protected virtual void OnAdded(MmcContext context, int targetAttrSetCode, int targetAttrCode) { }
    protected virtual void OnRemoved() { }
}

// 泛型版本（推荐使用）
public abstract class ModMagnitudeCalculationBase<T> : ModMagnitudeCalculationBase where T : XParam
{
    public T Parameter { get; private set; }
}
```

### 11.2 MmcContext

```csharp
public sealed class MmcContext
{
    public Entity Source;       // 来源 ASC Entity
    public Entity Target;       // 目标 ASC Entity
    public Entity EffectEntity; // GE 自身 Entity
}
```

### 11.3 内置 MMC

#### MMCScalableFloat —— 线性缩放

```csharp
public class MMCScalableFloat : ModMagnitudeCalculationBase<MmcParaFloatScale>
{
    // magnitude = magnitude * K + B
}

public class MmcParaFloatScale : XParam
{
    public float K { get; private set; } = 1f;
    public float B { get; private set; } = 0f;
}
```

#### MMCNone —— 直通

```csharp
public class MMCNone : ModMagnitudeCalculationBase<XParamNone>
{
    // 直接返回 magnitude，不做任何修改
}
```

#### MMCAttributeBased —— 基于属性值计算

```csharp
public sealed class MMCAttributeBased : ModMagnitudeCalculationBase<AttributeBasedMmcParam>
{
    // magnitude = 指定属性值 * K + B
    // 支持 Snapshot（快照）和 Track（实时追踪）两种捕获模式
}

public class AttributeBasedMmcParam : XParam
{
    public int AttrSetCode { get; private set; }
    public int AttrCode { get; private set; }
    public AttributeFromType FromType { get; private set; }   // Source 还是 Target
    public AttributeCaptureType CaptureType { get; private set; } // Snapshot 还是 Track
    public float K { get; private set; } = 1f;
    public float B { get; private set; } = 0f;
}

public enum AttributeFromType { Source, Target }
public enum AttributeCaptureType { Track, SnapShot }
```

### 11.4 GEOperation

```csharp
public enum GEOperation
{
    Add = 0,       // currentValue + magnitude
    Minus = 3,     // currentValue - magnitude
    Multiply = 1,  // currentValue * magnitude
    Divide = 4,    // currentValue / magnitude
    Override = 2   // magnitude（覆盖）
}
```

### 11.5 自定义 MMC 示例

```csharp
// 基于目标已损失生命值的伤害计算
public class MMCExecuteBased : ModMagnitudeCalculationBase<MmcParaFloatScale>
{
    public override float CalculateMagnitude(MmcContext context, float magnitude)
    {
        // 获取目标的 HP 属性
        // 这里通过 ECS 访问需要持有 EntityManager
        // 更优雅的方式是使用 IAttributeValueResolver
        // 作为示例，返回缩放后的值
        return magnitude * Parameter.K + Parameter.B;
    }
}
```

---

## 12. Event（事件）系统

### 12.1 ECS 内部事件（GASEvents.cs）

```csharp
// GE 施加到目标
public struct GEAppliedEvent { Entity Target; Entity Source; int EffectCode; }

// GE 激活（持续型生效）
public struct GEActivatedEvent { Entity Target; int EffectCode; }

// GE 从目标移除
public struct GERemovedEvent { Entity Target; int EffectCode; }

// 属性当前值变化
public struct AttributeChangedEvent { Entity Target; int AttrSetCode; int AttrCode; float OldValue; float NewValue; }

// 属性基础值变化
public struct AttributeBaseChangedEvent { Entity Target; int AttrSetCode; int AttrCode; float OldValue; float NewValue; }

// 能力激活
public struct AbilityActivatedEvent { Entity Owner; int AbilityCode; }

// 能力结束
public struct AbilityEndedEvent { Entity Owner; int AbilityCode; }

// 能力取消
public struct AbilityCancelledEvent { Entity Owner; int AbilityCode; }

// 效果堆叠层数变化
public struct EffectStackChangedEvent { Entity EffectEntity; int OldStackCount; int NewStackCount; }
```

### 12.2 架构层事件（Events/GASEvents.cs）

```csharp
// 属性变化（NexusFramework 事件系统）
public struct GASAttributeChangedEvent
{
    public CarrierId CarrierId;
    public int AttrSetCode;
    public int AttrCode;
    public float OldValue;
    public float NewValue;
}

// GE 施加
public struct GASEffectAppliedEvent
{
    public CarrierId Target;
    public CarrierId Source;
    public int ConfigId;
}

// 能力激活
public struct GASAbilityActivatedEvent
{
    public CarrierId CarrierId;
    public int AbilityCode;
    public bool Success;
}

// Carrier 创建/销毁
public struct GASCarrierCreatedEvent { public CarrierId CarrierId; public string TypeName; }
public struct GASCarrierDestroyedEvent { public CarrierId CarrierId; }
```

### 12.3 事件桥接机制

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

**注意**：GAS 内部 ECS 事件（`GEAppliedEvent` 等）和架构层事件（`GASAttributeChangedEvent` 等）是两套独立的事件系统。架构层事件目前通过 `EventBridgeService` 桥接 ECS 事件，但也可以直接 `SendEvent<GASAttributeChangedEvent>()`。

### 12.4 事件使用

```csharp
// 监听 ECS 事件（通过架构事件系统）
arch.RegisterEvent<GEAppliedEvent>(e =>
{
    Debug.Log($"GE Applied: {e.EffectCode} → Target:{e.Target}");
});

arch.RegisterEvent<AttributeChangedEvent>(e =>
{
    Debug.Log($"Attr {e.AttrCode} changed: {e.OldValue} → {e.NewValue}");
});

arch.RegisterEvent<AbilityActivatedEvent>(e =>
{
    Debug.Log($"Ability {e.AbilityCode} activated by {e.Owner}");
});

arch.RegisterEvent<EffectStackChangedEvent>(e =>
{
    Debug.Log($"Stack changed: {e.OldStackCount} → {e.NewStackCount}");
});

// 监听架构层事件
arch.RegisterEvent<GASAttributeChangedEvent>(e =>
{
    Debug.Log($"[{e.CarrierId}] Attr {e.AttrCode}: {e.OldValue} → {e.NewValue}");
});

arch.RegisterEvent<GASEffectAppliedEvent>(e =>
{
    Debug.Log($"Effect {e.ConfigId} applied to {e.Target} from {e.Source}");
});

arch.RegisterEvent<GASAbilityActivatedEvent>(e =>
{
    string result = e.Success ? "success" : "failed";
    Debug.Log($"Ability {e.AbilityCode} activation {result}");
});
```

### 12.5 GASInternalBridge（ECS → 主线程桥接）

```csharp
// ECS System 中排队事件
GASInternalBridge.Enqueue(new AttributeChangedEvent { ... });

// 也可以排队任意 Action
GASInternalBridge.Enqueue(() =>
{
    Debug.Log("ECS System callback on main thread");
});

// 在所有 Update 之后排空
GASInternalBridge.Drain();
```

---

## 13. Config（配置）系统

### 13.1 IConfigLoader 接口

```csharp
public interface IConfigLoader : IUtility
{
    string LoadRaw(string fullPath);                                   // 读取原始文本
    GameplayEffectComponentConfig[] ParseGameplayEffect(string json);   // 解析 GE
    AbilityComponentConfig[] ParseAbility(string json);                 // 解析 Ability
    GameplayCueConfig ParseGameplayCue(string json);                   // 解析 Cue
    MMCConfig ParseMmc(string json);                                   // 解析 MMC
    TagHierarchyData ParseTagHierarchy(string json);                   // 解析标签层级
}
```

### 13.2 JsonConfigLoader

```csharp
public class JsonConfigLoader : IConfigLoader
{
    // LoadRaw: 从文件系统读取
    // ParseXxx: 从 JSON 反序列化（TODO: 部分方法待实现）
    // ParseTagHierarchy: 已实现基本版本
}
```

### 13.3 ConfigModel（配置缓存）

```csharp
var configModel = arch.GetModel<ConfigModel>();

// ── 注册配置 ──
configModel.RegisterEffect(configId, componentConfigs);
configModel.RegisterAbility(abilityCode, componentConfigs);
configModel.RegisterCues(cueConfigs);
configModel.RegisterMmcs(mmcConfigs);
configModel.RegisterTagHierarchy(tagData);

// ── 从 IConfigLoader 加载 ──
configModel.LoadEffect(loader, configId, "path/to/effect.json");
configModel.LoadAbility(loader, abilityCode, "path/to/ability.json");
configModel.LoadTags(loader, "path/to/tags.json");
configModel.LoadEffectsFromDir(loader, "Config/Effects/");      // 按目录批量加载
configModel.LoadAbilitiesFromDir(loader, "Config/Abilities/");  // 按目录批量加载

// ── 查询配置 ──
var geConfigs = configModel.GetGameplayEffectConfig(configId);
var abilityConfigs = configModel.GetAbilityConfig(abilityCode);
var cueConfig = configModel.GetGameplayCueConfig(cueId);
var mmcConfig = configModel.GetMmcConfig(mmcId);
var tagHierarchy = configModel.GetTagHierarchy();
```

### 13.4 配置数据结构

```csharp
// Cue 配置
public struct GameplayCueConfig
{
    public string CueType;       // Cue 类型名称（映射到 CueHelper 注册的类型）
    public XParam Param;         // Cue 参数
    public int[] RequiredTags;   // 播放所需标签
    public int[] ImmunityTags;   // 播放免疫标签
}

// MMC 配置
public struct MMCConfig
{
    public string MmcType;       // MMC 类型名称
    public XParam Param;         // MMC 参数
}

// 标签层级
public struct TagHierarchyData
{
    public TagNode[] Tags;
}

[Serializable]
public struct TagNode
{
    public int Code;
    public string Name;
    public int[] Children;
}
```

### 13.5 AbilityComponentConfig

```csharp
public abstract class AbilityComponentConfig
{
    protected static EntityManager _entityManager;
    public static void SetEntityManager(EntityManager em);
    public abstract void LoadToGameplayAbilityEntity(Entity ability);
}
```

### 13.6 自定义 MockConfigLoader 示例

参考测试中的实现：

```csharp
// 直接在 C# 代码中构造配置（用于测试或运行时生成）
configModel.RegisterEffect(1, new GameplayEffectComponentConfig[]
{
    new ConfDuration { duration = 50, timeUnit = TimeUnit.Frame },
    new MCConfModifiers
    {
        modifierSettings = new[]
        {
            new ModifierSetting
            {
                AttrSetCode = 1, AttrCode = 1,
                Operation = GEOperation.Add, Magnitude = 10f,
                MMC = new MMCConfig { MmcType = typeof(MMCScalableFloat) }
            }
        }
    }
});
```

---

## 14. Timer & World（时间与世界）系统

### 14.1 WorldService

```csharp
var worldService = arch.GetService<WorldService>();

// ECS World 访问
World exWorld = worldService.ExWorld;
EntityManager em = worldService.EntityManager;
bool initialized = worldService.IsInitialized;
bool running = worldService.IsRunning;

// 生命周期控制
worldService.Run();
worldService.Stop();

// 内部 SetupGASEntity —— 为 ASC Entity 添加所有必要的 Buffer/Component
worldService.SetupGASEntity(entity);
```

### 14.2 ECS SystemGroup 层级

```
InitializationSystemGroup
SimulationSystemGroup
├── FixedStepSimulationSystemGroup (RateManager: FixedRateSimpleManager)
│   └── SGLogic
│       ├── SGlobalTimer           // 全局计时器
│       ├── SGAbility
│       │   ├── STryActivateAbility
│       │   ├── STryCancelAbility
│       │   ├── STryEndAbility
│       │   └── SAbilityTick
│       ├── SGAttribute
│       │   └── SUpdateAttributeCurrentValue
│       └── SGEffect
│           ├── SGEffectCreate
│           │   └── SGInstantiateEffect → SInstantiateEffect
│           ├── SGEffectOperation
│           │   ├── SGCheckApplyEffect    → SCheckApplicationRequiredTags / SCheckImmunityTags / SCheckApplyEnd
│           │   ├── SGApplyEffect         → SPlayCueOnApply / SRemoveEffectWithTags / ... / SGInstantEffect / SGDurationalEffect
│           │   ├── SGCheckActivateEffect → SCheckEffectActive
│           │   ├── SGActivateEffect      → SSetEffectActive / SAddModifiers / SAddGrantedAbility / SEffectAddGrantedTags / ...
│           │   ├── SGDeactivateEffect    → SSetEffectDeactive / SRemoveModifiers / ...
│           │   └── SGRemoveEffect        → SRemoveEffectFromAscBuffList / ...
│           ├── SGEffectDestroy   → SDestroyEffects
│           └── SGEffectTick
│               └── SGRunningEffect → SEffectDurationTick / SEffectPeriodTick / SEffectStackingTick
└── PresentationSystemGroup
    └── SysGrpDisplay
        ├── SCueStart
        ├── SCueTick
        ├── SCueEnd
        ├── SCueDestroy
        └── SEventForwarder
```

### 14.3 TimerService & GlobalTimer

```csharp
var timerService = arch.GetService<TimerService>();

// 获取全局计时器
GlobalTimer timer = timerService.GetGlobalTimer();
int currentFrame = timer.Frame;
int currentTurn = timer.Turn;

// 也可以直接获取
int frame = timerService.CurrentFrame;
int turn = timerService.CurrentTurn;
```

### 14.4 TimeUnit

```csharp
public enum TimeUnit
{
    Frame,  // 逻辑帧（跟随 FixedStepSimulationSystemGroup）
    Turn    // 回合（手动控制 TurnController）
}
```

```csharp
// TurnController 手动回合控制
var turnCtrl = new TurnController();
turnCtrl.NextTurn();
turnCtrl.SetTurn(5);
long currentTurn = turnCtrl.CurrentTurn;
turnCtrl.ResetTurn();
```

---

## 15. 完整示例：火球技能

这是一个从创建 Architecture 到实现完整火球技能的端到端示例。

### 15.1 定义 Architecture

```csharp
public class MyGameArchitecture : GASArchitecture
{
    protected override void OnInit()
    {
        base.OnInit();

        // 注册 Carrier 类型
        GetCarrierManager().RegisterType("Hero");
        GetCarrierManager().RegisterType("Enemy");

        // 加载配置
        var configModel = GetModel<ConfigModel>();
        configModel.RegisterEffect(effectId_FireballDamage, CreateFireballDamageGE());
        configModel.RegisterEffect(effectId_FireballCooldown, CreateFireballCooldownGE());
        configModel.RegisterEffect(effectId_FireballCost, CreateFireballCostGE());
        configModel.RegisterAbility(abilityCode_Fireball, CreateFireballAbility());
    }

    // 效果 ID 常量
    private const int effectId_FireballDamage = 100;
    private const int effectId_FireballCooldown = 101;
    private const int effectId_FireballCost = 102;
    private const int abilityCode_Fireball = 1001;

    // ── 创建伤害 GE 配置 ──
    private GameplayEffectComponentConfig[] CreateFireballDamageGE()
    {
        return new GameplayEffectComponentConfig[]
        {
            new ConfDuration
            {
                duration = 0,               // 瞬时 GE（无 CDuration 即为瞬时）
                timeUnit = TimeUnit.Frame
            },
            new ConfEffectAssetTags
            {
                tags = new NativeArray<int>(new[] { 101 }, Allocator.Persistent) // Damage.Fire
            },
            new MCConfModifiers
            {
                modifierSettings = new[]
                {
                    new ModifierSetting
                    {
                        AttrSetCode = 1, AttrCode = 1, // HP
                        Operation = GEOperation.Minus,
                        Magnitude = 50f,                // 基础伤害 50
                        MMC = new MMCConfig
                        {
                            MmcType = typeof(MMCScalableFloat),
                            MmcParameter = new MmcParaFloatScale(k: 1f, b: 0f)
                        }
                    }
                }
            },
            new ConfCueOnApply
            {
                cues = CreateFireballHitCue()
            }
        };
    }

    // ── 创建冷却 GE 配置 ──
    private GameplayEffectComponentConfig[] CreateFireballCooldownGE()
    {
        return new GameplayEffectComponentConfig[]
        {
            new ConfDuration
            {
                duration = 120,             // 120 帧冷却
                timeUnit = TimeUnit.Frame,
                ResetStartTimeWhenActivated = false,
                StopTickWhenDeactivated = false
            },
            new ConfEffectGrantedTags
            {
                tags = new NativeArray<int>(new[] { 200 }, Allocator.Persistent) // Cooldown.Fireball
            }
        };
    }

    // ── 创建消耗 GE 配置 ──
    private GameplayEffectComponentConfig[] CreateFireballCostGE()
    {
        return new GameplayEffectComponentConfig[]
        {
            new MCConfModifiers
            {
                modifierSettings = new[]
                {
                    new ModifierSetting
                    {
                        AttrSetCode = 1, AttrCode = 2, // MP
                        Operation = GEOperation.Minus,
                        Magnitude = 30f,
                        MMC = new MMCConfig { MmcType = typeof(MMCScalableFloat) }
                    }
                }
            }
        };
    }

    // ── 创建 Ability 配置 ──
    private AbilityComponentConfig[] CreateFireballAbility()
    {
        return new AbilityComponentConfig[]
        {
            new ConfAbilityBaseInfo { Code = abilityCode_Fireball, Level = 1 },
            new ConfAbilityCooldown
            {
                Cooldown = 120,
                CooldownComponentConfigs = CreateFireballCooldownGE()
            },
            new ConfAbilityCost
            {
                CostComponentConfigs = CreateFireballCostGE()
            },
            new ConfAbilityActivationBlockedTags
            {
                requirement = new TagRequirementData
                {
                    any = new NativeArray<int>(new[] { 200 }, Allocator.Persistent) // 冷却中不能激活
                }
            },
            // AbilityLogic 配置
            new ConfAbilityLogic
            {
                LogicTypeName = "ALFireball"
            }
        };
    }

    // ── 命中特效 Cue ──
    private NativeArray<Entity> CreateFireballHitCue()
    {
        // 这个方法需要 EntityManager，在实际使用时通过 em 创建
        return default;
    }
}
```

### 15.2 定义 AbilityLogic

```csharp
public class ALFireball : AbilityLogicBase<XParamFireball>
{
    public ALFireball(Entity ability, IArchitecture architecture) : base(ability, architecture) { }
    public ALFireball(Entity ability, EntityManager em) : base(ability, em) { }

    public override void ActivateAbility(GlobalTimer timer)
    {
        var owner = OwnerEntity;
        var configModel = Architecture.GetModel<ConfigModel>();

        // 1. 施加消耗（扣 MP）
        if (_entityManager.HasComponent<CAbilityCost>(_abilityEntity))
        {
            var cost = _entityManager.GetComponentData<CAbilityCost>(_abilityEntity);
            var costInstance = _entityManager.Instantiate(cost.ProtoGameplayEffectCost);
            ApplyGameplayEffectTo(costInstance, owner, owner);
        }

        // 2. 施加冷却
        if (_entityManager.HasComponent<CAbilityCooldown>(_abilityEntity))
        {
            var cd = _entityManager.GetComponentData<CAbilityCooldown>(_abilityEntity);
            var cdInstance = _entityManager.Instantiate(cd.ProtoGameplayEffectCooldown);
            ApplyGameplayEffectTo(cdInstance, owner, owner);
        }

        // 3. 对目标施加伤害
        var damageConfigs = configModel.GetGameplayEffectConfig(effectId: 100); // FireballDamage
        var damageGE = CreateGameplayEffectEntity(damageConfigs);
        ApplyGameplayEffectTo(damageGE, _param.MainTarget, owner);
    }

    public override void CancelAbility(GlobalTimer timer)
    {
        EndAbility(timer);
    }

    public override void EndAbility(GlobalTimer timer)
    {
        // 清理该 Ability 创建的所有 GE
        var ownerAsc = GetOwnerAscEntity();
        var geEntities = _entityManager.GetBuffer<BGameplayEffect>(ownerAsc);
        foreach (var be in geEntities)
        {
            var effect = be.GameplayEffect;
            if (_entityManager.HasComponent<CCreatedByAbility>(effect))
            {
                var createdBy = _entityManager.GetComponentData<CCreatedByAbility>(effect);
                if (createdBy.sourceAbility == _abilityEntity)
                    RemoveGameplayEffect(effect);
            }
        }
    }

    public override void AbilityTick(GlobalTimer timer) { }
}
```

### 15.3 游戏层使用

```csharp
public class FireballSkill : MonoBehaviour
{
    private MyGameArchitecture _arch;
    private CarrierId _heroId;
    private CarrierId _enemyId;

    void Start()
    {
        // 初始化架构
        _arch = new MyGameArchitecture();
        _arch.Initialize();

        // 创建 Hero
        var heroGo = GameObject.Find("Hero");
        _heroId = _arch.CreateGASCarrier("Hero", heroGo);

        // 初始化 Hero 属性
        InitHeroAttributes();

        // 创建 Enemy
        var enemyGo = GameObject.Find("Enemy");
        _enemyId = _arch.CreateGASCarrier("Enemy", enemyGo);
        InitEnemyAttributes();

        // 授予能力
        var abilityService = _arch.GetService<AbilityService>();
        abilityService.GrantAbility(_heroId, abilityCode: 1001, _arch);

        // 监听事件
        _arch.RegisterEvent<GASAttributeChangedEvent>(OnAttrChanged);
        _arch.RegisterEvent<GASAbilityActivatedEvent>(OnAbilityActivated);
    }

    void InitHeroAttributes()
    {
        var em = _arch.GetService<WorldService>().EntityManager;
        var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(_heroId);
        var buf = em.GetBuffer<BEAttrSet>(entity);

        var attrs = new NativeArray<CAttributeData>(3, Allocator.Persistent);
        attrs[0] = new CAttributeData { Code = 1, BaseValue = 500f, CurrentValue = 500f, IsClampMin = true, MinValue = 0f, IsClampMax = true, MaxValue = 9999f }; // HP
        attrs[1] = new CAttributeData { Code = 2, BaseValue = 200f, CurrentValue = 200f, IsClampMin = true, MinValue = 0f, IsClampMax = true, MaxValue = 9999f }; // MP
        attrs[2] = new CAttributeData { Code = 3, BaseValue = 40f, CurrentValue = 40f }; // Attack
        buf.Add(new BEAttrSet { Code = 1, Attributes = attrs });
    }

    void InitEnemyAttributes()
    {
        var em = _arch.GetService<WorldService>().EntityManager;
        var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(_enemyId);
        var buf = em.GetBuffer<BEAttrSet>(entity);

        var attrs = new NativeArray<CAttributeData>(1, Allocator.Persistent);
        attrs[0] = new CAttributeData { Code = 1, BaseValue = 200f, CurrentValue = 200f, IsClampMin = true, MinValue = 0f, IsClampMax = true, MaxValue = 9999f }; // HP
        buf.Add(new BEAttrSet { Code = 1, Attributes = attrs });
    }

    void Update()
    {
        // ECS World 会在架构初始化时自动加入 player loop
        // 也可以通过 WorldService 手动控制

        if (Input.GetKeyDown(KeyCode.Q))
        {
            CastFireball();
        }
    }

    void CastFireball()
    {
        var abilityService = _arch.GetService<AbilityService>();

        // 查找目标（通过 TargetCatcher 或手动指定）
        var entityMap = _arch.GetModel<GASEntityMapModel>();
        var enemyEntity = entityMap.GetGASEntity(_enemyId);

        // 激活能力
        bool success = abilityService.TryActivate(
            _heroId,
            abilityCode: 1001,
            param: new XParamFireball { Damage = 50f, Speed = 10f, MainTarget = enemyEntity }
        );

        Debug.Log(success ? "Fireball cast!" : "Cannot cast - on cooldown or blocked");
    }

    void OnAttrChanged(GASAttributeChangedEvent e)
    {
        Debug.Log($"[{e.CarrierId}] Attr {e.AttrCode}: {e.OldValue:F0} → {e.NewValue:F0}");

        // UI 更新
        if (e.CarrierId.Equals(_heroId) && e.AttrSetCode == 1)
        {
            switch (e.AttrCode)
            {
                case 1: UpdateHpBar(e.NewValue); break;
                case 2: UpdateMpBar(e.NewValue); break;
            }
        }
    }

    void OnAbilityActivated(GASAbilityActivatedEvent e)
    {
        Debug.Log($"Ability {e.AbilityCode} {(e.Success ? "activated" : "failed")}");
    }

    void OnDestroy()
    {
        _arch?.Dispose();
    }
}
```

### 15.4 Command 方式调用（可选）

```csharp
// 使用 Command 模式施加效果
arch.SendCommand(new ApplyEffectCommand
{
    ConfigId = 100,   // FireballDamage
    Target = enemyId,
    Source = heroId
});

// 使用 Command 模式激活能力
arch.SendCommand(new ActivateAbilityCommand
{
    Carrier = heroId,
    AbilityCode = 1001,
    Param = new XParamFireball { Damage = 50f, MainTarget = enemyEntity }
});
```

---

## 附录 A：XParam 类型参考

| XParam 类型 | 用途 | 关键字段 |
|---|---|---|
| `XParamNone` | 无参数 | - |
| `XParamBool` | 布尔参数 | `Value` |
| `XParamInt` | 整数参数 | `Value` |
| `XParamFloat` | 浮点参数 | `Value` |
| `XParamString` | 字符串参数 | `Value` |
| `XParamVector2` | 2D 向量 | `Value` |
| `XParamVector3` | 3D 向量 | `Value` |
| `XParamArrayInt` | 整数数组 | `Values` |
| `XParamArrayFloat` | 浮点数组 | `Values` |
| `XParamAnimator` | 动画参数 | `StateName`, `Layer`, `NormalizedTime` |
| `XParamPlaySound` | 音效参数 | `AudioClipPath`, `Volume`, `Speed`, `Loop` |
| `XParamMountPrefab` | 预制体参数 | `PrefabPath`, `MountPointPath`, `LocalPosition`, ... |
| `XParamLogging` | 日志参数 | `Message` |
| `XParamCue` | 通用 Cue 参数 | - |
| `XParamCueIDs` | Cue ID 列表 | `IDs` |
| `XParamEffectIDs` | Effect ID 列表 | `IDs` |
| `XParamApplyEffects` | 施加 Effect 参数 | - |
| `XParamMMCScalable` | MMC 缩放参数 | `K`, `B` |
| `XParamCatchAreaBox3D` | 3D 区域捕获 | `isWorldSpace`, `offset`, `size`, `rotation`, `layer` |

### XParam 接口

```csharp
public interface XParam
{
#if UNITY_EDITOR
    void DecodeExcelData(List<object> paramData);    // Luban Excel → 参数
    List<object> EncodeExcelData();                  // 参数 → Luban Excel
#endif
}
```

### BeanField / BeanPolymorphicField

```csharp
// 用于编辑器反射，支持 Luban Excel 列映射
[BeanField(nameof(SetDamage), Comment = "伤害值", Order = 1)]
public float Damage { get; private set; }

[BeanPolymorphicField(nameof(SetTargetCatcher), Comment = "目标捕获类型", Order = 2)]
public string TargetCatcherType { get; private set; }
```

---

## 附录 B：ECS Component 速查表

### Buffer 类型（挂载在 ASC Entity 上）

| Buffer | Element Type | 说明 |
|---|---|---|
| `BEAttrSet` | AttributeSet with NativeArray | 属性集列表 |
| `BGameplayEffect` | Entity | 活跃 GE Entity 列表 |
| `BAbility` | Entity | 已授予 Ability Entity 列表 |
| `BFixedTag` | int | 固有标签列表 |
| `BTemporaryTag` | int + Entity source | 临时标签列表（带来源） |

### Static Component（配置时设定）

| Component | 说明 |
|---|---|
| `CAbilityBaseInfo` | 能力基本信息和 Owner |
| `CAbilityCooldown` | 冷却配置 |
| `CAbilityCost` | 消耗配置 |
| `MCAbilityLogic` | 能力逻辑实例 |
| `CDuration` | 持续时间 |
| `CPeriod` | 周期触发配置 |
| `CStacking` | 堆叠配置 |
| `MCModifiers` | 属性修改器列表 |
| `MCGrantedAbility` | 授予的能力 |
| `CEffectBasicInfo` | GE 基本信息/名称 |
| `CEffectAssetTags` | GE 自带标签 |
| `CEffectGrantedTags` | GE 授予标签 |
| `CApplicationRequiredTags` | 施加条件标签 |
| `CEffectImmunityTags` | 免疫标签 |
| `COngoingRequiredTags` | 持续条件标签 |
| `CRemoveEffectWithTags` | 通过标签移除 GE |
| `CCueOnActivate/Deactivate/Apply/Remove/Add/Tick` | 各阶段 Cue |
| `CBlockAbilityWithTags` | 通过标签阻止 Ability |
| `CCancelAbilityWithTags` | 通过标签取消 Ability |
| `CAscBasicData` | ASC 基础数据 |

### Dynamic Component（运行时标记）

| Component | 说明 |
|---|---|
| `CAbilityActive` | Ability 已激活 |
| `CAbilityInTryActivate` | 请求激活 |
| `CAbilityInTryCancel` | 请求取消 |
| `CAbilityInTryEnd` | 请求结束 |
| `CGrantedByEffect` | 由 GE 授予 |
| `MCGrantedAbilityRuntime` | 授予 Ability 的运行时数据 |
| `CEffectInUsage` | GE 使用中（Source + Target） |
| `CEffectApplied` | GE 已施加 |
| `CEffectInstance` | GE 实例标记 |
| `CEffectDestroy` | 请求销毁 GE |
| `CCreatedByAbility` | 由哪个 Ability 创建 |
| `CInApplicationProgress` | 正在施加过程中 |
| `CAttributeIsDirty` | 属性需要重算 |
| `WipXxx` 系列 | 管线 Work-In-Progress 标记 |

---

## 附录 C：已知限制

1. **ConfigLoader 部分未实现**：`JsonConfigLoader.ParseGameplayEffect/ParseAbility/ParseGameplayCue/ParseMmc` 方法返回 null/默认值
2. **CueHelper.GetCueTypeCode / GetCueLogicParamType(int)** 返回 0/null（待实现配置索引查询）
3. **GeneralGasChoiceHelper.AttrSets()** 返回空列表（待实现）
4. **SCheckImmunityTags 的免疫 Cue 触发** 逻辑未实现
5. **SCheckApplicationCondition 的应用条件判断** 逻辑未实现
6. **JsonConfigLoader 的反序列化** 依赖 Luban 工具链（Excel → JSON），框架本身不包含配置导出逻辑
7. **GAS 模块依赖 Unity DOTS 全家桶**（Entities, Mathematics, Collections, Burst），Core 无外部依赖
