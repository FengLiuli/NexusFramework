# NexusFramework UML 模型

> 本文档基于代码实况生成，覆盖 `NexusFramework`（框架层）、`NexusFramework.GAS`（技能系统层）与二者共享的 Unity ECS 承载层。
> 图表使用 Mermaid，可在支持 Mermaid 的 Markdown 渲染器（VS Code、GitHub、Typora）中直接查看。

---

## 1. 总览：模块分层

自下而上四层，每层只依赖其下层：

```mermaid
graph BT
    subgraph L0["L0 · 宿主"]
        UNITY["Unity 运行时<br/>GameObject / ScriptableObject"]
    end

    subgraph L1["L1 · 框架层 — NexusFramework"]
        ARCH["Architecture<br/>IOC + 事件 + 生命周期"]
        CARRIER["CarrierManager<br/>载体 / 特征"]
        TAGSVC["CarrierTagService<br/>托管 Tag 实现"]
    end

    subgraph L2["L2 · 技能层 — NexusFramework.GAS"]
        GASARCH["GASArchitecture"]
        SVCS["Service × 8<br/>World / Effect / Ability / Cue<br/>Attribute / Timer / EventBridge"]
        MODELS["Model × 2<br/>GASEntityMap / Config"]
        ECSTAG["EcsTagService<br/>ECS Tag 实现"]
    end

    subgraph L3["L3 · ECS 承载层 — Unity.Entities"]
        WORLD["World: GAS_World"]
        SYS["SystemGroup 树<br/>SGLogic → SGAbility / SGEffect / ..."]
        COMP["Component / Buffer<br/>B* / C* / Wip* / E*"]
    end

    L1 --> L0
    L2 --> L1
    L3 --> L0
    L2 --> L3
    GASARCH -.覆盖 CreateTagService.-> ECSTAG
    ARCH -.默认.-> TAGSVC

    style L1 fill:#e8f4f8
    style L2 fill:#fff4e6
    style L3 fill:#f0e8f8
```

**关键设计决策**：
- 框架层不依赖 ECS。`ITagService` 是抽象契约，`Architecture.CreateTagService()` 是虚方法——GAS 模块覆盖它把 Tag 的真相源从托管字典换成 ECS。
- 两套 Tag 实现共存：非 GAS 场景用 `CarrierTagService`（纯托管字典），GAS 场景用 `EcsTagService`（ECS 为真相源）。

---

## 2. 框架层类图

### 2.1 架构内核

```mermaid
classDiagram
    class IArchitecture {
        <<interface>>
        +byte ArchitectureId
        +string ArchitectureType
        +ArchitectureState State
        +Initialize() void
        +Pause() void
        +Resume() void
        +Shutdown() void
        +RegisterService~T~(T) void
        +RegisterModel~T~(T) void
        +RegisterUtility~T~(T) void
        +GetService~T~() T
        +GetModel~T~() T
        +GetUtility~T~() T
        +SendCommand~T~(T) void
        +SendCommand~TResult~(ICommand~TResult~) TResult
        +SendQuery~TResult~(IQuery~TResult~) TResult
        +SendEvent~T~(T) void
        +RegisterEvent~T~(Action~T~) IUnRegister
        +GetCarrierManager() ICarrierManager
        +GetTagService() ITagService
    }

    class Architecture {
        <<abstract>>
        -IOCContainer mContainer
        -TypeEventService mTypeEventService
        -CarrierManager mCarrierManager
        -ITagService mTagService
        #OnInit()* void
        #OnInitialized() void
        #CreateTagService() ITagService
        #OnPause() void
        #OnResume() void
        #OnShutdown() void
    }

    class ArchitectureFactory {
        <<static>>
        -Dictionary~string,Type~ AllArchitectureTypes
        -Dictionary~byte,IArchitecture~ ArchitectureInstances
        +RegisterArchitecture~T~(string) void
        +CreateArchitecture(string, byte?) IArchitecture
        +GetArchitecture~T~() T
        +DestroyArchitecture(byte) bool
        +Reset() void
    }

    class IOCContainer {
        -Dictionary~Type,ICanInit~ mInstances
        +Register~T~(T) void
        +Get~T~() T
        +InitAll() void
        +DeInitAll() void
    }

    class TypeEventService {
        -Dictionary~Type,List~object~~ mEventListeners
        +Register~T~(Action~T~) IUnRegister
        +Send~T~(T) void
        +UnRegister~T~(Action~T~) void
        +Clear() void
    }

    class ArchitectureState {
        <<enumeration>>
        NotInitialized
        Initializing
        Initialized
        Paused
        Shutting
        Shutdown
    }

    IArchitecture <|.. Architecture
    Architecture *-- IOCContainer : 组合
    Architecture *-- TypeEventService : 组合
    Architecture --> ArchitectureState : 状态
    ArchitectureFactory ..> Architecture : 反射创建
    ArchitectureFactory o-- IArchitecture : 持有实例
```

### 2.2 生命周期钩子与事件

`Initialize()` 的严格次序——注释中明确指出 `OnInitialized()` 必须在 `ArchitectureAfterInitEvent` **之前**调用，否则订阅者会读到不完整状态（如 GAS 的 Tag 层级尚未烘焙）。

```mermaid
sequenceDiagram
    autonumber
    participant F as ArchitectureFactory
    participant A as Architecture
    participant C as IOCContainer
    participant CM as CarrierManager
    participant TS as ITagService
    participant Sub as 事件订阅者

    F->>A: Activator.CreateInstance(type, instanceId)
    F->>A: Initialize()
    A->>A: State = Initializing
    A->>Sub: SendEvent(ArchitectureBeforeInitEvent)
    A->>C: Register(mCarrierManager)
    A->>CM: SetEventCallback(CarrierManagerEventBridge)
    Note over A,TS: CreateTagService() 虚方法分派<br/>GAS 覆盖为 EcsTagService
    A->>TS: new CarrierTagService(CM) / new EcsTagService(this)
    A->>CM: SetTagService(TS)
    A->>TS: SetEventCallback(bridge)
    A->>C: Register(TS)
    A->>A: OnInit()
    Note over A: 子类注册 Service / Model / Utility
    A->>C: InitAll()
    Note over C: 幂等：同一实例多 key 注册只 Init 一次
    A->>A: OnInitialized()
    Note over A: GAS 在此烘焙 Tag 层级
    A->>A: State = Initialized
    A->>Sub: SendEvent(ArchitectureAfterInitEvent)
    F->>F: ArchitectureInstances[id] = instance
```

### 2.3 规则接口（能力组合）

框架用「小接口 + 扩展方法」实现能力隔离：`IController` 拿到全部能力，`IQuery` 只能只读访问载体。

