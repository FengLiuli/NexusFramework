---
layer: design
status: draft
topic: item-storage-interface
origin:
  - docs/brainstorms/2026-11-17-item-storage-requirements.md
  - docs/brainstorms/2026-11-17-config-item-boundary-analysis.md
  - docs/design/D006-data-pack-interface-design.md
created: 2026-11-17
updated: 2026-11-17
---

# 设计：物品与存储子系统接口契约

## 方案概述

本文档固定物品子系统的接口形状：物品定义读取、物品实例、容器归属、操作与事件。只定义契约，不含实现。

四条基本原则：

1. **实例 = Carrier，容器 = Carrier**：复用框架的载体、特征、索引与序列化，不新建实体系统；
2. **物品库不依赖具体表类型**：定义通过 `IItemDefinitionProvider` 读取，Luban 行类型由业务层适配；
3. **物品不改属性、不播效果、不落盘**：这些通过事件交给 GAS，落盘交给存档子系统；
4. **查询走索引、热路径零拷贝**：按定义/容器的查询必须有专用索引，不依赖全量载体扫描（依据 factory-scenario 基线：全扫 222 ms vs 索引 28 µs）。

---

## 一、分层与依赖

```
NexusFramework.Inventory          → NexusFramework + NexusFramework.Config
业务（Emberheart）                 → NexusFramework.Inventory + 数据包程序集（cfg.*）
```

- 物品库依赖 Config 仅用于定义表读取的**可选适配器**；核心接口不出现 Config 类型。
- 物品库**不引用** GAS、ECS、Unity.Entities。
- 物理目录（`Assets/NexusFramework.Inventory/` 或 `Assets/Demo/Inventory/`）待 Q3 确认，程序集名与命名空间不受影响。

---

## 二、数据模型（三层）

| 层 | 载体 | 内容 |
|---|---|---|
| 定义 | Luban 表行 + `ItemDefinitionView` | 静态只读 |
| 实例 | `Carrier` + `ItemCoreTrait`（必需）+ 可选 Trait | 运行时 |
| 容器 | `Carrier` + `ContainerTrait`，关系存 `CarrierGroupIndex` / `CarrierIndex` | 归属 |

### 2.1 建议的物品定义表（Luban）

表名 `Tbitem`，文件 `#item.xlsx`，数据名 `tbitem`，包级命名空间 `dpack.core`：

| 列 | 类型 | 说明 |
|---|---|---|
| `ID` | int | 主键（大写，`DataPackConventions` 约定） |
| `NameKey` | text | 本地化 key |
| `DescKey` | text | 本地化 key |
| `IconKey` | string | 资源 key |
| `Category` | enum `ItemCategory` | 分类 |
| `MaxStack` | int | 1 = 不可堆叠 |
| `MaxDurability` | int | 0 = 无耐久 |
| `Tags` | `list,string` | 物品标签 key |
| `UseEffectId` | int | GAS 效果 ID（0 = 不可使用） |
| `AllowedSlot` | enum `ItemSlotType` | 可装备槽位（None = 不可装备） |
| `is_void` | bool | 废除列（约定） |

---

## 三、核心契约

### 3.1 句柄与枚举

```csharp
namespace NexusFramework.Inventory
{
    /// <summary>物品实例句柄。包装 CarrierId，避免与容器放宽类型误传。</summary>
    public readonly struct ItemHandle : IEquatable<ItemHandle>
    {
        public CarrierId CarrierId { get; }
        public bool IsValid { get; }
        public static ItemHandle FromCarrier(CarrierId id);
    }

    /// <summary>容器句柄。</summary>
    public readonly struct ContainerId : IEquatable<ContainerId>
    {
        public CarrierId CarrierId { get; }
        public bool IsValid { get; }
        public static ContainerId FromCarrier(CarrierId id);
    }

    public enum ItemCategory { None, Consumable, Equipment, Material, Quest, Currency }

    public enum ItemSlotType { None, Weapon, Armor, Accessory, Boots, Ring }
}
```

### 3.2 物品定义

