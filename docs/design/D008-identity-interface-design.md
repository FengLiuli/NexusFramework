---
layer: design
status: draft
topic: identity-interface
origin:
  - docs/requirements/R011-identity-system.md
  - docs/requirements/R006-data-pack-config.md
  - docs/requirements/R007-item-storage.md
  - Assets/NexusFramework/DataCarrier/CarrierId.cs
  - Assets/NexusFramework/DataCarrier/TagRuntimeIndex.cs
  - Assets/NexusFramework.GAS/Services/EcsTagService.cs
  - TestResults/id-handle-perf.txt
created: 2026-11-17
updated: 2026-11-19
---

# 设计：NexusFramework.Identity 接口契约

## 方案概述

本文是 [R011 通用标识需求](../requirements/R011-identity-system.md) 的接口设计稿：只定义类型、契约与语义，不含实现。

设计建立在两层划分上：

- **L1 持久化输入**（配置、资源、本地化、存档）只用字符串 key。
- **L2 运行时数据**（定义、类型、实例）只用 int id。
- **ID 管理器**是两者之间的桥：加载时 key→id，写档时 id→key，读档时 key→id。

L2 内部提供三个可选原语：

| 原语 | 职责 | 主要使用方 |
|---|---|---|
| `KeyIndex` | key ↔ id | 物品定义、Carrier 类型、GAS 效果/属性/标签、本地化 |
| `IdAllocator` | 纯发号 + 回收 | 临时对象、粒子、匿名实体 |
| `TypedAllocator` | 发号 + typeById | Carrier 实例、GAS ASC、效果实例 |

三者按域独立编号，不共享全局空间。id 不进入任何持久化格式。

五条硬约束：

1. L1 与 L2 的边界只在加载、注册、存档、读档四处。
2. id 是 `int`，不做泛型包装。
3. 每个域只注册一种原语。
4. 回收后的 id 可复用；旧引用可能指向新对象，使用方负责（RID19/RID30）。
5. 失败经结果类型 + 诊断返回，不抛异常中断加载流程。

---

## 一、分层与依赖

```
L1 文件/资产
  │  配置表行键、资源 key、本地化 key、存档引用
  │
  ▼  加载 / 注册 / 读档
┌─────────────────────────────────────────┐
│ L2 运行时                               │
│                                         │
│  KeyIndex        key → id               │
│  IdAllocator     ─ → id                 │
│  TypedAllocator  typeId → id + typeById │
│                                         │
│  CarrierManager / GAS / 物品 …          │
└─────────────────────────────────────────┘
  │
  ▼  写档
L1 存档：只写 key
```

程序集依赖：

```
NexusFramework.Identity（core 程序集内）
  ← NexusFramework.DataCarrier
  ← NexusFramework.GAS
  ← NexusFramework.Config（可选）
```

`NexusFramework.Identity` 不得引用 GAS、Config、物品或 Demo。物理位置：`Assets/NexusFramework/Identity/`。

---

## 二、核心契约

### 2.1 稳定 key

```csharp
namespace NexusFramework.Identity
{
    /// <summary>L1 资产键或 L2 注册名。规范化后作为相等性判定的唯一依据。</summary>
    public readonly struct IdKey : IEquatable<IdKey>
    {
        public string Value { get; }       // 规范化后的完整键
        public string Namespace { get; }   // 冒号前；legacy 键为 null
        public string Path { get; }        // 冒号后；legacy 键为整个键
        public bool IsLegacy { get; }
        public bool IsValid { get; }

        public static IdKey Parse(string value);
        public static bool TryParse(string value, bool requireNamespace, out IdKey key);

        public bool Equals(IdKey other);
        public override bool Equals(object obj);
        public override int GetHashCode();
        public override string ToString();
    }
}
```

- 冒号为唯一分隔符；`Namespace` 与 `Path` 均不得为空。
- `requireNamespace = false` 时，无冒号的键为 legacy。
- `IdKey` 只做校验与解析，不参与发号。

### 2.2 KeyIndex