```mermaid
classDiagram
    direction LR
    class IBelongToArchitecture {
        <<interface>>
        +IArchitecture Architecture
    }
    class ICanInit {
        <<interface>>
        +bool Initialized
        +Init() void
        +Deinit() void
    }
    class ICanGetService { <<interface>> }
    class ICanGetModel { <<interface>> }
    class ICanGetUtility { <<interface>> }
    class ICanSendCommand { <<interface>> }
    class ICanSendQuery { <<interface>> }
    class ICanSendEvent { <<interface>> }
    class ICanRegisterEvent { <<interface>> }
    class ICanGetCarrier { <<interface>> }
    class ICanCreateCarrier { <<interface>> }
    class ICanManageCarrierTrait { <<interface>> }
    class ICanGetCarrierTag { <<interface>> }
    class ICanManageCarrierTag { <<interface>> }

    ICanGetService --|> IBelongToArchitecture
    ICanGetModel --|> IBelongToArchitecture
    ICanGetUtility --|> IBelongToArchitecture
    ICanSendCommand --|> IBelongToArchitecture
    ICanSendQuery --|> IBelongToArchitecture
    ICanSendEvent --|> IBelongToArchitecture
    ICanRegisterEvent --|> IBelongToArchitecture
    ICanGetCarrier --|> IBelongToArchitecture
    ICanCreateCarrier --|> IBelongToArchitecture
    ICanManageCarrierTrait --|> ICanGetCarrier
    ICanGetCarrierTag --|> ICanGetCarrier
    ICanManageCarrierTag --|> ICanGetCarrierTag
```

**角色 → 能力对照表**（代码中的实际组合）：

| 角色 | 能力构成 | 载体访问权限 |
|---|---|---|
| `IController` | Command + Service + Model + Event + Query + Utility | **完整**：读 / 建 / 改 Trait + Tag 读写 |
| `IService` | Model + Utility + Event + `ICanInit` | **完整**：读 / 建 / 改 Trait + Tag 读写 |
| `ICommand` / `ICommand<T>` | Service + Model + Utility + Event + Command + Query | **完整**：读 / 建 / 改 Trait + Tag 读写 |
| `IModel` | Utility + Event + `ICanInit` | **仅 Tag 只读**（`ICanGetCarrierTag`） |
| `IQuery<T>` | Model + Service + Query + Utility | **只读**：`ICanGetCarrier` + Tag 只读 |
| `IUtility` | `ICanInit` | 无 |

对应的抽象基类：`AbstractService` / `AbstractModel` / `AbstractModelSO` / `AbstractCommand` / `AbstractCommand<T>` / `AbstractQuery<T>`。

### 2.4 数据载体（Carrier）子系统

```mermaid
classDiagram
    class CarrierId {
        <<struct>>
        +byte FrameworkId
        +ushort TypeId
        +ulong UniqueId
        +ulong RawValue
        +bool IsValid
        +Invalid$ CarrierId
    }
    note for CarrierId "64 位位域：<br/>高 8 位 FrameworkId<br/>中 16 位 TypeId<br/>低 40 位 UniqueId"

    class IDataTrait {
        <<interface>>
        +string TraitTypeName
        +Clone() IDataTrait
        +ToJson() string
        +FromJson(string) void
    }

    class DataTrait {
        <<abstract>>
        +TraitTypeName string
        +Clone() IDataTrait
        +ToJson() string
        +FromJson(string) void
    }

    class ScriptableObjectDataTrait {
        <<abstract>>
    }

    class BasicInfoTrait {
        +string name
        +string description
        +string iconPath
        +bool isActive
    }

    class ICarrierManager {
        <<interface>>
        +int TotalCarrierCount
        +int TotalTraitCount
        +RegisterType(string) ushort
        +CreateCarrier(ushort) CarrierId
        +CreateCarrier(ushort, ulong) bool
        +DestroyCarrier(CarrierId) bool
        +IsCarrierValid(CarrierId) bool
        +AddTrait~T~(CarrierId, T) bool
        +RemoveTrait~T~(CarrierId) bool
        +GetTrait~T~(CarrierId) T
        +FindCarriersWithTrait~T~() IEnumerable
        +FindCarriersWithTraits(Type[]) IEnumerable
        +SerializeCarrier(CarrierId) string
        +DeserializeCarrier(string) CarrierId
        +SaveToFile(string) void
        +LoadFromFile(string) void
    }

    class CarrierManager {
        -Dictionary~CarrierId,Dictionary~Type,IDataTrait~~ carrierTraits
        -Dictionary~ushort,HashSet~CarrierId~~ carriersByType
        -Dictionary~Type,HashSet~CarrierId~~ traitReverseIndex
        -ICarrierEventCallback eventCallback
        -ITagService tagService
    }

    class ICarrierEventCallback {
        <<interface>>
        +OnTagAdded(CarrierId, string) void
        +OnTagRemoved(CarrierId, string) void
        +OnCarrierDestroyed(CarrierId) void
    }

    class CarrierManagerEventBridge {
        -Architecture architecture
    }

    class CarrierTagEvents {
        <<struct>>
    }
    class CarrierLifecycleEvents {
        <<struct>>
    }

    IDataTrait <|.. DataTrait
    DataTrait <|-- ScriptableObjectDataTrait
    DataTrait <|-- BasicInfoTrait
    ICarrierManager <|.. CarrierManager
    CarrierManager --> CarrierId : 键
    CarrierManager --> IDataTrait : 持有
    CarrierManager ..> ICarrierEventCallback : 广播
    ICarrierEventCallback <|.. CarrierManagerEventBridge
    CarrierManagerEventBridge ..> Architecture : SendEvent
    CarrierManagerEventBridge ..> CarrierTagEvents
    CarrierManagerEventBridge ..> CarrierLifecycleEvents
```

**设计要点**：
- `CarrierManager` **不持有 Tag 状态**。Tag 由独立的 `ITagService` 持有，二者通过 `ICarrierEventCallback` 解耦——`CarrierManager` 销毁载体时锁外广播 `OnCarrierDestroyed`，接收方在**自己的锁内**清理索引，避免锁嵌套（代码注释明确记录了这个死锁面考量）。
- 序列化时 `SerializeCarrier` 从 `tagService` 读取 Tag，且**必须在进入本类锁之前取完**——同样是为了避免与 Tag 服务的锁嵌套。
- `traitReverseIndex` 是 Trait 类型 → 载体的反向索引，用于 `FindCarriersWithTrait` 的 O(1) 查询。

### 2.5 Tag 子系统（双实现）