```csharp
    /// <summary>物品定义的只读视图，不暴露 Luban 生成类型。</summary>
    public readonly struct ItemDefinitionView
    {
        public int Id { get; }
        public string NameKey { get; }
        public string DescriptionKey { get; }
        public string IconKey { get; }
        public ItemCategory Category { get; }
        public int MaxStack { get; }          // 1 = 不可堆叠
        public int MaxDurability { get; }     // 0 = 无耐久
        public IReadOnlyList<string> Tags { get; }
        public int UseEffectId { get; }       // 0 = 不可使用
        public ItemSlotType AllowedSlot { get; }
        public object Raw { get; }            // 原始行，供 provider 解释分类专属字段
    }

    public interface IItemDefinitionProvider
    {
        bool TryGetDefinition(int definitionId, out ItemDefinitionView definition);
        bool ContainsDefinition(int definitionId);
        IEnumerable<ItemDefinitionView> All { get; }
    }
```

**与 Config 的衔接**

```csharp
    /// <summary>可选适配器：把 Config 的一张表映射为物品定义。业务层实例化。</summary>
    public sealed class ConfigItemDefinitionProvider<TRow> : IItemDefinitionProvider
        where TRow : class
    {
        public ConfigItemDefinitionProvider(
            string tableName,
            Func<TRow, ItemDefinitionView> map,
            Func<TRow, int> getId);
    }
```

业务侧一行实例化：

```csharp
var provider = new ConfigItemDefinitionProvider<cfg.item>(
    "tbitem",
    MapItemDefinition,
    row => row.ID);
```

物品库对 `cfg.item` 零知识；换表或换数据源只改业务层一行。

### 3.3 实例特征

```csharp
    /// <summary>必需特征：任何物品实例必须有且仅有一个。</summary>
    [Serializable]
    public sealed class ItemCoreTrait : DataTrait
    {
        public int DefinitionId;
        public int Count;
        public bool Bound;
    }

    [Serializable]
    public sealed class ItemDurabilityTrait : DataTrait
    {
        public int Current;
        public int Max;
    }

    [Serializable]
    public struct ItemAffixData
    {
        public int AffixId;
        public int Tier;
        public float Value;
    }

    [Serializable]
    public sealed class ItemAffixListTrait : DataTrait
    {
        public List<ItemAffixData> Affixes;
    }
```

**约束**：

- `DataTrait` 基于 `JsonUtility`，所有字段必须可序列化；禁止 `Dictionary`、接口、多态字段；
- 每个 Trait **必须提供无参构造**（`DeserializeCarrier` 经 `Activator.CreateInstance` 重建）；
- 每个 Trait 使销毁 +210 ns、写入 +5~10 B。物品实例建议**不超过 3 个 Trait**；耐久等高频字段优先合入 `ItemCoreTrait`；
- **前置阻塞**：当前 `DeserializeCarrier` 无法恢复任何 Trait（backlog A1）。A1 修复前，物品存档不可用。

### 3.4 容器

```csharp
    public enum ContainerKind
    {
        Capacity,   // 无序容量型：容量上限
        Slot        // 槽位型：固定槽位 + 槽位→物品
    }

    [Serializable]
    public sealed class ContainerTrait : DataTrait
    {
        public ContainerKind Kind;
        public int Capacity;        // Capacity 型有效
        public int SlotCount;       // Slot 型有效
        public string ConstraintId; // 空 = 无额外约束
    }

    public readonly struct ItemLocation
    {
        public ContainerId Container { get; }
        public int SlotIndex { get; }   // -1 = 无序容器
    }

    public interface IContainerConstraint
    {
        string Id { get; }
        bool CanAccept(ContainerId container, in ItemDefinitionView definition, int slotIndex);
    }

    public interface IContainerConstraintRegistry
    {
        void Register(IContainerConstraint constraint);
        bool TryGet(string id, out IContainerConstraint constraint);
    }

    public interface IContainerService
    {
        ContainerId Create(ContainerKind kind, int capacity, int slotCount, string constraintId = null);
        void Destroy(ContainerId container);
        bool TryGet(ContainerId container, out ContainerView view);
        IEnumerable<ContainerId> All { get; }
    }

    public readonly struct ContainerView
    {
        public ContainerId Id { get; }
        public ContainerKind Kind { get; }
        public int Capacity { get; }
        public int SlotCount { get; }
        public int ItemCount { get; }
        public string ConstraintId { get; }
    }
```

### 3.5 索引表设计

| 索引类型 | 表名 | 方向 | 用途 |
|---|---|---|---|
| `CarrierGroupIndex<CarrierId>` | `"item.container"` | 容器 → 物品集合；`KeysOf(item)` → 物品所在容器 | 归属（多对一） |
| `CarrierIndex<ItemLocation>` | `"item.slot"` | 物品 → 位置；反向 `TryGetCarrier(location)` | 槽位（一对一） |
| `CarrierGroupIndex<int>` | `"item.definition"` | definitionId → 物品集合；`KeysOf(item)` → 定义 ID | 按定义统计/查找 |