```csharp
    public enum KeyIndexError
    {
        None,
        InvalidKey,
        MissingNamespace,
        NamespaceNotRegistered,
        CapacityExceeded,
        IdOutOfRange
    }

    public readonly struct KeyIndexOptions
    {
        public KeyIndexOptions(bool requireNamespace = false, int maxValue = 0,
            bool parseNamespace = true);
        public bool RequireNamespace { get; }
        public bool DisableNamespaceParsing { get; }
        public bool ParseNamespace { get; }  // DisableNamespaceParsing 的取反
        public int MaxValue { get; }         // 0 表示 int.MaxValue
    }

    public sealed class KeyIndex
    {
        public KeyIndex(string name, KeyIndexOptions options = default);

        public string Name { get; }
        public int Count { get; }        // 已有 key 数量
        public int Capacity { get; }     // 底层分配器已发过的最大槽位

        /// <summary>key → id；查不到则注册并分配。</summary>
        public int Intern(string key);

        public bool TryIntern(string key, out int id,
            out KeyIndexError error, out string diagnostic);

        /// <summary>只查，不分配。</summary>
        public bool TryGet(string key, out int id);

        /// <summary>id → key，O(1) 数组直索引。</summary>
        public bool TryGetKey(int id, out string key);

        /// <summary>删除映射并回收 id。</summary>
        public bool Remove(string key);

        /// <summary>显式绑定 key → id（配置驱动的稳定 id，如 GAS Tag Code）。</summary>
        public bool TryRegister(string key, int id,
            out KeyIndexError error, out string diagnostic);

        public bool IsAlive(int id);
        public void Clear();

        public KeyIndexDiagnostics GetDiagnostics();
    }
```

**语义**

- 内部维护 `Dictionary<string, int>`、`string[]`（id→key）与一个 `IdAllocator`。
- `Intern` 是唯一会发号的入口。
- `Remove` 删除键、清空槽位并 `Free`；后续 `Intern` 可能复用该 id（RID11）。
- `MaxValue` 在发号时校验；超过返回 `IdOutOfRange`，不截断。
- 重复键：当前实现幂等返回既有 id，不区分来源、不告警。RID14 的「同源重复视为错误」需要来源信息，本期不提供（`DuplicateKey` 分支已从枚举移除）。
- `TryRegister` 用于配置驱动的稳定 id：key 已绑定同一 id 时幂等成功；key 或 id 已被其它绑定占用时返回 `IdConflict`，不覆盖。显式 id 不经过自动发号，`Intern` 会跳过它们。
- `parseNamespace = false` 时冒号按普通字符处理，只做首尾裁剪；供 Tag 这类自有键规则的索引器使用。`default(KeyIndexOptions)` 等价于解析开启。

### 2.3 IdAllocator

```csharp
    /// <summary>纯发号与回收；不持有任何字符串。槽位连续，范围 [1, Capacity]。</summary>
    public sealed class IdAllocator
    {
        public IdAllocator(int initialCapacity = 0);

        public int Allocate();
        public bool TryAllocate(out int id);

        /// <summary>重复 Free、Free(0)、越界 id 返回 false。</summary>
        public bool Free(int id);

        public bool IsAlive(int id);
        public int Count { get; }
        public int Capacity { get; }     // 单调不减

        public void Clear();
    }
```

- 从 `1` 开始；内部维护空闲表，`Allocate` 优先复用。
- 无代际；`Free` 后槽位可被再次分配（RID19）。

### 2.4 TypedAllocator

```csharp
    /// <summary>发号并记录 typeId（discriminator）。</summary>
    public sealed class TypedAllocator
    {
        public TypedAllocator(string name, int initialCapacity = 0);

        public string Name { get; }
        public int Count { get; }
        public int Capacity { get; }

        public int Allocate(int typeId);
        public bool TryAllocate(int typeId, out int id);

        public bool Free(int id);
        public bool IsAlive(int id);

        /// <summary>id → typeId，O(1)。</summary>
        public bool TryGetType(int id, out int typeId);
        public bool IsType(int id, int typeId);

        public void Clear();
    }
```

- 内部维护一个 `IdAllocator` 与 `typeById`（数组或分块数组）。
- `typeId` 由调用方提供，通常来自某个 `KeyIndex`。
- 类型数量只受 `int` 限制；不把类型编码进 id。
- 回收与 `IdAllocator` 一致。

### 2.5 IdRegistry