```mermaid
classDiagram
    class ITagService {
        <<interface>>
        +OnCarrierDestroyed(CarrierId) void
        +IsCarrierKnown(CarrierId) bool
        +SetEventCallback(ICarrierEventCallback) void
        +InternTag(string) int
        +GetTagKey(int) string
        +TryGetTagId(string, out int) bool
        +MatchesOrIsAncestor(int, int) bool
        +AddTag(CarrierId, string) bool
        +RemoveTag(CarrierId, string) bool
        +AddTags(CarrierId, IEnumerable) int
        +RemoveTags(CarrierId, IEnumerable) int
        +RestoreTags(CarrierId, IEnumerable) int
        +HasExactTag(CarrierId, string) bool
        +HasTag(CarrierId, string) bool
        +HasAllTags(CarrierId, IEnumerable) bool
        +HasAnyTag(CarrierId, IEnumerable) bool
        +HasNoneTags(CarrierId, IEnumerable) bool
        +GetTags(CarrierId) IEnumerable
        +GetTagCount(CarrierId) int
        +FindCarriersWithTag(string) IEnumerable
        +FindCarriersWithTags(TagQuery) IEnumerable
    }

    class CarrierTagService {
        -TagRuntimeIndex tagIndex
        -Dictionary~CarrierId,HashSet~RuntimeTagSlot~~ tagsByCarrier
        -Dictionary~RuntimeTagSlot,HashSet~CarrierId~~ carriersBySlot
        -HashSet~CarrierId~ knownCarriers
    }

    class EcsTagService {
        -Dictionary~string,int~ mIdByKey
        -Dictionary~int,string~ mKeyById
        +RebuildTagMap() void
    }

    class TagRuntimeIndex {
        -Dictionary~string,RuntimeTagSlot~ 键到槽位
        +Intern(string) RuntimeTagSlot
        +TryGetSlot(...) bool
        +MatchesOrIsAncestor(...) bool
    }

    class RuntimeTagSlot {
        <<struct>>
        +int Value
    }

    class TagKey {
        <<static>>
        +Normalize(string) string
    }

    class TagQuery {
        +IReadOnlyList~string~ All
        +IReadOnlyList~string~ Any
        +IReadOnlyList~string~ None
        +bool IsEmpty
    }

    class SingletonGameplayTagMap {
        <<ECS IComponentData>>
        +NativeHashMap Map
    }

    ITagService <|.. CarrierTagService
    ITagService <|.. EcsTagService
    CarrierTagService *-- TagRuntimeIndex
    TagRuntimeIndex --> RuntimeTagSlot
    CarrierTagService --> RuntimeTagSlot : 索引键
    CarrierTagService ..> TagKey : 归一化
    CarrierTagService ..> TagQuery : 条件反查
    EcsTagService ..> SingletonGameplayTagMap : 烘焙写入
```

**两套实现的语义差异**：

| 维度 | `CarrierTagService`（框架默认） | `EcsTagService`（GAS 覆盖） |
|---|---|---|
| 槽位来源 | `TagRuntimeIndex` intern，运行时动态分配 | 配置表 `exgas.gameplayTags` 的 Tag ID |
| 状态真相源 | 托管字典 `tagsByCarrier` + `carriersBySlot` | ECS `BFixedTag` / `BTemporaryTag` Buffer |
| 层级推导 | 点分路径运行时推导祖先链 | 启动期烘焙进 `SingletonGameplayTagMap`，Burst 热路径可读 |
| `OnCarrierDestroyed` | 清理正向 + 反向索引残留 | 空实现（无托管账本，ECS 为准） |
| 未配置的 Tag 键 | 自动创建槽位 | `InternTag` 返回 0（键必须已在配置表） |

**`RestoreTags` 的特殊性**：反序列化专用，批量写入但**不触发** `TagAdded` 事件——加载存档不应引发一连串变更广播。两条路径（`CarrierManager.DeserializeCarrier` → `tagService.RestoreTags`）都遵守该约定。

---

## 3. GAS 层类图

### 3.1 GASArchitecture 与服务/模型

```mermaid
classDiagram
    class Architecture {
        <<abstract>>
    }
    class GASArchitecture {
        <<abstract>>
        +string ArchitectureType = "GAS"
        #CreateConfigLoader() IConfigLoader
        #CreateTagService() ITagService
        #OnInit() void
        #OnInitialized() void
        #OnShutdown() void
        +CreateGASCarrier(string, GameObject) CarrierId
        +CreateGASCarrier(string, int, GameObject) CarrierId
        +BindGameObjectForCarrier(CarrierId, GameObject) void
        +DestroyGASCarrier(CarrierId) void
    }

    class AbstractService {
        <<abstract>>
    }
    class AbstractModel {
        <<abstract>>
    }

    class WorldService {
        +World ExWorld
        +EntityManager EntityManager
        +bool IsRunning
        +Run() void
        +Stop() void
        #CreateSystems() void
    }
    class TimerService
    class EventBridgeService {
        -Dispatch(object) void
    }
    class EffectService {
        +ApplyEffect(int, CarrierId, CarrierId) void
    }
    class AbilityService {
        -Dictionary~CarrierId,List~int~~ _grantedAbilities
        +ScanAndRegisterAll() void
        +GrantAbility(CarrierId, int, IArchitecture) void
        +TryActivate(CarrierId, int, XParam) bool
        +TryEnd(CarrierId, int) void
        +TryCancel(CarrierId, int) void
        +RemoveAbility(CarrierId, int) void
        +IsActive(CarrierId, int) bool
    }
    class CueService
    class AttributeService
    class EcsTagService {
        +RebuildTagMap() void
    }

    class GASEntityMapModel {
        -Dictionary~CarrierId,Entity~ _carrierToEntity
        -Dictionary~Entity,GameObject~ _entityToGameObject
        +Bind(CarrierId, Entity) void
        +GetGASEntity(CarrierId) Entity
        +GetCarrierId(Entity) CarrierId
        +BindGameObject(Entity, GameObject) void
        +GetGameObject(Entity) GameObject
    }
    class ConfigModel {
        +GetAscConfig(int) AscConfigData?
        +GetAbilityConfig(int) AbilityConfig[]
        +GetGameplayEffectConfig(int) GameplayEffectComponentConfig[]
        +GetAttrSetDef(int) AttrSetDef?
        +GetCueConfig(int) GameplayCueConfig?
        +GetTagHierarchy() TagHierarchyData
    }

    class IConfigLoader {
        <<interface>>
    }
    class JsonConfigLoader
    class IGASEntityResolver {
        <<interface>>
        +GetGameObject(Entity) GameObject
        +GetEntity(GameObject) Entity
    }

    Architecture <|-- GASArchitecture
    AbstractService <|-- WorldService
    AbstractService <|-- TimerService
    AbstractService <|-- EventBridgeService
    AbstractService <|-- EffectService
    AbstractService <|-- AbilityService
    AbstractService <|-- CueService
    AbstractService <|-- AttributeService
    AbstractService <|-- EcsTagService
    AbstractModel <|-- GASEntityMapModel
    AbstractModel <|-- ConfigModel
    IConfigLoader <|.. JsonConfigLoader
    IGASEntityResolver <|.. GASEntityMapModel
    GASArchitecture ..> WorldService : 注册
    GASArchitecture ..> EcsTagService : 覆盖创建
    GASArchitecture ..> ConfigModel : 注册
    AbilityService ..> GASEntityMapModel : CarrierId→Entity
    EffectService ..> GASEntityMapModel : CarrierId→Entity
    EffectService ..> ConfigModel : 读配置
    AbilityService ..> ConfigModel : 读配置
    WorldService ..> IGASEntityResolver : 供 Cue 解析 GO
```