- **不使用** `CarrierIndex<CarrierId>` 表达"物品 → 容器"：该类型是双射，多物品指向同一容器会互相驱逐（D005 文档已说明）。
- **定义索引是硬需求**：全扫 100 万物品按定义统计需 221.7 ms，走索引 28.3 µs（factory-scenario 基线，差 7 800 倍）。
- 三张索引均在容器/物品载体销毁时由 `CarrierManager` 自动清理，业务不手写 `Clear`。
- 索引数量有成本：每多一个索引，销毁 +190 ns（32 索引时慢 14 倍）。三张索引对物品系统可接受，但不再新增无关索引。

### 3.6 操作

```csharp
    public enum ItemOperationCode
    {
        Success,
        ContainerFull,
        SlotOccupied,
        CategoryNotAllowed,      // 容器约束拒绝
        SlotTypeMismatch,        // 装备槽类型不符
        StackLimitExceeded,
        NotStackable,
        QuantityInvalid,
        DefinitionMissing,
        InvalidItem,
        InvalidContainer,
        InternalError
    }

    public readonly struct ItemOperationResult
    {
        public bool Success { get; }
        public ItemOperationCode Code { get; }
        public ItemHandle Item { get; }        // 受影响物品（新建或变更后）
        public int AffectedCount { get; }
    }

    public sealed class ItemInstanceOptions
    {
        public int Durability = -1;                    // -1 = 取定义默认
        public IReadOnlyList<ItemAffixData> Affixes;
        public bool Bound;
        public bool TryAutoMerge = true;               // Add 时优先并入已有堆叠
    }

    public interface IItemService
    {
        ItemOperationResult Add(ContainerId container, int definitionId, int count,
                                ItemInstanceOptions options = null);
        ItemOperationResult Remove(ItemHandle item, int count);
        ItemOperationResult Move(ItemHandle item, ContainerId target, int targetSlot = -1);
        ItemOperationResult Swap(ItemHandle a, ItemHandle b);
        ItemOperationResult Split(ItemHandle item, int count, ContainerId target, int targetSlot = -1);
        ItemOperationResult Merge(ItemHandle source, ItemHandle target);
        ItemOperationResult Destroy(ItemHandle item);
        ItemOperationResult Use(ItemHandle item);       // 扣数量 + 发 ItemUsedEvent，不执行效果
    }
```

**操作语义**

- **原子性**：先完成全部校验（容量、槽位、约束、数量），再一次性写入；失败不产生中间态；
- **Add**：`TryAutoMerge` 为真时先补满同类堆叠，再新建；全部放不下则整体失败（不回滚已放置部分 —— 若实现采用"部分成功"，必须在结果中报告实际数量，接口预留 `AffectedCount`）；
- **Remove**：数量降到 0 时销毁实例；移除后重建堆叠归属；
- **Move**：目标槽位为空 → 移动；被占 → 失败，调用方改用 `Swap`；
- **Split**：源数量减少，新实例加入目标位置；词条复制规则待定（见 IQ4）；
- **Use**：仅扣数量 + 发事件；效果由 GAS 订阅 `ItemUsedEvent` 执行。

**性能约束**（factory-scenario 基线）：

| 操作 | 单次成本 | 每帧上限（16 ms 预算） |
|---|---|---|
| 移动 | 1.17 µs | ~5 000 次 |
| 生成 + 挂索引 + 销毁 | 3.13 µs | ~2 000 次 |
| 单容器读取（100 物品） | 27.1 µs | UI 刷新按需 |

- 高频搬运（传送带、机械臂）应走批量 API，并**合并事件**（帧末一次批量事件）；
- 大批量生成/销毁必须分帧。

### 3.7 查询