```csharp
    /// <summary>每架构一份。架构创建时构造，Shutdown 时 Clear。</summary>
    public sealed class IdRegistry
    {
        public KeyIndex GetOrCreateIndex(string name, KeyIndexOptions options = default);
        public IdAllocator GetOrCreateAllocator(string name);
        public TypedAllocator GetOrCreateTypedAllocator(string name, int initialCapacity = 0);

        public bool TryGetIndex(string name, out KeyIndex index);
        public bool TryGetAllocator(string name, out IdAllocator allocator);
        public bool TryGetTypedAllocator(string name, out TypedAllocator allocator);

        public IReadOnlyCollection<string> Names { get; }

        public bool Remove(string name);
        public void Clear();
    }

    /// <summary>命名空间归属校验的可注入来源。内容包系统未建时可为空。</summary>
    public interface IIdNamespaceAuthority
    {
        bool IsRegistered(string namespaceId);
    }
```

**语义**

- 同名同类型 → 返回既有实例；同名不同类型 → 返回 `null` 并记录错误（RID32）。同名同类型但选项不同 → 返回既有实例并告警。
- `IdRegistry` 只做持有与生命周期，不含发号或映射逻辑。
- `RequireNamespace = true` 的索引从所属架构取 `IIdNamespaceAuthority`；缺失时放行并计入 `KeyIndexDiagnostics.AuthorityUnavailableCount`（RID13）。

### 2.6 Carrier 集成

```csharp
namespace NexusFramework.DataCarrier
{
    public sealed class CarrierManager : ICarrierManager
    {
        public CarrierManager(IdRegistry registry);

        // 类型（L2 注册名，不是 L1 资产）
        public int RegisterType(string typeKey);
        public bool IsTypeRegistered(string typeKey);
        public int GetTypeId(string typeKey);
        public string GetTypeKey(int typeId);

        // 实例
        public int CreateCarrier(int typeId);
        public int CreateCarrier(string typeKey);
        public bool DestroyCarrier(int carrierId);
        public bool IsCarrierValid(int carrierId);
        public int GetCarrierTypeId(int carrierId);
        public IEnumerable<int> GetCarriersByType(int typeId);
        public int GetCarrierCount(int typeId);
        public IEnumerable<int> GetAllCarriers();
    }
}
```

内部结构：

```
mTypeIndex      : KeyIndex         ("carrier.type")
mInstances      : TypedAllocator   ("carrier.instance")
mCarriersByType : Dictionary<int, HashSet<int>>         // 二级索引
mCarrierTraits  : Dictionary<int, Dictionary<Type, IDataTrait>>
```

- `RegisterType(typeKey)` → `mTypeIndex.Intern(typeKey)`。
- `CreateCarrier(typeId)` → `mInstances.Allocate(typeId)` → 入 `mCarriersByType`。
- `GetCarrierTypeId(id)` → `mInstances.TryGetType(id, out typeId)`。
- `DestroyCarrier(id)` → 清理 Trait/索引/Tag → `mInstances.Free(id)`。
- `mTypeIndex` 的 `MaxValue` 不设上限（默认 `int.MaxValue`）；类型数量到 10 万级无需额外配置。

**序列化**

```csharp
public sealed class CarrierSerializationData
{
    public string typeKey;                 // 只写 key，不写 id
    public TraitSerializationData[] traits;
    public string[] tags;
}

public sealed class TraitSerializationData
{
    public string typeName;
    public string assemblyQualifiedName;
    public string data;
    public IdRefSerializationData[] idRefs; // 新字段；旧存档为 null
}

public sealed class IdRefSerializationData
{
    public string domain;                  // IdRegistry 域名
    public string key;                     // 稳定 key
}
```

- 读档：`RegisterType(typeKey)` → `CreateCarrier(typeId)`。
- Trait 内的 ID 引用（`IIdRefTrait`）：写档时 `CaptureIdRefs` 把字段临时置 0、框架把 id 转 key 写入 `idRefs`，随后 `ApplyIdRefs` 恢复对象；读档时先按 key 解析 id，`FromJson` 后再 `ApplyIdRefs` 写回。id 不进入存档。

### 2.7 Tag 集成