### 3.2 ConfigModel 装载的配置类型

```mermaid
classDiagram
    class IConfigLoader {
        <<interface>>
    }
    class AscConfigData {
        <<struct>>
        +int Level
        +int[] Tags
        +int[] AttrSetIds
        +int[] AbilityIds
    }
    class AttrSetDef {
        <<struct>>
        +AttrInitDef[] Attributes
    }
    class AttrInitDef {
        <<struct>>
        +int Code
        +float InitValue
        +bool UseMinValue
        +bool UseMaxValue
        +float MinValue
        +float MaxValue
    }
    class GameplayCueConfig {
        <<struct>>
    }
    class MMCConfig {
        <<struct>>
    }
    class TagHierarchyData {
        <<struct>>
        +TagNode[] Tags
    }
    class TagNode {
        <<struct>>
        +int Code
        +string Name
    }

    IConfigLoader ..> AscConfigData
    IConfigLoader ..> AttrSetDef
    AttrSetDef *-- AttrInitDef
    IConfigLoader ..> GameplayCueConfig
    IConfigLoader ..> MMCConfig
    IConfigLoader ..> TagHierarchyData
    TagHierarchyData *-- TagNode
```

---

## 4. ECS 层模型

### 4.1 组件分类（按生命周期语义）

```mermaid
classDiagram
    direction TB

    class IComponentData { <<interface>> }
    class IBufferElementData { <<interface>> }
    class IEnableableComponent { <<interface>> }

    %% ── ASC 主体 ──
    class CAscBasicData {
        +int Level
    }
    class BFixedTag {
        +int tag
    }
    class BTemporaryTag {
        +int tag
    }
    class BGameplayEffect {
        +Entity Effect
    }
    class BAbility {
        +Entity Ability
    }
    class BEAttrSet {
        +int Code
        +NativeArray~CAttributeData~ Attributes
    }
    class CAttributeData {
        +int Code
        +float BaseValue
        +float CurrentValue
        +float MinValue
        +float MaxValue
        +bool Dirty
    }
    class CAttributeIsDirty
    class CGASRunningTag
    class SingletonGameplayTagMap {
        +NativeHashMap~int,ComGameplayTag~ Map
    }

    %% ── Effect 静态配置（Instantiate 时挂载）──
    class CEffectBasicInfo {
        +int Code
    }
    class CDuration
    class CPeriod
    class CStacking {
        +EffectStackType
        +EffectDurationRefreshPolicy
        +EffectPeriodResetPolicy
        +EffectExpirationPolicy
    }
    class MCModifiers {
        +EffectModifier[] Modifiers
    }
    class MCGrantedAbility {
        +GrantedAbility[] Abilities
    }
    class CEffectGrantedTags
    class CApplicationRequiredTags
    class COngoingRequiredTags
    class CEffectImmunityTags
    class CRemoveEffectWithTags
    class CCueOnAdd
    class CCueOnApply
    class CCueOnActivate
    class CCueOnTick
    class CCueOnDeactivate
    class CCueOnRemove

    %% ── Effect Wip 状态机（操作推进）──
    class WipInstantiateEffect
    class WipCheckApplyEffect
    class WipApplyEffect
    class WipCheckActiveEffect
    class WipActivateEffect
    class WipDeactivateEffect
    class WipRemoveEffect
    class CEffectInUsage {
        +Entity Source
        +Entity Target
    }
    class CEffectInstance {
        +Entity Instigator
    }
    class CEffectApplied
    class CEffectDestroy
    class CCreatedByAbility

    %% ── Ability ──
    class CAbilityBaseInfo {
        +int Code
        +Entity Owner
        +int Level
    }
    class MCAbilityLogic {
        +AbilityLogicBase logic
    }
    class CAbilityActive
    class CAbilityInTryActivate
    class CAbilityInTryEnd
    class CAbilityInTryCancel
    class CAbilityCooldown
    class CAbilityCost
    class CAbilityActivationRequiredTags
    class CAbilityActivationBlockedTags
    class CAbilityActivationOwnedTags
    class CGrantedByEffect
    class MCGrantedAbilityRuntime

    %% ── Cue ──
    class MCCue {
        +CueSetting[] Settings
    }
    class ECCuePlayable
    class ECCuePlaying
    class ECKillCue
    class CPlayRequiredTags
    class CPlayImmunitedTags

    IComponentData <|.. CAscBasicData
    IComponentData <|.. CAttributeData
    IComponentData <|.. CAttributeIsDirty
    IComponentData <|.. CGASRunningTag
    IComponentData <|.. SingletonGameplayTagMap
    IComponentData <|.. CEffectBasicInfo
    IComponentData <|.. CDuration
    IComponentData <|.. CPeriod
    IComponentData <|.. CStacking
    IComponentData <|.. MCModifiers
    IComponentData <|.. MCGrantedAbility
    IComponentData <|.. CEffectGrantedTags
    IComponentData <|.. CApplicationRequiredTags
    IComponentData <|.. COngoingRequiredTags
    IComponentData <|.. CEffectImmunityTags
    IComponentData <|.. CRemoveEffectWithTags
    IComponentData <|.. CCueOnAdd
    IComponentData <|.. CCueOnApply
    IComponentData <|.. CCueOnActivate
    IComponentData <|.. CCueOnTick
    IComponentData <|.. CCueOnDeactivate
    IComponentData <|.. CCueOnRemove
    IComponentData <|.. WipInstantiateEffect
    IComponentData <|.. WipCheckApplyEffect
    IComponentData <|.. WipApplyEffect
    IComponentData <|.. WipCheckActiveEffect
    IComponentData <|.. WipActivateEffect
    IComponentData <|.. WipDeactivateEffect
    IComponentData <|.. WipRemoveEffect
    IComponentData <|.. CEffectInUsage
    IComponentData <|.. CEffectInstance
    IComponentData <|.. CEffectApplied
    IComponentData <|.. CEffectDestroy
    IComponentData <|.. CCreatedByAbility
    IComponentData <|.. CAbilityBaseInfo
    IComponentData <|.. MCAbilityLogic
    IComponentData <|.. CAbilityActive
    IComponentData <|.. CAbilityInTryActivate
    IComponentData <|.. CAbilityInTryEnd
    IComponentData <|.. CAbilityInTryCancel
    IComponentData <|.. CAbilityCooldown
    IComponentData <|.. CAbilityCost
    IComponentData <|.. CAbilityActivationRequiredTags
    IComponentData <|.. CAbilityActivationBlockedTags
    IComponentData <|.. CAbilityActivationOwnedTags
    IComponentData <|.. CGrantedByEffect
    IComponentData <|.. MCCue
    IComponentData <|.. CPlayRequiredTags
    IComponentData <|.. CPlayImmunitedTags
    IEnableableComponent <|.. ECCuePlayable
    IEnableableComponent <|.. ECCuePlaying
    IEnableableComponent <|.. ECKillCue
    IBufferElementData <|.. BFixedTag
    IBufferElementData <|.. BTemporaryTag
    IBufferElementData <|.. BGameplayEffect
    IBufferElementData <|.. BAbility
    IBufferElementData <|.. BEAttrSet
```