```csharp
    public sealed class ItemFilter
    {
        public int? DefinitionId;
        public ItemCategory? Category;
        public string RequiredTag;
        public bool IncludeVoid;
    }

    public readonly struct ItemView
    {
        public ItemHandle Handle { get; }
        public int DefinitionId { get; }
        public int Count { get; }
        public bool Bound { get; }
        public int Durability { get; }
        public int MaxDurability { get; }
        public ItemLocation Location { get; }
        public ItemDefinitionView Definition { get; }
        public bool TryGetAffixes(out IReadOnlyList<ItemAffixData> affixes);
    }

    public interface IItemQuery
    {
        bool TryGetItem(ItemHandle item, out ItemView view);
        ContainerId GetContainerOf(ItemHandle item);
        ItemLocation GetLocation(ItemHandle item);

        // 热路径：写入调用方缓冲，避免每次分配
        int CopyItemsTo(ContainerId container, List<ItemHandle> buffer);
        int CopyItemsByDefinition(int definitionId, List<ItemHandle> buffer);

        // 冷路径：便捷枚举（每次分配）
        IEnumerable<ItemHandle> GetItems(ContainerId container);
        IEnumerable<ItemHandle> Find(ContainerId container, ItemFilter filter);

        int CountInContainer(ContainerId container, int definitionId);
        int CountHeld(int definitionId);   // 走 item.definition 索引
    }
```

**查询不遍历全部载体**：容器列举走 `CarrierGroupIndex.Get(container)`；按定义统计走容器内遍历；跨容器统计由调用方显式传入容器集合，避免引入"玩家所有权"概念。

### 3.8 事件

```csharp
    public readonly struct ItemAddedEvent
    {
        public ContainerId Container;
        public ItemHandle Item;
        public int DefinitionId;
        public int Count;
    }

    public readonly struct ItemRemovedEvent
    {
        public ContainerId Container;
        public ItemHandle Item;
        public int DefinitionId;
        public int Count;
    }

    public readonly struct ItemMovedEvent
    {
        public ItemHandle Item;
        public ContainerId From;
        public ContainerId To;
        public int FromSlot;
        public int ToSlot;
    }

    public readonly struct ItemCountChangedEvent
    {
        public ItemHandle Item;
        public int Before;
        public int After;
    }

    public readonly struct ItemEquippedEvent
    {
        public ItemHandle Item;
        public ContainerId EquipmentSlot;
        public ItemSlotType SlotType;
    }

    public readonly struct ItemUnequippedEvent
    {
        public ItemHandle Item;
        public ContainerId FromSlot;
        public ItemSlotType SlotType;
    }

    public readonly struct ItemUsedEvent
    {
        public ItemHandle Item;
        public int DefinitionId;
        public int UseEffectId;
    }

    public readonly struct ContainerChangedEvent
    {
        public ContainerId Container;
        public int ItemCount;
    }
```

**发送规则**：所有改变状态的操作成功后发送；事件只携带变更上下文，不携带完整快照（订阅方按需 `IItemQuery` 读取）。高频搬运场景应合并为帧末批量事件，避免每帧数千次广播。

### 3.9 存档快照

物品存档**不走** `CarrierManager.SaveToFile`：

```csharp
    [Serializable]
    public struct ItemRecord
    {
        public int DefinitionId;
        public int Count;
        public int Durability;
        public bool Bound;
        public ItemAffixData[] Affixes;   // 可空
    }

    [Serializable]
    public sealed class ContainerSnapshot
    {
        public int SchemaVersion;            // 当前 1
        public int ContainerKind;
        public int Capacity;
        public int SlotCount;
        public string ConstraintId;
        public int[] Slots;                  // Slot 容器：槽位 → 记录下标（-1 空）
        public ItemRecord[] Items;           // Capacity 容器：顺序即列表顺序
    }

    public interface IItemSnapshotSerializer
    {
        string Serialize(IReadOnlyList<ContainerSnapshot> snapshots);
        IReadOnlyList<ContainerSnapshot> Deserialize(string json);
        void Restore(ContainerId container, ContainerSnapshot snapshot);
    }
```

理由：逐物品 JSON 137 B/件、10.2 s/100 万；容器批量 35 B、0.95 s；二进制 12 B、0.27 s。**物品存档的语义单位是容器，不是任意载体集合**。

与 R19 的关系：仍不新建实体系统，但显式放弃逐载体 JSON（见需求文档修订）。

---

## 四、关键设计决策