TagService 分为**映射层**与**挂载层**。映射层由 `KeyIndex` 提供，挂载层留在 TagService。

```csharp
public sealed class CarrierTagService : ITagService
{
    public CarrierTagService(ICarrierManager carriers, KeyIndex tagIndex);

    // 映射层（委托 KeyIndex）
    public int InternTag(string key);
    public bool TryGetTagId(string key, out int tagId);
    public string GetTagKey(int tagId);
    public bool MatchesOrIsAncestor(int actualTagId, int queryTagId);

    // 挂载层（自有状态）
    // tagsByCarrier: Dictionary<int, HashSet<int>>
    // carriersByTag: Dictionary<int, HashSet<int>>
    // ancestorsByTag: Dictionary<int, int[]>    // 点分层级推导
    // 写入、查询、反查、事件
}
```

**合并边界**

| 层 | 归属 | 说明 |
|---|---|---|
| key→slot 映射 | `KeyIndex("core.tag")` | `TagRuntimeIndex` 删除 |
| slot→key 反查 | `KeyIndex.TryGetKey` | 数组直索引 |
| carrier→tags 挂载 | `CarrierTagService` | 领域状态 |
| tag→carriers 反查 | `CarrierTagService` | 领域状态 |
| 层级祖先链 | `CarrierTagService` | 首次 `Intern` 时按 `.` 分段计算并缓存 |
| 查询/事件 | `CarrierTagService` | 领域语义 |

**语义**

- `TagRuntimeIndex` 删除，`KeyIndex` 提供 key↔slot；`RuntimeTagSlot` 保留为对外值类型，内部值 = KeyIndex id。
- 层级（`a.b.c` 的祖先链）不归 `KeyIndex` 管；`CarrierTagService` 在首次 `Intern` 时计算 `ancestorsByTag`，供 `MatchesOrIsAncestor`、`HasTag`、`FindCarriersWithTag` 使用。
- Tag 索引器不启用 `Remove`；现有 API 没有删除 key 的操作，避免 slot 回收与挂载表、祖先链脱节。
- Tag 的 key 规范化沿用 `TagKey.Normalize`（点分层）；`KeyIndex` 只做通用的空值与首尾空白校验，不强制命名空间。Tag 索引器以 `RequireNamespace = false`、`parseNamespace = false` 创建，冒号按普通字符处理。
- GAS 的 `EcsTagService` 使用独立 `KeyIndex("gas.tag")`；Tag id 直接取自配置表 Code，通过 `TryRegister` 显式绑定。其配置驱动的封闭集合与层级语义留在 GAS，不与 Core Tag 共享编号。

**作用域**

Tag 索引器按架构持有，不再随每个 `CarrierManager` 独立。当前架构只创建一个 `CarrierManager`，行为等价。若未来一个架构有多个 `CarrierManager`，必须为每个 manager 分配独立 Tag 域名（如 `core.tag.{managerId}`），否则挂载表会与共享的 slot 空间错配。

---

## 三、关键设计决策

| # | 决策 | 说明 |
|---|---|---|
| C1 | L1/L2 分层 | id 不持久化；持久化只写 key（RID1–RID6） |
| C2 | 三个原语按域独立 | 不共享全局编号空间；无全局分配器争用 |
| C3 | 完全裸 int | 不做泛型包装；类型安全由模块边界与命名约定保证 |
| C4 | 类型用 `int`，不编进位域 | `TypedAllocator` 的 `typeById` 是 discriminator |
| C5 | 句柄从 1 开始，0 无效，回收复用 | 与现有 `RuntimeTagSlot` 语义一致 |
| C6 | 无代际检测 | 旧 id 可能指向新对象，契约写明 |
| C7 | 每架构一份 `IdRegistry` | 与 `CarrierManager` / `TagService` 同级 |
| C8 | `CarrierId` 彻底移除 | 类型走 `KeyIndex`，实例走 `TypedAllocator` |
| C9 | 序列化只写 key | 读档重建 id，不依赖旧 id |
| C10 | 失败经结果类型 + 诊断 | 与 D006 一致 |

---

## 四、生命周期与调用时序

### 启动