### 4.2 Entity 结构

三种 Entity 实体各自携带的组件集合：

```mermaid
graph TB
    subgraph ASC["ASC Entity（角色实体）"]
        A1["CAscBasicData"]
        A2["BFixedTag[]<br/>固定 Tag"]
        A3["BTemporaryTag[]<br/>临时 Tag"]
        A4["BEAttrSet[]<br/>属性集（含 NativeArray 子数组）"]
        A5["BGameplayEffect[]<br/>持有中的 Effect"]
        A6["BAbility[]<br/>已授予的技能"]
        A7["CGASRunningTag"]
    end

    subgraph AB["Ability Entity（技能实体）"]
        B1["CAbilityBaseInfo<br/>Code / Owner / Level"]
        B2["MCAbilityLogic<br/>托管逻辑实例"]
        B3["CAbilityCooldown / CAbilityCost"]
        B4["CAbilityActivation*Tags"]
        B5["CAbilityActive / CAbilityInTry*<br/>动态状态"]
    end

    subgraph GE["GameplayEffect Entity（效果实体）"]
        E1["CEffectBasicInfo"]
        E2["CDuration / CPeriod / CStacking"]
        E3["MCModifiers / MCGrantedAbility"]
        E4["C*Tags<br/>Granted / Required / Immunity"]
        E5["Wip*<br/>操作推进状态机"]
        E6["CCueOn*<br/>6 个时机钩子"]
    end

    subgraph CE["Cue Entity（表现实体）"]
        C1["MCCue"]
        C2["ECCuePlayable / ECCuePlaying"]
        C3["ECKillCue"]
        C4["CPlayRequiredTags / CPlayImmunitedTags"]
    end

    ASC -->|BAbility.Ability| AB
    ASC -->|BGameplayEffect.Effect| GE
    GE -->|CCueOn*.CueEntity| CE
    AB -->|CGrantedByEffect| GE
    GE -->|CCreatedByAbility| AB

    style ASC fill:#e8f4f8
    style AB fill:#fff4e6
    style GE fill:#f0e8f8
    style CE fill:#e8f8e8
```

### 4.3 Bridge 与 XParam 多态

```mermaid
classDiagram
    class GASInternalBridge {
        <<static>>
        -List~Action~ _pendingEvents
        +event Action OnBeforeDrain
        +event Action~object~ OnEventEnqueued
        +Enqueue(Action) void
        +Enqueue~T~(T) void
        +Drain() void
        +Clear() void
    }
    note for GASInternalBridge "ECS 系统（Burst/Job）无法直接调托管事件系统，<br/>先入队，由 SEventForwarder 在 Presentation 阶段 Drain"

    class SEventForwarder {
        <<SystemBase>>
    }
    class EventBridgeService {
        -Dispatch(object) void
    }

    class GEAppliedEvent { <<struct>> }
    class GEActivatedEvent { <<struct>> }
    class GERemovedEvent { <<struct>> }
    class AttributeChangedEvent { <<struct>> }
    class AttributeBaseChangedEvent { <<struct>> }
    class AbilityActivatedEvent { <<struct>> }
    class AbilityEndedEvent { <<struct>> }
    class AbilityCancelledEvent { <<struct>> }
    class EffectStackChangedEvent { <<struct>> }

    class XParam {
        <<interface>>
    }
    class XParamNone
    class XParamInt
    class XParamFloat
    class XParamBool
    class XParamString
    class XParamVector2
    class XParamVector3
    class XParamArrayInt
    class XParamArrayFloat
    class XParamAnimator
    class XParamEffectIDs
    class XParamCueIDs
    class XParamApplyEffects
    class XParamALTimelineID
    class XParamCue
    class XParamLogging
    class XParamPlaySound
    class XParamMountPrefab
    class XParamMMCScalable
    class AttributeBasedMmcParam
    class MmcParaFloatScale

    class IDataTrait { <<interface>> }
    class DataTrait { <<abstract>> }
    class GASEntityTrait

    SEventForwarder ..> GASInternalBridge : Drain
    GASInternalBridge ..> EventBridgeService : OnEventEnqueued
    EventBridgeService ..> GEAppliedEvent
    EventBridgeService ..> GEActivatedEvent
    EventBridgeService ..> GERemovedEvent
    EventBridgeService ..> AttributeChangedEvent
    EventBridgeService ..> AttributeBaseChangedEvent
    EventBridgeService ..> AbilityActivatedEvent
    EventBridgeService ..> AbilityEndedEvent
    EventBridgeService ..> AbilityCancelledEvent
    EventBridgeService ..> EffectStackChangedEvent

    XParam <|.. XParamNone
    XParam <|.. XParamInt
    XParam <|.. XParamFloat
    XParam <|.. XParamBool
    XParam <|.. XParamString
    XParam <|.. XParamVector2
    XParam <|.. XParamVector3
    XParam <|.. XParamArrayInt
    XParam <|.. XParamArrayFloat
    XParam <|.. XParamAnimator
    XParam <|.. XParamEffectIDs
    XParam <|.. XParamCueIDs
    XParam <|.. XParamApplyEffects
    XParam <|.. XParamALTimelineID
    XParam <|.. XParamCue
    XParam <|.. XParamLogging
    XParam <|.. XParamPlaySound
    XParam <|.. XParamMountPrefab
    XParam <|.. XParamMMCScalable
    XParam <|.. AttributeBasedMmcParam
    XParam <|.. MmcParaFloatScale

    DataTrait <|-- GASEntityTrait
    IDataTrait <|.. DataTrait
```

**`XParam` 的角色**：配置表的「载荷多态基类」。`AbilityComponentConfig` / `GameplayEffectComponentConfig` / `GameplayCueBase<T>` / `AbilityTaskBase<T>` / `TargetCatcherBase<T>` / `ModMagnitudeCalculationBase<T>` 全部以 `XParam` 为泛型约束——**一类逻辑 + 一个 XParam 子类 = 一个可配置项**。新增逻辑只需新建这一对，`AbilityService.ScanAndRegisterAll()` 会通过反射自动注册。

### 4.4 策略基类家族