| # | 决策 | 说明 |
|---|---|---|
| I1 | 物品实例 = `Carrier` + `ItemCoreTrait` | 复用载体、Trait、索引、序列化 |
| I2 | 堆叠 = 单 Carrier + `Count` | 拆分/合并是数量运算；避免 N 个 Carrier |
| I3 | 容器归属用 `CarrierGroupIndex<CarrierId>` | 多对一必须用分组索引，`CarrierIndex` 的双射语义会驱逐 |
| I4 | 槽位用 `CarrierIndex<ItemLocation>` | `(容器, 槽位)` 组合值唯一，双射成立 |
| I5 | 定义经 `IItemDefinitionProvider`，不暴露 Luban 类型 | 物品库零表类型知识；适配器由业务实例化 |
| I6 | 操作经 `IItemService`，返回 `ItemOperationResult` | 不抛异常；调用方可分支处理 |
| I7 | 事件用 struct，成功后发送 | 与框架事件系统一致 |
| I8 | 约束经 `IContainerConstraint` 注册表 | 数据驱动，新增容器类型不改核心代码 |
| I9 | GAS 桥接完全事件驱动 | 物品不引 GAS；`ItemEquippedEvent` → GAS 施效 |
| I10 | 物品库依赖 Config，但不依赖具体表类型 | 接口与适配器分离；换数据源只改适配器 |
| I11 | `ItemHandle` / `ContainerId` 包装 `CarrierId` | 编译期区分物品与容器，避免误传 |
| I12 | 增加 `"item.definition"` 分组索引 | 按定义查询必须走索引（28 µs vs 222 ms） |
| I13 | 查询提供「缓冲复用」与「便捷枚举」双形态 | 热路径零分配，冷路径易用 |
| I14 | 存档用容器批量快照，不用 `CarrierManager.SaveToFile` | 体积与耗时差 4~10 倍；语义单位是容器 |
| I15 | 物品 Trait 必须有**无参构造** | `Activator.CreateInstance` 重建路径 |
| I16 | **确定采用全量 Carrier**，不做冷热分离后端 | 已决策（2026-11-17）：接受内存与读档代价；目标上限约 50 万，见 §七 |

---

## 五、生命周期与调用时序

```
启动：
  var provider = new ConfigItemDefinitionProvider<cfg.item>("tbitem", MapItem, r => r.ID);
  arch.RegisterService(new ItemService(provider));           // IItemService
  arch.RegisterService(new ItemQueryService(...));          // IItemQuery
  arch.RegisterService(new ContainerService(...));          // IContainerService
  arch.GetCarrierManager().RegisterIndex(new CarrierGroupIndex<CarrierId>("item.container"));
  arch.GetCarrierManager().RegisterIndex(new CarrierIndex<ItemLocation>("item.slot"));

获得物品：
  var result = itemService.Add(backpack, 1001, 3);
  if (!result.Success) UI.Show(result.Code);

装备：
  var equip = itemService.Move(weapon, equipmentSlotContainer, slotIndex: 0);
  // 成功后 ItemService 发 ItemEquippedEvent
  // GAS 侧订阅 → 施加 Effect

存档：
  carrierManager.SerializeCarrier(containerCarrier) / SaveToFile(...)
  // 物品库不参与落盘；Serialization 结构由 Trait 决定
```

---

## 六、待确认问题

| # | 问题 | 默认建议 |
|---|---|---|
| IQ1 | `ItemHandle` / `ContainerId` 包装是否必要，还是直接用 `CarrierId` | 保留包装；误传容器与物品是高频错误 |
| IQ2 | `Add` 部分成功还是全有全无 | 全有全无（原子性优先）；如业务需要部分成功，另开 `TryAddPartial` |
| IQ3 | 装备槽容器的槽位类型约束如何表达 | 每个槽位一个 `AllowedSlot` 配置（`ContainerConstraint` 的具名实现），不写进 `ContainerTrait` |
| IQ4 | 拆分带词条物品时的复制规则 | 默认**禁止拆分**带词条的不可堆叠物品；堆叠物品若带词条，词条按数量均分并取整 |
| IQ5 | 物品标签是否复用 GAS `GameplayTag` | 定义表存字符串；GAS 侧提供 `ITagMatcher` 适配，物品库不引 GAS |
| IQ6 | `IItemQuery.CountInContainers` 是否需要缓存 | 先不缓存；数量级（数千实例）下线性遍历可接受，超出再优化 |
| IQ7 | 物品服务是否需要 Command 包装 | Service 为对外门面；Command 仅用于需要记录/回放的高层业务操作，不替代 Service |
| IQ8 | 容器销毁时物品如何处理 | 默认**禁止销毁非空容器**；`Destroy` 需先清空或调用 `DestroyWithItems` 显式处理 |
| IQ9 | ~~目标规模~~ | **已决策**：全量 Carrier；读档 30–300 s 可接受；上限由内存决定（约 50 万） |
| IQ10 | 是否需要帧末批量事件 | 高频搬运场景需要；核心先提供单事件，批量事件作为可选 |

---

## 七、性能边界