```
ArchitectureFactory.CreateArchitecture(...)
  → Architecture ctor
      → new IdRegistry()
      → new CarrierManager(registry)
          → registry.GetOrCreateIndex("carrier.type")
          → registry.GetOrCreateTypedAllocator("carrier.instance")
```

### 配置加载（L1 → L2）

```
foreach row in tbitem:
    var defId = ids.GetOrCreateIndex("item.definition").Intern(row.ID)   // "core:item.potion" → 1
    // 运行时只持有 defId
```

### Carrier 创建与查询

```
var playerType = manager.RegisterType("core:player");     // typeId
var carrier    = manager.CreateCarrier(playerType);       // instanceId

manager.GetCarrierTypeId(carrier);                        // typeId
manager.GetCarriersByType(playerType);                    // 二级索引
```

### 销毁与复用

```
manager.DestroyCarrier(carrier);
  → 清理 Trait / 索引 / Tag
  → mInstances.Free(carrier)

// 旧 id 可能被下一次 CreateCarrier 复用 —— RID19 契约
```

### 存档（L2 → L1）

```
SerializeCarrier(carrier)
  → typeKey = mTypeIndex.TryGetKey(typeId)
  → traits: 每个 ID 引用字段由 IdRegistry 转成 key
  → 输出 { typeKey, traits, tags }
  // 不写 instanceId
```

### 读档（L1 → L2）

```
DeserializeCarrier(json)
  → typeId = mTypeIndex.Intern(json.typeKey)
  → carrier = mInstances.Allocate(typeId)
  → traits: key → id
  → 恢复 Trait / Tag
```

### 架构关闭

```
arch.Shutdown()
  → CarrierManager.Dispose()
  → TagService.Dispose()
  → IdRegistry.Clear()
```

---

## 五、与现有代码的兼容（CarrierId 移除）

破坏性变更。当前代码库共 37 个 `.cs` 文件引用 `CarrierId`：

| 位置 | 处理 |
|---|---|
| `DataCarrier/CarrierId.cs` | 删除 |
| `DataCarrier/CarrierManager.cs` | 全部 `CarrierId` 参数/返回改 `int` |
| `DataCarrier/CarrierIndex.cs` / `CarrierGroupIndex.cs` / `ICarrierIndex.cs` | 键类型改 `int` |
| `DataCarrier/ITagService.cs` / `CarrierTagService.cs` | 参数改 `int` |
| `DataCarrier/ICarrierEventCallback.cs` / `CarrierLifecycleEvents.cs` / `CarrierTagEvents.cs` | 字段改 `int` |
| `DataCarrier/TagRuntimeIndex.cs` | 内部改 `KeyIndex`；`RuntimeTagSlot` 不变 |
| `Framework.cs` / `FrameworkArchitecture.cs` / `ArchitectureFactory.cs` | 架构构造注入 `IdRegistry`；扩展方法改 `int` |
| GAS 全量（`GASAttributeBindable`、`GASCommands`、`GASEvents`、`GASArchitecture`、`GASEntityMapModel`、`GASQueries`、`AbilityService`、`AttributeService`、`EcsTagService`、`EffectService`） | 参数/字段改 `int` |
| `Demo/Scripts/DemoSceneManager.cs` / `DemoUnitUI.cs` | 改 `int` |
| 测试（Core / ECS / Services / Stress） | 改名与断言调整 |
| 存档格式 | `carrierId` 字段移除，改 `typeKey` |

**迁移建议**

1. 先引入 `Identity` 与 `KeyIndex` / `IdAllocator` / `TypedAllocator`。
2. `CarrierManager` 内部切到新结构；对外暂时保留 `CarrierId` 过渡别名（仅一个版本周期）。
3. 全仓库替换为 `int`。
4. 删除 `CarrierId` 与过渡代码。
5. 旧存档不做运行时兼容；需要时单独写迁移工具。

---

## 六、测试要求