```mermaid
classDiagram
    class AbilityLogicBase {
        <<abstract>>
        +Execute(...)
    }
    class AbilityLogicBase~T~ {
        <<abstract>>
        +SetParam(T) void
    }
    class ALApplyEffect
    class ALDebugLog
    class ALTimeline

    class AbilityTaskBase {
        <<abstract>>
    }
    class AbilityTaskBase~T~ {
        <<abstract>>
    }
    class TaskApplyEffects
    class TaskDoCooldown
    class TaskDoCost
    class TaskPlayCue
    class TaskDebug
    class TaskDoNothing

    class TargetCatcherBase {
        <<abstract>>
    }
    class TargetCatcherBase~T~ {
        <<abstract>>
    }
    class CatchSelf
    class CatchTarget
    class CatchAreaBox3D

    class ModMagnitudeCalculationBase {
        <<abstract>>
    }
    class ModMagnitudeCalculationBase~T~ {
        <<abstract>>
    }
    class MMCNone
    class MMCScalableFloat
    class MMCAttributeBased
    class IAttributeValueResolver {
        <<interface>>
    }
    class DefaultAttributeValueResolver

    class GameplayCueBase {
        <<abstract>>
    }
    class GameplayCueBase~T~ {
        <<abstract>>
    }
    class CueLog
    class CueLogging
    class CuePlayAnimator
    class CuePlaySound
    class CueMountPrefab

    AbilityLogicBase <|-- AbilityLogicBase~T~
    AbilityLogicBase~T~ <|-- ALApplyEffect
    AbilityLogicBase~T~ <|-- ALDebugLog
    AbilityLogicBase~T~ <|-- ALTimeline

    AbilityTaskBase <|-- AbilityTaskBase~T~
    AbilityTaskBase~T~ <|-- TaskApplyEffects
    AbilityTaskBase~T~ <|-- TaskDoCooldown
    AbilityTaskBase~T~ <|-- TaskDoCost
    AbilityTaskBase~T~ <|-- TaskPlayCue
    AbilityTaskBase~T~ <|-- TaskDebug
    AbilityTaskBase~T~ <|-- TaskDoNothing

    TargetCatcherBase <|-- TargetCatcherBase~T~
    TargetCatcherBase~T~ <|-- CatchSelf
    TargetCatcherBase~T~ <|-- CatchTarget
    TargetCatcherBase~T~ <|-- CatchAreaBox3D

    ModMagnitudeCalculationBase <|-- ModMagnitudeCalculationBase~T~
    ModMagnitudeCalculationBase~T~ <|-- MMCNone
    ModMagnitudeCalculationBase~T~ <|-- MMCScalableFloat
    ModMagnitudeCalculationBase~T~ <|-- MMCAttributeBased
    MMCAttributeBased ..> IAttributeValueResolver
    IAttributeValueResolver <|.. DefaultAttributeValueResolver

    GameplayCueBase <|-- GameplayCueBase~T~
    GameplayCueBase~T~ <|-- CueLog
    GameplayCueBase~T~ <|-- CueLogging
    GameplayCueBase~T~ <|-- CuePlayAnimator
    GameplayCueBase~T~ <|-- CuePlaySound
    GameplayCueBase~T~ <|-- CueMountPrefab
```

**工厂注册表**：`AbilityLogicFactory`（Logic + Task）、`TargetCatcherHelper`（Catcher）由 `AbilityService.ScanAndRegisterAll()` 在 `OnInit` 时反射填充。扫描范围是 **GAS 程序集 + Architecture 所在程序集**——框架自带逻辑在 GAS 程序集，项目自定义逻辑与 Architecture 同程序集。

---

## 5. 行为模型

### 5.1 Architecture 状态机

```mermaid
stateDiagram-v2
    [*] --> NotInitialized
    NotInitialized --> Initializing : Initialize()
    Initializing --> Initialized : OnInit → InitAll → OnInitialized
    Initialized --> Paused : Pause()
    Paused --> Initialized : Resume()
    Initialized --> Shutting : Shutdown()
    Paused --> Shutting : Shutdown()
    Shutting --> Shutdown : DeInitAll + Clear + Dispose
    Shutdown --> [*]
    NotInitialized --> Shutdown : Dispose()

    note right of Pause
        Pause() 仅当 State == Initialized 时生效
    end note
    note right of Resume
        Resume() 仅当 State == Paused 时生效
    end note
    note right of Initialize
        Initialize() 幂等：mInitialized 为 true 时直接返回
    end note
```

### 5.2 GameplayEffect 生命周期状态机

Effect 通过 **Wip\* 组件**标记「待执行的操作」，各 `SG*` 系统组轮流消费自己负责的 Wip 标记并推进到下一阶段。

```mermaid
stateDiagram-v2
    [*] --> Created : EffectService.ApplyEffect<br/>挂 WipInstantiateEffect
    Created --> CheckingApply : SInstantiateEffect<br/>WipCheckApplyEffect
    CheckingApply --> Applying : SCheckApplicationRequiredTags<br/>SCheckImmunityTags<br/>SCheckApplyEnd
    CheckingApply --> [*] : 条件不满足<br/>SDestroyEffects
    Applying --> Instant : 瞬时型<br/>SExecuteInstantEffectModifiers
    Applying --> Durational : 持续型<br/>SAddEffectToAscBuffList
    Instant --> [*] : SExecuteInstantEffectEnd
    Durational --> CheckingActive : SCheckEffectStacking<br/>SPlayCueOnAdd
    CheckingActive --> Activating : SCheckEffectActive
    Activating --> Active : SAddModifiers<br/>SAddGrantedAbility<br/>SEffectAddGrantedTags<br/>SPlayCueOnActivate<br/>SSetEffectActive
    Active --> Active : SEffectDurationTick<br/>SEffectPeriodTick<br/>SEffectStackingTick<br/>SPlayCueOnTick
    Active --> Deactivating : 时长耗尽 / 主动移除<br/>WipDeactivateEffect
    Deactivating --> Removing : SRemoveModifiers<br/>SRemoveGrantedAbility<br/>SEffectRemoveGrantedTags<br/>SPlayCueOnDeactivate<br/>SSetEffectDeactive
    Removing --> [*] : SEffectRemoveEnd<br/>SRemoveEffectFromAscBuffList<br/>SRemoveGrantedAbilityOnRemove<br/>SDestroyEffects

    note right of Active
        每个转换都伴随对应时机的 CCueOn* 钩子
    end note
```

### 5.3 Ability 生命周期状态机