> 数据来自 2026-11-17 四份基线；完整映射见 [影响分析](../progress/2026-11-17-perf-impact-on-config-item-design.md)。

| 维度 | 安全区 | 警戒区 | 不可用区 |
|---|---|---|---|
| 物品 Carrier 总量（PC） | ≤ 20 万 | 50 万 | 100 万 |
| 单容器物品数（UI 读取） | ≤ 500 | 1 000 | 5 000 |
| 每帧搬运 | ≤ 5 000 | 10 000 | 20 000 |
| 每帧生成+销毁 | ≤ 2 000 | 5 000 | 10 000 |
| 活跃容器 tick（业务侧） | ≤ 100 | 300 | 1 000 |
| 全量存档 | 容器批量快照 | 二进制快照 | 逐物品 JSON（>1 万） |

**硬前置**：backlog A1（Trait 反序列化）修复前，物品存档不可用。

**目标规模决策（2026-11-17）**：采用**全量 Carrier**，不做冷热分离。代价如下：

| 物品总数 | 驻留内存 | 冷构建 | 读档（A1 修复后估算） |
|---|---|---|---|
| 10 万 | ≈ 85 MB | ≈ 2 s | ≈ 4 s |
| 20 万 | ≈ 170 MB | ≈ 4 s | ≈ 8 s |
| 50 万 | ≈ 425 MB | ≈ 10 s | ≈ 20 s |
| 100 万 | ≈ 850 MB | ≈ 19 s | ≈ 40 s |

读档 30–300 s 可接受；**真正的硬约束是内存**：100 万约 1.7 GB 属 Mono 风险区。**目标上限 50 万**。超过上限时再评估冷热分离（届时新增 `IItemStorage` 后端，核心 API 不变）。

冷构建 100 万约 19 s，其中约 15 s 来自 5 次完整 GC；若需要缩短启动时间，可按 1 万一批分帧构建，纯 CPU 约 3–4 s（可选优化，非必须）。

> 注：读档时间为估算值。当前 `LoadFromFile` 因 A1 缺陷跳过了 Trait 重建（10 万仅 579 ms），修复后会接近 41 µs/载体的真实成本，必须在 A1 修复后实测确认。

## 八、与 Config 的接口对齐检查

| Config 侧 | 物品侧 | 状态 |
|---|---|---|
| `ITableReader<TRow>.Get/TryGet/All` | `ConfigItemDefinitionProvider<TRow>` | ✅ 一致 |
| `DataPackConventions.PrimaryKeyField = "ID"` | 物品定义表使用大写 `ID` | ✅ 一致 |
| `DataPackConventions.VoidField = "is_void"` | 物品定义表包含 `is_void` | ✅ 一致 |
| `ITableReader.Count` 过滤废除行 | provider 不返回废除物品 | ✅ 一致 |
| 表名小写约定 | `"tbitem"` | ✅ 一致 |
| 数据源目录 `Resources/DataPacks/{packId}/` | 数据包 `dpack.core` | ✅ 一致 |

---

## 九、Unit 1 落地文件清单

```
Assets/NexusFramework.Inventory/            // 物理位置待 Q3
├── NexusFramework.Inventory.asmdef          // 引 NexusFramework + NexusFramework.Config
├── Handles/
│   ├── ItemHandle.cs
│   └── ContainerId.cs
├── Definitions/
│   ├── ItemDefinitionView.cs
│   ├── IItemDefinitionProvider.cs
│   └── ConfigItemDefinitionProvider.cs      // 泛型适配器（接口部分）
├── Traits/
│   ├── ItemCoreTrait.cs
│   ├── ItemDurabilityTrait.cs
│   ├── ItemAffixListTrait.cs
│   └── ContainerTrait.cs
├── Containers/
│   ├── IContainerService.cs
│   ├── IContainerConstraint.cs
│   ├── IContainerConstraintRegistry.cs
│   └── ContainerView.cs
├── Operations/
│   ├── IItemService.cs
│   ├── ItemOperationResult.cs
│   ├── ItemOperationCode.cs
│   └── ItemInstanceOptions.cs
├── Queries/
│   ├── IItemQuery.cs
│   ├── ItemFilter.cs
│   ├── ItemView.cs
│   └── ItemLocation.cs
├── Events/
│   └── ItemEvents.cs
├── Snapshots/
│   ├── ItemRecord.cs
│   ├── ContainerSnapshot.cs
│   └── IItemSnapshotSerializer.cs
└── InventoryConventions.cs                  // 索引表名、载体类型名、Trait 约定
```