| # | 测试 | 对应需求 |
|---|---|---|
| T1 | `KeyIndex`：Intern 幂等、TryGet、TryGetKey、Remove 回收 | RID7–RID11 |
| T2 | `KeyIndex`：规范化、legacy 键、命名空间校验 | RID12/RID13 |
| T3 | `KeyIndex`：重复键幂等；命名空间拒绝与 authority 缺失计数 | RID14/RID15 |
| T4 | `IdAllocator`：发号从 1 起、Free 复用、重复 Free、越界、Clear | RID16–RID22 |
| T5 | `IdAllocator`：槽位连续，`Capacity` 单调不减 | RID20/RID21 |
| T6 | `TypedAllocator`：Allocate(typeId)、TryGetType、IsType、Free | RID24–RID30 |
| T7 | `TypedAllocator`：类型数量超过 `ushort` 范围仍可分配 | RID27 |
| T8 | 每域独立编号：同名 id 在不同域互不影响 | RID31/RID32 |
| T9 | 同名域以不同原语创建被拒绝 | RID32 |
| T10 | 架构重启后旧 id 失效 | RID34 |
| T11 | Tag：`KeyIndex` 替代 `TagRuntimeIndex`；挂载、反查、层级仍由服务维护 | RID36–RID40 |
| T12 | Carrier：类型注册、实例分配、销毁复用、`GetCarrierTypeId` | RID42–RID46 |
| T13 | Carrier：读档按 `typeKey` 重建；缺失键拒绝 | RID47/RID48 |
| T13b | Trait 内 id 引用：存档只写 key，读档按 key 重建新 id | RID3/RID47 |
| T14 | 基准：Carrier/Tag 路径相对现有基线不回退 >5% | RID50 |
| T15 | 如需：IL2CPP Player 侧复测 | RID51 |

---

## 七、待确认问题

| # | 问题 | 默认建议 |
|---|---|---|
| DQ1 | API 是否保留非泛型强类型别名（如 `CarrierHandle` 包装 int） | 不保留；完全裸 int，靠命名区分 |
| DQ2 | `KeyIndex.Remove` 在 Tag 索引器是否启用 | 不启用；现有 Tag 无删除 key |
| DQ3 | `typeById` 用连续数组、分块数组还是字典 | 连续数组；容量大时切分块 |
| DQ4 | 域名的分层规范与命名空间治理 | 模块前缀 + 点分层；文档约定 |
| DQ5 | Trait 内 ID 引用字段的声明方式 | 已落地：实现 `IIdRefTrait`（`CaptureIdRefs` / `ApplyIdRefs`），框架侧写 `idRefs`（domain + key），不做反射 |
| DQ6 | 旧存档迁移工具是否本期交付 | 不交付；先拒绝并诊断 |
| DQ7 | `CarrierManager` 是否依赖窄接口而非整个 `IdRegistry` | 依赖窄接口，降低耦合 |

---

## 八、落地文件清单

```
Assets/NexusFramework/Identity/
├── IdKey.cs
├── KeyIndex.cs
├── KeyIndexOptions.cs
├── KeyIndexError.cs
├── KeyIndexDiagnostics.cs
├── IdAllocator.cs
├── TypedAllocator.cs
├── IdRegistry.cs
└── IIdNamespaceAuthority.cs

Assets/NexusFramework/DataCarrier/
├── CarrierId.cs                // 删除
├── CarrierManager.cs           // 改：类型=KeyIndex，实例=TypedAllocator
├── TagRuntimeIndex.cs          // 改：KeyIndex 封装
└── RuntimeTagSlot.cs           // 不变

Assets/NexusFramework/Framework.cs                 // 改：IArchitecture.GetIdRegistry()
Assets/NexusFramework/FrameworkArchitecture.cs     // 改：IdRegistry 生命周期

Assets/Tests/Core/
├── KeyIndexTests.cs
├── IdAllocatorTests.cs
├── TypedAllocatorTests.cs
└── CarrierMigrationTests.cs
```

---

## 九、实施顺序

1. 新增 `Identity/` 三原语与单元测试（不改现有模块）。
2. `CarrierManager` 内部切到「类型索引 + 类型化分配器」，对外保留 `CarrierId` 过渡别名。
3. 全仓库 `CarrierId` 引用改 `int`；跑 Core / GAS / Stress 测试。
4. 删除 `CarrierId` 与过渡代码。
5. `TagRuntimeIndex` 改 `KeyIndex` 封装；跑 Tag 测试与性能基线。
6. GAS `EcsTagService` 接入独立 `KeyIndex`。
7. 补基准、旧存档处理与迁移记录。