```mermaid
stateDiagram-v2
    [*] --> Granted : AbilityService.GrantAbility<br/>建 Ability Entity + BAbility 入 ASC Buffer
    Granted --> TryActivate : AbilityService.TryActivate<br/>挂 CAbilityInTryActivate
    TryActivate --> Blocked : 检查失败<br/>BlockedTags / Cost / Cooldown
    Blocked --> Granted : STryActivateAbility 移除标记
    TryActivate --> Active : STryActivateAbility 检查通过<br/>挂 CAbilityActive + OwnedTags
    Active --> TryEnd : TryEnd()<br/>挂 CAbilityInTryEnd
    Active --> TryCancel : TryCancel()<br/>挂 CAbilityInTryCancel
    TryEnd --> Granted : STryEndAbility<br/>撤销 OwnedTags + 清标记
    TryCancel --> Granted : STryCancelAbility<br/>撤销 OwnedTags + 清标记
    Granted --> [*] : AbilityService.RemoveAbility<br/>或 SRemoveGrantedAbilityOnRemove
```

**激活检查项**（`STryActivateAbility`）：`CAbilityActivationRequiredTags`（必须持有）→ `CAbilityActivationBlockedTags`（不得持有）→ `CAbilityCost`（代价支付）→ `CAbilityCooldown`（冷却就绪）→ `CCancelAbilityWithTags`（激活时取消其他技能）。

---

## 6. ECS 系统组树

`WorldService.CreateSystems()` 构建的完整层级：

```mermaid
graph TD
    ROOT["GAS_World"]
    INIT["InitializationSystemGroup"]
    SIM["SimulationSystemGroup"]
    PRES["PresentationSystemGroup"]
    FIXED["FixedStepSimulationSystemGroup<br/>FixedRateSimpleManager(fixedDeltaTime)"]
    LOGIC["SGLogic"]

    AB["SGAbility"]
    ATTR["SGAttribute"]
    EFF["SGEffect"]
    TIMER["SGlobalTimer"]

    CREATE["SGEffectCreate"]
    OP["SGEffectOperation"]
    DESTROY["SGEffectDestroy"]
    TICK["SGEffectTick"]

    INST["SGInstantiateEffect<br/>SInstantiateEffect"]
    CHKAP["SGCheckApplyEffect<br/>SCheckApplicationRequiredTags · SCheckImmunityTags · SCheckApplyEnd"]
    APPLY["SGApplyEffect<br/>SPlayCueOnApply · SRemoveEffectWithTags · SApplyEnd"]
    INSTE["SGInstantEffect<br/>SExecuteInstantEffectModifiers · SExecuteInstantEffectEnd"]
    DURE["SGDurationalEffect<br/>SAddEffectToAscBuffList · SCheckEffectStacking · SPlayCueOnAdd"]
    CHKAC["SGCheckActivateEffect<br/>SCheckEffectActive"]
    ACT["SGActivateEffect<br/>SActivateEnd · SAddGrantedAbility · SAddModifiers<br/>SEffectAddGrantedTags · SPlayCueOnActivate · SSetEffectActive · SPlayCueOnTick"]
    DEACT["SGDeactivateEffect<br/>SDeactivateEnd · SRemoveGrantedAbility · SRemoveModifiers<br/>SEffectRemoveGrantedTags · SPlayCueOnDeactivate · SSetEffectDeactive · SStopCueOnTick"]
    RM["SGRemoveEffect<br/>SEffectRemoveEnd · SPlayCueOnRemove<br/>SRemoveEffectFromAscBuffList · SRemoveGrantedAbilityOnRemove"]
    RUN["SGRunningEffect<br/>SEffectDurationTick · SEffectPeriodTick · SEffectStackingTick"]

    DISP["SysGrpDisplay<br/>SCueStart · SCueTick · SCueEnd · SCueDestroy · SEventForwarder"]

    ROOT --> INIT
    ROOT --> SIM
    ROOT --> PRES
    SIM --> FIXED
    FIXED --> LOGIC
    LOGIC --> TIMER
    LOGIC --> AB
    LOGIC --> ATTR
    LOGIC --> EFF

    AB --> AB1["STryActivateAbility<br/>STryCancelAbility<br/>STryEndAbility<br/>SAbilityTick"]
    ATTR --> AT1["SUpdateAttributeCurrentValue"]

    EFF --> CREATE
    EFF --> OP
    EFF --> DESTROY
    EFF --> TICK

    CREATE --> INST
    OP --> CHKAP
    OP --> APPLY
    OP --> CHKAC
    OP --> ACT
    OP --> DEACT
    OP --> RM
    APPLY --> INSTE
    APPLY --> DURE
    DESTROY --> D1["SDestroyEffects"]
    TICK --> RUN

    SIM --> DISP

    style LOGIC fill:#e8f4f8
    style EFF fill:#f0e8f8
    style DISP fill:#e8f8e8
```

**执行时序**：全部逻辑跑在 `FixedStepSimulationSystemGroup`（固定步长），与 `Time.fixedDeltaTime` 对齐——保证技能/效果的确定性推进。表现类（Cue）跑在 `SimulationSystemGroup` 的 `SysGrpDisplay`。

---

## 7. 关键时序

### 7.1 Carrier 创建全链路（GAS 模式）

```mermaid
sequenceDiagram
    autonumber
    participant Game as 游戏逻辑
    participant GA as GASArchitecture
    participant CM as CarrierManager
    participant WS as WorldService
    participant EM as EntityManager
    participant Map as GASEntityMapModel
    participant CM2 as ConfigModel
    participant Svc as AbilityService

    Game->>GA: CreateGASCarrier(typeName, ascId, go)
    GA->>CM: CreateCarrier(typeName)
    CM-->>GA: CarrierId(F:T:ID:)
    GA->>WS: EntityManager
    WS-->>GA: EntityManager
    GA->>EM: CreateEntity()
    EM-->>GA: Entity
    GA->>WS: SetupGASEntity(entity)
    Note over WS,EM: AddBuffer: BEAttrSet / BGameplayEffect<br/>BAbility / BFixedTag / BTemporaryTag<br/>AddComponent: CAscBasicData
    GA->>Map: Bind(carrierId, entity)
    GA->>Map: BindGameObject(entity, go)
    Note over Map: 自动挂 GASEntityRef 到 GameObject

    GA->>CM2: GetAscConfig(ascId)
    CM2-->>GA: AscConfigData
    GA->>EM: SetComponentData(CAscBasicData{Level})

    loop 每个 Tag
        GA->>EM: GetBuffer(BFixedTag).Add(tag)
    end

    loop 每个属性集
        GA->>CM2: GetAttrSetDef(setId)
        CM2-->>GA: AttrSetDef
        GA->>EM: 分配 NativeArray~CAttributeData~<br/>写入 BEAttrSet Buffer
    end

    loop 每个技能
        GA->>Svc: GrantAbility(carrierId, abilityId, this)
        Svc->>EM: CreateEntity() → Ability Entity
        Svc->>EM: config.LoadToGameplayAbilityEntity(abilityEntity)
        Svc->>EM: 挂 CAbilityBaseInfo{Code, Owner, Level}
        Svc->>EM: owner 的 BAbility Buffer.Add(new BAbility{Ability})
    end

    GA-->>Game: CarrierId
```

### 7.2 ApplyEffect 跨层链路

展示「托管 API 调用 → ECS Wip 状态机 → 事件回传托管层」的完整闭环。

```mermaid
sequenceDiagram
    autonumber
    participant Game as 游戏逻辑
    participant ES as EffectService
    participant EM as EntityManager
    participant Sys as SGEffect 系统链
    participant FW as SEventForwarder
    participant Br as GASInternalBridge
    participant EBS as EventBridgeService
    participant Sub as 订阅者

    Game->>ES: ApplyEffect(configId, target, source)
    ES->>EM: CreateEntity() → GE Entity
    ES->>EM: config.LoadToGameplayEffectEntity(geEntity)
    ES->>EM: AddComponent(CEffectInUsage{Source, Target})
    ES->>EM: AddComponent(WipInstantiateEffect)
    Note over ES,EM: 托管侧只负责"搭骨架 + 挂首个 Wip 标记"

    Note over Sys: ── FixedStepSimulation 阶段 ──
    Sys->>Sys: SInstantiateEffect<br/>WipInstantiateEffect → WipCheckApplyEffect
    Sys->>Sys: SCheckApplicationRequiredTags<br/>SCheckImmunityTags<br/>SCheckApplyEnd
    alt 条件不满足
        Sys->>Sys: SDestroyEffects 销毁 Entity
    else 条件通过
        Sys->>Sys: WipApplyEffect
        alt 瞬时型
            Sys->>Sys: SExecuteInstantEffectModifiers<br/>SExecuteInstantEffectEnd
        else 持续型
            Sys->>Sys: SAddEffectToAscBuffList<br/>SCheckEffectStacking<br/>SPlayCueOnAdd
            Sys->>Sys: WipCheckActiveEffect → SCheckEffectActive
            Sys->>Sys: SAddModifiers · SAddGrantedAbility<br/>SEffectAddGrantedTags · SSetEffectActive
            Sys->>Sys: SEffectDurationTick / SEffectPeriodTick
        end
    end
    Sys->>Br: GASInternalBridge.Enqueue(GEAppliedEvent)

    Note over FW: ── Presentation 阶段 ──
    FW->>Br: Drain()
    Br->>EBS: OnEventEnqueued(evt)
    EBS->>Sub: SendEvent(GEAppliedEvent)
```

**为什么需要 `GASInternalBridge`**：ECS 系统（可能运行在 Job / Burst 上下文）不能直接调用托管的事件系统。入队 → 在 `SysGrpDisplay` 的 `SEventForwarder` 中统一 `Drain()`，把 ECS 侧事件搬到主线程托管世界。

### 7.3 Tag 查询的层级匹配

```mermaid
sequenceDiagram
    autonumber
    participant Caller as 调用方
    participant TS as ITagService
    participant IDX as TagRuntimeIndex / SingletonGameplayTagMap

    Caller->>TS: HasTag(carrierId, "Ability.Skill.Fire")
    TS->>TS: TagKey.Normalize(key)
    TS->>IDX: TryGetSlot(normalized)
    IDX-->>TS: querySlot
    loop 该 Carrier 持有的每个槽位 actualSlot
        TS->>IDX: MatchesOrIsAncestor(actualSlot, querySlot)
        Note over IDX: actualTagId == queryTagId<br/>或 actual 在 query 的祖先链上
        IDX-->>TS: bool
    end
    TS-->>Caller: true / false
```

**语义**：`HasTag` 是**父 Tag 命中子 Tag**——Carrier 持有 `Ability.Skill.Fire`，则查询 `Ability.Skill` 返回 `true`。`HasExactTag` 才是精确匹配。`FindCarriersWithTag` 同样遵循层级语义（遍历 `carriersBySlot` 对每个槽位做祖先判定后并集）。

---

## 8. 依赖关系速查

| 依赖方向 | 说明 | 是否允许 |
|---|---|---|
| `NexusFramework.GAS` → `NexusFramework` | GAS 继承 `Architecture` / `AbstractService` / `AbstractModel` | ✅ |
| `NexusFramework` → `NexusFramework.GAS` | 框架层反向依赖 | ❌ 禁止 |
| `NexusFramework` → `Unity.Entities` | 框架层引入 ECS | ❌ 禁止（保持框架层宿主无关） |
| `GASArchitecture` → `EcsTagService` | 通过 `CreateTagService()` 虚方法覆盖 | ✅ |
| `CarrierManager` → `ITagService` | 仅序列化时读写，不持有 Tag 状态 | ✅ |
| `ITagService` → `CarrierManager` | 仅校验载体有效性 + 枚举已知载体 | ✅（双向但职责不重叠） |
| ECS 系统 → 托管事件系统 | 直接调用 | ❌ 必须经 `GASInternalBridge` |
| 托管 Service → ECS | 经 `WorldService.EntityManager` | ✅ |

---

## 9. 扩展点

新增一类玩法逻辑的标准路径：

```mermaid
graph LR
    A["1. 新建 XParam 子类<br/>承载配置参数"] --> B["2. 新建逻辑类<br/>继承 *Base~T~"]
    B --> C["3. 反射自动注册<br/>AbilityService.ScanAndRegisterAll()"]
    C --> D["4. 配置表填 XParam<br/>类型名 + 字段"]
    D --> E["5. 运行期按名实例化"]
```

| 扩展目标 | 基类 | 泛型约束 | 注册表 |
|---|---|---|---|
| 技能逻辑 | `AbilityLogicBase<T>` | `XParam` | `AbilityLogicFactory` |
| 技能任务（Timeline 片段） | `AbilityTaskBase<T>` | `XParam` | `AbilityLogicFactory` |
| 目标抓取 | `TargetCatcherBase<T>` | `XParam` | `TargetCatcherHelper` |
| 伤害/数值计算 | `ModMagnitudeCalculationBase<T>` | `XParam` | 由 `MMCConfig` 指定 |
| 表现 | `GameplayCueBase<T>` | `XParam` | `CueHelper` |
| 数据特征 | `DataTrait` | — | 载体 `AddTrait<T>` |

**装配约束**：`XParam` 子类需标 `[BeanField]` / `[BeanPolymorphicField]` 特性，供配置反序列化识别字段。

---

## 附：程序集依赖

```mermaid
graph BT
    NF["NexusFramework.asmdef"]
    GAS["NexusFramework.GAS.asmdef"]
    GASEDITOR["NexusFramework.GAS.Editor.asmdef"]
    TESTS["Tests.asmdef"]
    DEMO["Demo.asmdef"]
    PIPE["Unity.Pipeline.*"]

    GAS --> NF
    TESTS --> NF
    TESTS --> GAS
    DEMO --> NF
    DEMO --> GAS
    GASEDITOR --> GAS
    GASEDITOR --> PIPE

    style NF fill:#e8f4f8
    style GAS fill:#fff4e6
```
