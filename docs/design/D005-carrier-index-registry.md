---
layer: design
status: draft
created: 2026-10-10
updated: 2026-10-10
---

# 设计：Carrier 索引表注册机制（Carrier Index Registry）

## 方案概述

项目里反复出现同一个需求：**围绕 `CarrierId` 搭一张「投影索引」**——把 `CarrierId` 映射到某个值，或者反过来由某个 key 反查到载体集合。

典型现状：

| 现有实现 | 索引形态 | 手写成本 |
|---|---|---|
| `GASEntityMapModel` | `CarrierId ↔ Entity`、`Entity ↔ GameObject` | 4 个 `Dictionary` + `Clear()` |
| `CarrierManager.traitReverseIndex` | `Trait 类型 → {CarrierId}` | 内联在载体管理器里 |
| （业务中反复出现） | `CarrierId ↔ CarrierId`、`CarrierId ↔ string`、分组查询 | 每处重写一遍 |

这些索引的**结构完全同构**，且都踩同一个坑：**载体销毁后忘了清理 → 脏条目/内存泄漏**。

本设计将这类"针对 Carrier 的搜索表"抽象为框架级的一等公民：

- 提供通用的**索引容器类型**（映射 / 分组），承载各类键值形态；
- **索引由「表名」标识**（而非 C# 类型），注册与生命周期托管归 `ICarrierManager`；
- **载体销毁时由 `CarrierManager` 自动派发清理**，无需业务手写 `Clear`，也无事件订阅时序问题。

设计目标（对齐 `NexusFrameworkDevelopmentRules` 的"避免过度工程化"）：
1. **只解决高频共性**——索引结构与销毁清理；不引入查询 DSL、不接管业务语义。
2. **零侵入可选用**——不改任何现有基类；现有 `GASEntityMapModel` 可选择性迁移。
3. **落在框架层**——`NexusFramework` 程序集，不依赖 ECS，任何 Architecture 可用。

### 关键决策 A：为什么由 `ICarrierManager` 托管

初稿曾把托管做成"业务 Model 订阅 `CarrierDestroyedEvent` 清理"。改为 `ICarrierManager` 托管后更优：

| 维度 | 事件订阅式（否决） | CarrierManager 托管式（本稿） |
|---|---|---|
| 托管者 | Model/Service 基类 | `CarrierManager`（载体权威） |
| 清理触发 | 订阅 `CarrierDestroyedEvent` | `DestroyCarrier` 内部直接派发 |
| 时序脆性 | **有**：订阅须先于销毁，否则漏清理 | **无**：注册即受管，无窗口期 |
| 基类改动 | 需改 `AbstractModel`/`AbstractService` | **无** |
| 框架层 ECS 依赖 | 无 | 无（泛型在业务程序集实例化） |
| 内部 `traitReverseIndex` 统一 | 无路径 | 可统一（P2，见 §六） |

### 关键决策 B：用「表名」而非「类型」作索引标识

索引的**身份 = 它的业务语义（表名）**，而不是它的 C# 类型。`CarrierIndex<CarrierId>` 只是一段可复用的实现，同一类型会派生多张语义不同的表：

```
CarrierIndex<CarrierId>  "unit.target"   源 → 目标
CarrierIndex<CarrierId>  "unit.master"   主人 → 随从
CarrierIndex<CarrierId>  "summon.owner"  召唤物 → 召唤者
```

**若用类型作 key，这三张表会直接撞车、无法区分。** 用表名则天然隔离。

| 维度 | 类型作 key（否决） | 表名作 key（本稿） |
|---|---|---|
| 同类型多实例 | **无法区分** → 撞车 | 按语义名隔离 |
| `CarrierId ↔ CarrierId` / `↔ string` 高频场景 | 类型同构，全冲突 | 各自独立成表 |
| 跨组件共享 | 只能靠具体类型，耦合实现 | 按语义名取用，解耦 |
| 诊断可读性 | 日志/Inspector 显示 `CarrierIndex\`1` | 显示业务表名 |

**代价与对冲**：
- 失去类型即身份 → 用泛型 `TryGetIndex<TIndex>(name, out index)` 取回时做类型校验，类型不符返回 false。
- 「魔法字符串」风险 → **约定集中常量类**（见 §3.3），框架不强制但给出模式。
- 表名重名 → `RegisterIndex` 检测并 `LogWarning`（见 §4.3）。

---

## 一、核心概念

一个 **Carrier 索引表**（`ICarrierIndex`）以表名标识，是一张"以 Carrier 为主语"的搜索表。按 key 形态分两类：

### 1. 映射索引（Mapping）—— 一对一

```
CarrierId  ──►  TValue          （主映射）
TValue     ──►  CarrierId       （反向映射，可选）
```

- 用途：`CarrierId ↔ Entity`、`CarrierId ↔ GameObject`、`CarrierId ↔ CarrierId`、`CarrierId ↔ string`。
- 约束：**双射**——一个 Carrier 只映射一个值，一个值只属于一个 Carrier。
- 反向映射**始终维护**（`值 → CarrierId`，如 `Entity → CarrierId`、`string → CarrierId`）。这不是可选优化——**值冲突采用覆盖语义，写入时必须按值反查旧主并驱逐**（见 §2.2 规则 2），故反查能力为必需。

### 2. 分组索引（Grouping）—— 一对多

```
(TKey)  ──►  {CarrierId ...}
```

- 用途：`Trait 类型 → 载体集合`（即现有 `traitReverseIndex`）、`阵营 → 单位集合`、`场景ID → 实体集合`。
- 一个 Carrier 在同一分组键下只出现一次（集合语义）。

> `CarrierId ↔ CarrierId`、`CarrierId ↔ string` 都是**映射索引**在 `TValue = CarrierId` / `TValue = string` 时的实例，无需单独类型——靠表名区分。

---

## 二、类型设计

### 2.1 索引契约

```csharp
namespace NexusFramework.DataCarrier
{
    /// <summary>
    /// Carrier 索引表契约。载体销毁时由 CarrierManager 回调 OnCarrierRemoved 清理条目。
    /// </summary>
    public interface ICarrierIndex
    {
        /// <summary>表名。同一 Architecture 内唯一，标识索引的业务语义。</summary>
        string Name { get; }

        /// <summary>移除该载体在本索引中的所有条目（销毁清理入口）。</summary>
        void OnCarrierRemoved(CarrierId carrier);

        /// <summary>本索引当前承载的条目数（诊断用）。</summary>
        int EntryCount { get; }
    }
}
```

### 2.2 索引容器

```csharp
/// <summary>CarrierId ↔ TValue 双射索引表。表名在构造时指定，反向映射始终维护。</summary>
public sealed class CarrierIndex<TValue> : ICarrierIndex { ... }

/// <summary>按 TKey 分组的集合索引表：(TKey) → {CarrierId}。表名在构造时指定。</summary>
public sealed class CarrierGroupIndex<TKey> : ICarrierIndex { ... }
```

**`CarrierIndex<TValue>` API：**

| 成员 | 说明 |
|---|---|
| `CarrierIndex(string name)` | 表名（反向映射始终维护，无需开关） |
| `string Name { get; }` | 表名 |
| `bool Bind(CarrierId carrier, TValue value)` | 建立映射。必须遵守下方「映射写入三规则」 |
| `bool Unbind(CarrierId carrier)` | 解除该载体映射 |
| `bool TryGet(CarrierId carrier, out TValue value)` | 正查 |
| `CarrierId TryGetCarrier(TValue value)` | 反查（反向映射始终维护，恒可用） |
| `bool Contains(CarrierId carrier)` | 存在性 |
| `IEnumerable<CarrierId> Carriers` | 全部载体（诊断/遍历） |
| `void OnCarrierRemoved(CarrierId carrier)` | 销毁清理（契约实现） |

**`CarrierGroupIndex<TKey>` API：**

| 成员 | 说明 |
|---|---|
| `CarrierGroupIndex(string name)` | 表名 |
| `string Name { get; }` | 表名 |
| `void Add(TKey key, CarrierId carrier)` | 加入分组 |
| `bool Remove(TKey key, CarrierId carrier)` | 移出分组 |
| `void RemoveAll(TKey key)` | 清空某分组 |
| `IEnumerable<CarrierId> Get(TKey key)` | 反查分组 |
| `IEnumerable<TKey> KeysOf(CarrierId carrier)` | 某载体所属的所有键 |
| `void OnCarrierRemoved(CarrierId carrier)` | 销毁清理（契约实现） |

**映射写入三规则（`CarrierIndex<TValue>.Bind` 必须遵守，否则产生脏条目）：**

1. **载体有效性**：写入前校验 `IsCarrierValid(carrier)`，无效则 `LogWarning` + 拒绝。否则会写入**永不被清理的孤儿条目**——已销毁/从未创建的载体不会再触发 `OnCarrierRemoved`（`CarrierManager.DestroyCarrier` 对不存在的载体在存在性检查处提前 `return false`，不派发，见 `CarrierManager.cs:290-291`）。
2. **值冲突 → 覆盖（后者覆盖前者）**：若 `value` 已被**另一个** Carrier 持有，则**驱逐旧主**——移除旧 Carrier 的正向条目，`value` 归后者所有（`reverse[value] = 新carrier`）。返回 true。
3. **自我重绑**：同一 Carrier 重复 `Bind` → 覆盖其旧值，且**必须先清理旧值的反向条目**（否则反向字典残留悬垂）。

> **规则 2「覆盖」的强制性连带要求**：驱逐旧主意味着 **`CarrierIndex` 的正向字典必须支持按值反查旧主**——否则无法找到并移除旧条目。即 **`reverse` 参数对 `CarrierIndex` 实为强制**（见下）。
>
> **规则 2 的语义后果（业务必读）**：
> - ① **旧 Carrier 静默失去绑定**——被驱逐的 Carrier 之后 `TryGet(旧carrier)` 返回 false，业务不会收到任何通知。这是覆盖语义的固有代价。
> - ② **反向映射与正向镜像一致**：`reverse[v] == 当前持有 v 的 Carrier`，恒成立（不再出现正反不一致）。
> - ③ **`CarrierId ↔ CarrierId` 多对一场景需重新评估**：若一张表允许"多个源指向同一目标"（如多单位锁定同一目标），覆盖语义会**互相驱逐、只剩最后一个**。此类"多对一"**不要用 `CarrierIndex<CarrierId>`**，应用 `CarrierGroupIndex<CarrierId>`（`target → {源集合}`）。**一张 `CarrierIndex` 表 = 一个双射关系**。

> ⚠ **这是初稿最严重的缺陷**：初稿只写了"覆盖并清理旧值"，未定义**值冲突**路径。按初稿实现，`Bind(A,v)` 后 `Bind(B,v)` 会留下 `forward[A]=v` 悬垂，随后 `OnCarrierRemoved(A)` 会**误删 `reverse[v]=B`**。现有 `GASEntityMapModel` 已是同款不对称写法（`GASEntityMapModel.cs:24-37`），迁移前必须修正。**本稿的「覆盖 + 强制反向」正是修法**：写入即驱逐旧主，正反恒镜像，`OnCarrierRemoved` 再带所有权守卫兜底。

**`OnCarrierRemoved` 实现要点（必须清理载体在索引中的**全部角色**）：**

一个 `CarrierId` 在索引里可能以两种角色出现，销毁时必须两种都清，否则留悬垂引用：

| 容器 | 角色 A：主语（key/成员） | 角色 B：被引用对象 |
|---|---|---|
| `CarrierIndex<TValue>` | 载体是 key → 清 `_forward[carrier]` + `_reverse` | `TValue = CarrierId` 时，载体是被指向的目标 → 清**所有 `value == carrier` 的条目** |
| `CarrierGroupIndex<TKey>` | 载体是成员 → 从所属分组移除 | `TKey = CarrierId` 时，载体是分组 key → 清**该分组整体** |

- `CarrierIndex.OnCarrierRemoved`：先清 `_forward[carrier]`（含所有权守卫清反向）；**若 `TValue` 为 `CarrierId`，再经 `_reverse[carrier]` 反查指向该载体的 source 并 `Unbind` 之**（写入规则 2 保证同一 value 至多一个持有者，故至多一条，见下 §2.2.1）。
- `CarrierGroupIndex.OnCarrierRemoved`：用 `CarrierId → {TKey}` 反向账本清其成员身份；**若 `TKey` 为 `CarrierId`，另清 `groups[carrier]` 这一整组**（见下 §2.2.1）。

> **`CarrierGroupIndex` 的反向账本**：需维护 `CarrierId → {TKey}` 才能 O(#键) 精确清理成员身份，否则退化为全表扫描。**这是唯一需要额外内部账本的地方。**

### 2.2.1 引用完整性（Reference Integrity）：被引用 Carrier 的销毁清理

当 `TKey`/`TValue` 为 `CarrierId` 时，该 `CarrierId` 是**被引用的对象**，而非仅主语。载体销毁时若不清理这些引用，索引会残留指向死载体的悬垂条目——后续 `Get`/`TryGetCarrier` 会吐出已销毁的 `CarrierId`。

**语义（本稿定案）：载体销毁 = 从所有索引中彻底消失——凡以它为成员、key、或值的条目，一并清除。**

分两种情形，**代价不对称**，必须区别对待：

**情形 ①：`CarrierGroupIndex<CarrierId>` 的 `TKey` 为被销毁载体** → **清该组，无代价**
`groups[carrier]` 整组删除。即"以该载体为分组键的集合"随载体消失，语义自然（该分组本身就没有意义了）。同时该载体作为成员若还在别的分组，按常规清理。

**情形 ②：`CarrierIndex<X, CarrierId>`（如源→目标）中被销毁载体是 value** → **级联驱逐引用者，语义正确，但有影响**
`source → deadTarget` 变成悬垂。处置：经 `_reverse[carrier]` 反查指向该载体的 source，`Unbind(source)` 驱逐（**双射不变量下至多一个**——写入规则 2 保证同一 value 只被一个 Carrier 持有）。

> **这是"级联副作用"，业务必须知情**：销毁一个"目标"，会静默解除多个"源"对它的引用（那些源的 `TryGet` 随之返回 false）。若业务需要响应（如"目标没了，锁定的单位应转移目标"），应**订阅 `CarrierDestroyedEvent` 自行处理**——索引只保证不悬垂，不代替业务决策。

**实现策略（`CarrierIndex<TValue>` 的 `OnCarrierRemoved`）：**

```csharp
public void OnCarrierRemoved(CarrierId carrier)
{
    // 角色 A：载体作主语（key）
    if (_forward.TryGetValue(carrier, out var value))
    {
        _forward.Remove(carrier);
        if (_reverse.TryGetValue(value, out var owner) && owner == carrier) // 所有权守卫
            _reverse.Remove(value);
    }

    // 角色 B：载体作被引用值（仅 TValue == CarrierId）
    // 双射不变量下 _reverse[carrier] 至多一个 source，直接反查即可，无需遍历收集。
    if (typeof(TValue) == typeof(CarrierId) &&
        _reverse.TryGetValue((TValue)(object)carrier, out var source) && source != carrier)
        Unbind(source);
}
```

> `TValue == CarrierId` 用 `typeof` 静态缓存判定，**每次销毁仅一次类型检查**。反查是 O(1)——`_reverse` 直接命中，无需快照收集（初稿曾设想"遍历所有 source"，在双射不变量下多余）。

**实现策略（`CarrierGroupIndex<TKey>` 的 `OnCarrierRemoved`）：**

```csharp
public void OnCarrierRemoved(CarrierId carrier)
{
    // 角色 A：载体作成员
    foreach (var key in _keysByCarrier[carrier]) _groups[key].Remove(carrier);
    _keysByCarrier.Remove(carrier);

    // 角色 B：载体作分组 key（仅 TKey == CarrierId）→ 清整组
    // 注意：须用 RemoveAll（一并反清成员的 _keysByCarrier），而非 _groups.Remove——
    // 后者只删组，会给"既是组员、又是它组 key"的载体留下悬垂账本。
    if (typeof(TKey) == typeof(CarrierId))
        RemoveAll((TKey)(object)carrier);
}
```

**边界**：
- 销毁**目标**（角色 B）**不**触发引用者（source）自身的销毁或级联——只解除引用，source 存活。
- `CarrierIndex` 中若某 source 指向的 target 与 source 自身相同（自引用），角色 A 先行清理，角色 B 遍历时已无该条。
- 值冲突覆盖（§2.2 规则 2）与角色 B 清理**不会冲突**：驱逐旧主只动 `_forward`，角色 B 只动 `_reverse` 的指向条目，二者互不干扰。

---

**有效性校验的落地**：`CarrierIndex` / `CarrierGroupIndex` 实现一个**可选**接口 `internal interface ICarrierIndexOwner { void SetOwner(ICarrierManager owner); }`；`CarrierManager.RegisterIndex` 在注册时对实现了该接口的索引注入自身，容器据此在 `Bind`/`Add` 前校验。不实现该接口的索引不做校验（框架不强制），但需业务自行保证「先建载体再绑定」。

> **不做的事**（明确边界，防止过度工程化）：
> - 不引入表达式树 / lambda 投影（`x => x.someField`）——分组键由业务算好传入。
> - 不内建 Trait 字段索引（如"按 `BasicInfoTrait.name` 查询"）——需要时业务在 `AddTrait` 处同步维护一个 `CarrierGroupIndex<string>`。
> - 不做序列化——索引是运行期派生数据，随载体重建。
> - 不内置表名常量——具体表名属业务语义，由项目自行定义（见 §3.3）。

### 2.3 注册表：`ICarrierManager` 扩展（名称索引）

`CarrierManager` 是索引的**寄存器（registry）**，按 `Name` 管理，只负责注册/查询/销毁派发，**不创建**索引。

```csharp
public interface ICarrierManager : IDisposable, ICanInit
{
    // ... 现有成员 ...

    /// <summary>注册索引表（以 index.Name 为键）。重名或无效应被拒绝。</summary>
    void RegisterIndex(ICarrierIndex index);

    /// <summary>按表名注销。返回是否确实移除。</summary>
    bool UnregisterIndex(string name);

    /// <summary>按表名获取（非泛型）。不存在返回 null。</summary>
    ICarrierIndex GetIndex(string name);

    /// <summary>按表名 + 类型安全获取。名字不存在或类型不符返回 false。</summary>
    bool TryGetIndex<TIndex>(string name, out TIndex index) where TIndex : class, ICarrierIndex;

    /// <summary>全部索引（诊断）。</summary>
    IReadOnlyCollection<ICarrierIndex> GetAllIndexes();
}
```

`CarrierManager` 内部：

```csharp
private readonly Dictionary<string, ICarrierIndex> indexesByTable = new();

public void RegisterIndex(ICarrierIndex index)
{
    if (index == null || string.IsNullOrEmpty(index.Name))
    {
        Debug.LogWarning("[CarrierManager] RegisterIndex skipped: null index or empty name");
        return;
    }

    lock (lockObject)
    {
        // 同实例重复注册 → no-op
        if (indexesByTable.TryGetValue(index.Name, out var existing))
        {
            if (ReferenceEquals(existing, index)) return;
            // 重名不同实例 → 拒绝覆盖，保留先者（见 §4.3）
            Debug.LogWarning($"[CarrierManager] RegisterIndex skipped: table name '{index.Name}' already registered by {existing.GetType().Name}");
            return;
        }
        indexesByTable[index.Name] = index;
    }

    // 注入 owner 供索引做载体有效性校验（见 §2.2 三规则之规则 1）
    if (index is ICarrierIndexOwner owned) owned.SetOwner(this);
}

public bool UnregisterIndex(string name)
{
    lock (lockObject) return indexesByTable.Remove(name);
}

public ICarrierIndex GetIndex(string name)
{
    lock (lockObject) return indexesByTable.GetValueOrDefault(name);
}

public bool TryGetIndex<TIndex>(string name, out TIndex index) where TIndex : class, ICarrierIndex
{
    lock (lockObject)
    {
        if (indexesByTable.TryGetValue(name, out var v) && v is TIndex typed)
        {
            index = typed;
            return true;
        }
        index = null;
        return false;
    }
}

public IReadOnlyCollection<ICarrierIndex> GetAllIndexes()
{
    lock (lockObject) return indexesByTable.Values.ToArray(); // 返回快照，非活集合
}
```

**`DestroyCarrier` 尾部（快照与移除同锁；派发锁外、逐条隔离异常）：**

```csharp
// ── 锁内：移除载体 + 同锁取索引快照（与移除同锁，保证「快照 == 已移除」原子）──
ICarrierIndex[] snapshot;
lock (lockObject)
{
    // ...carrierTraits / carriersByType / traitReverseIndex 清理...
    snapshot = indexesByTable.Values.ToArray();
}

// ── 锁外：派发清理，逐条隔离异常 ──
try
{
    for (int i = 0; i < snapshot.Length; i++)
    {
        try { snapshot[i].OnCarrierRemoved(carrierId); }
        catch (Exception ex)
        {
            Debug.LogError($"[CarrierManager] index '{snapshot[i].Name}' OnCarrierRemoved failed: {ex.Message}");
        }
    }
}
finally
{
    // 即使某个业务索引抛异常，也必须广播销毁事件（否则 Tag 清理级联失效）
    eventCallback?.OnCarrierDestroyed(carrierId);
}
```

> ⚠ 两条初稿遗留缺陷：① 初稿把「取快照」画成独立锁块（与移除不原子），此处统一为同一把锁；② 初稿派发裸循环无异常隔离，一个索引抛异常会连累其余索引与 `CarrierDestroyedEvent`（现有框架的 `TypeEventService.Send` 恰恰是逐监听 try/catch，`FrameworkArchitecture.cs:81-89`），此处补齐。

**便捷糖**（扩展方法，挂在 `ICanGetCarrier` 上——`IModel` / `IService` / `ICommand` / `IController` 天然具备）：

```csharp
public static class CanGetCarrierIndexExtension
{
    public static TIndex RegisterCarrierIndex<TIndex>(this ICanGetCarrier self, TIndex index)
        where TIndex : class, ICarrierIndex
    {
        self.Architecture.GetCarrierManager().RegisterIndex(index);
        return index; // 便于字段内联注册
    }

    public static bool UnregisterCarrierIndex(this ICanGetCarrier self, string name)
        => self.Architecture.GetCarrierManager().UnregisterIndex(name);

    public static bool TryGetCarrierIndex<TIndex>(this ICanGetCarrier self, string name, out TIndex index)
        where TIndex : class, ICarrierIndex
        => self.Architecture.GetCarrierManager().TryGetIndex(name, out index);
}
```

> **取用即取得写权限**：`TryGetCarrierIndex` 返回的是**活对象**，取用方对表有完整读写权。这是当前规格的已知缺口（只读视图 `IReadOnlyCarrierIndex` 被推迟到 P3）。**约定：跨组件取用者应只读**；如需共享写入，说明该表应归属单一所有者、其余方经 Command/事件交互。P0 若时间允许，宜先引入只读接口堵住此口（见 §七 Q6）。

---

## 三、使用形态

### 3.1 表名常量（推荐模式）

框架不内置具体表名（那是业务语义），但**强烈建议集中定义**以避免魔法字符串：

```csharp
public static class CarrierIndexNames
{
    public const string UnitEntity = "unit.entity";   // CarrierId ↔ Entity
    public const string UnitName   = "unit.name";     // CarrierId ↔ string  (名字唯一，name ↔ CarrierId)
    public const string UnitTarget = "unit.target";   // CarrierId ↔ CarrierId (一对一锁定)
    public const string UnitFlags  = "unit.flags";    // (flag) → {CarrierId}
    // 注意：若"多个源锁定同一目标"，用下表的 CarrierGroupIndex<CarrierId>，勿用 UnitTarget（见 §2.2 规则 2 后果③）
    public const string TargetLockers = "target.lockers"; // (目标CarrierId) → {源CarrierId}  ← TKey=CarrierId
    //   ↑ 目标 Carrier 销毁时，该分组整组自动清除（§2.2.1 情形①）
    //   ↑ UnitTarget 与 TargetLockers 中被引用 Carrier 销毁时的清理见 §2.2.1
}
```

### 3.2 Model 用法（主场景）

```csharp
public class UnitIndexModel : AbstractModel
{
    private readonly CarrierIndex<Entity>   _entityOf;
    private readonly CarrierIndex<string>   _nameOf;
    private readonly CarrierIndex<CarrierId> _targetOf;
    private readonly CarrierGroupIndex<string> _byFlag;

    public UnitIndexModel()
    {
        // 三张 CarrierId↔X 表同程序集、同为"映射"，靠表名区分语义；反向映射无需开关
        _entityOf = new CarrierIndex<Entity>(CarrierIndexNames.UnitEntity);
        _nameOf   = new CarrierIndex<string>(CarrierIndexNames.UnitName);
        _targetOf = new CarrierIndex<CarrierId>(CarrierIndexNames.UnitTarget);
        _byFlag   = new CarrierGroupIndex<string>(CarrierIndexNames.UnitFlags);
    }

    protected override void OnInit()
    {
        this.RegisterCarrierIndex(_entityOf);
        this.RegisterCarrierIndex(_nameOf);
        this.RegisterCarrierIndex(_targetOf);
        this.RegisterCarrierIndex(_byFlag);
    }

    // 业务读写直接持有引用
    public Entity GetEntity(CarrierId carrier) => _entityOf.TryGet(carrier, out var e) ? e : Entity.Null;
    public CarrierId GetByName(string name)     => _nameOf.TryGetCarrier(name);
    public void SetTarget(CarrierId src, CarrierId dst) => _targetOf.Bind(src, dst);

    // 无需 Clear —— 销毁 Carrier 时四张表由 CarrierManager 自动清理
    protected override void OnDeinit() { }
}
```

### 3.3 跨组件共享（按表名取用）

按表名取索引，解耦了"谁建的表"与"谁用表"——Service 无需依赖具体 Model 类型：

```csharp
public class SomethingService : AbstractService
{
    protected override void OnInit()
    {
        // 按语义名取他人（或本项目）注册的表，类型不符自动 false
        if (this.TryGetCarrierIndex<CarrierIndex<Entity>>(CarrierIndexNames.UnitEntity, out var map))
            _unitEntity = map;
    }

    private CarrierIndex<Entity> _unitEntity;
}
```

> **注意**：跨组件共享引入了**取用时机**问题——`TryGetIndex` 失败时应视为"该表尚未注册"并优雅降级，而非抛异常。表名常量保证了"名字"这一契约稳定。另：取到的是**活对象**，取用方应只读（见 §2.3 末尾约定）。

**反例（不要这样做）**：`_targetOf` 若为 `"unit.target"`（源→目标）还是 `"summon.master"`（召唤物→召唤者）分别注册，**不能**因两者都是 `CarrierIndex<CarrierId>` 就复用同一张表——**一张表一个语义**，不同语义即使键值类型相同也必须不同表名。这是本设计用表名而非类型作身份的根本原因（见 §关键决策 B）。

### 3.4 与现有代码的关系

| 现有 | 迁移到 | 说明 |
|---|---|---|
| `GASEntityMapModel` 的 4 个字典 | 2 个 `CarrierIndex`（CarrierId↔Entity、CarrierId↔GameObject）+ 1 张轻量 `GameObject→Entity` 查表 | ✅ 已迁移（P1），见下 |
| `CarrierManager.traitReverseIndex` | 内部自注册 `CarrierGroupIndex<Type>("trait.reverse")` | 可选重构（P2），**非强制项** |

> **✅ P1 实际方案（2026-10-10 完成）**：两条**以 Carrier 为主语**的链迁移到 `CarrierIndex`——
> `CarrierId↔Entity`、`CarrierId↔GameObject`（后者以 `CarrierId` 为键）。而
> **`Entity↔GameObject` 无需独立存储**：由 `Entity → CarrierId`（实体索引反查）+ `CarrierId → GameObject` 组合推导，
> 仅需一张 `GameObject → Entity` 查表。故 4 字典 → 2 索引 + 1 查表，且载体销毁时两条主链**自动清理**（无需 Model 手写订阅）。
>
> **`GameObject → Entity` 为何仍手写**：`Entity→CarrierId` 是 O(1)（索引反查），但"由 GO 反查 Entity"需要一张 GO 表；
> 且此表以 `GameObject` 为键——与**假 null** 及"一个 GO 只能绑一个 Entity"的拒绝语义耦合，属 Model 层策略，不塞进通用容器。
>
> **假 null 惰性过期**：`GetGameObject` 保留过期剔除（GO 销毁后重载 null → 清表返回 null），
> 但**不放进容器**——容器保持纯净，过期判定是 Model 的职责（容器 `isValid` 谓词方案未采用，见 §七 Q5）。
>
> **`CarrierId↔Entity` 作键正确性**：`Entity` 是 `IEquatable` 结构体，作键无装箱、哈希正确。

> **建议**：先落地框架 + 单元测试，`GASEntityMapModel` 改造作为**第二期**独立验证。首期不动 GAS，降低回归面。

---

## 四、生命周期与时序

### 4.1 关键链路

```
CarrierManager.DestroyCarrier(carrier)
    ├── lock {
    │     carrierTraits / carriersByType 清理
    │     traitReverseIndex 清理（内部索引，锁内）
    │     取注册索引快照 → snapshot[]        // by TableName
    │   }
    └── 锁外 {
          foreach snapshot: index.OnCarrierRemoved(carrier)   // ① 先清业务索引
          eventCallback?.OnCarrierDestroyed(carrier)          // ② 再广播
        }
```

**与现有 Tag 清理同序**——`CarrierManagerEventBridge.OnCarrierDestroyed` 里本就是"先 `tagService.OnCarrierDestroyed`、再 `SendEvent(CarrierDestroyedEvent)`"。索引清理插入同一位置，语义一致。

### 4.2 四条不变式

1. **锁外调用业务代码**：`OnCarrierRemoved` 是业务实现的，**必须在 `lock` 外执行**。真实理由有二：① **避免锁序嵌套**——`CarrierManager.lock → 业务锁` 若与别处反序即 AB-BA 死锁，现有 `CarrierManager.cs:311-313` 已用"锁外广播"切断这个方向；② **避免持锁执行外部代码**——`lock`（Monitor）对同线程可重入，同线程回摸不会死锁，但会**在派发遍历中重入改集合**、污染状态。⚠初稿把理由写成"回摸即死锁"有误导（同线程不会），此处更正。
2. **先清索引、后广播**：`CarrierDestroyedEvent` 派发时索引已干净。任何订阅者收到事件后反查索引，**不会读到已销毁载体**（延续 `CarrierLifecycleEvents` 的既有承诺）。
3. **注册后受管**（非"无窗口期"）：索引在 `RegisterIndex` 后即受 Provider 派发保护。⚠ 初稿"无窗口期"是**过度承诺**——`RegisterIndex` **之前**就绑定、或绑定到**已销毁载体**的条目，不会被回溯清理。该缺口由 §2.2 的「映射写入三规则」之规则 1（写入前校验载体有效性）封堵。
4. **引用完整性**：载体销毁后，**所有**索引中不再存在以它为成员、key、或 value 的条目——悬垂引用为零（§2.2.1）。这是"先清索引、后广播"在 `CarrierId` 作引用键时的具体兑现：`CarrierDestroyedEvent` 派发时，任何反查都不会得到已销毁的 `CarrierId`。

### 4.3 边界情况

| 场景 | 行为 |
|---|---|
| 同名表重复注册（不同实例） | `LogWarning` + **拒绝覆盖**，保留先者。**设计原则：改表 = 改数据，不是换表**——需要变更时操作表内条目（`Bind`/`Add`/`Remove`），不得注册同名新表替换整表；表名是稳定的业务语义身份，替换会打断所有按名取用者 |
| 同实例重复注册 | no-op |
| 空名 / null 注册 | `LogWarning` + 忽略 |
| `Bind` 到无效 / 已销毁载体 | 按 §2.2 规则 1 拒绝（`LogWarning`），避免孤儿条目 |
| `Bind` 的值已被别的 Carrier 持有 | **覆盖**：驱逐旧主（移除旧 Carrier 正向条目），值归后者。见 §2.2 规则 2 |
| 同一 Carrier 重复 `Bind` | 覆盖其旧值，先清旧值的反向条目 |
| `OnCarrierRemoved` 内 `UnregisterIndex` | 遍历用快照，不受影响 |
| **被引用 Carrier 销毁**：`CarrierGroupIndex<CarrierId>` 的 `TKey` 命中 | 清该分组整体（`groups[carrier]` 删除），语义自然无代价（§2.2.1 情形①） |
| **被引用 Carrier 销毁**：`CarrierIndex<X, CarrierId>` 的 `TValue` 命中 | 经 `_reverse[carrier]` 反查指向它的 source，`Unbind` 驱逐（双射不变量下至多一个）；**source 存活**，仅失去引用（§2.2.1 情形②）。业务若需响应应订阅 `CarrierDestroyedEvent` |
| `OnCarrierRemoved` 内 `DestroyCarrier(Y)` | 触发**递归派发**（快照隔离，不会改当前遍历）；业务**禁止**在回调内销毁"正在派发的载体"以免无界递归。P0 不额外加防护，仅文档化 |
| `Architecture.Shutdown` 后 `RegisterIndex` | 拒绝 + `LogWarning`（另见 §4.4 Init/Shutdown 语义） |

---

### 4.4 初始化 / 关闭语义（须写入 P0）

现有 `CarrierManager.Init / Deinit / Dispose`（`CarrierManager.cs:90-125`）**不碰**新字典，需显式规定：

| 阶段 | `indexesByTable` 行为 | 理由 |
|---|---|---|
| `Init()` | **不清空**（字段在声明处 `new()`） | ⚠ 若 Init 清空，会吞掉先于它执行的 `RegisterIndex`（见下时序风险）。索引是「随 Architecture 存活」的派生数据，Init 无需重置 |
| `Deinit()` / `Dispose()` | **清空 + 置 `disposed = true`** | 释放引用；标志位供关闭后拒绝注册 |
| `Shutdown()` 之后 `RegisterIndex` | **拒绝 + `LogWarning`**（经 `disposed` 标志，字段未置 null 时仍可安全读取） | 关闭后写入会无清理地悬挂 |

> ⚠ **Init 时序风险与 P0 处置**：`IOCContainer.InitAll` 的遍历顺序不保证（`FrameworkArchitecture.cs:31-40`）。若 `CarrierManager.Init` 清空字典、且晚于某 Model 的 `OnInit`（后者已 `RegisterIndex`），注册会被**静默清掉**。**P0 最终采用「Init 不清空」**——字段声明处即 `new()`，`Init` 不再触碰 `indexesByTable`。`disposed` 标志在 `Dispose` 置位、`Init` 复位（供 Architecture 重启复用同一实例）。

---

## 五、权限与依赖

| 依赖方向 | 说明 | 是否允许 |
|---|---|---|
| `NexusFramework` → 新索引类型 | 落在框架层 `DataCarrier/` | ✅ |
| 索引类型 → `Unity.Entities` | 框架层引入 ECS | ❌ 禁止（`CarrierManager` 只持 `ICarrierIndex`，泛型在业务程序集实例化） |
| `ICarrierManager` → 业务索引 | 仅经 `ICarrierIndex` 接口，不知具体类型 | ✅ |

**访问路径**：
- 注册：`ICanGetCarrier` 扩展方法 `RegisterCarrierIndex`（`IModel`/`IService`/`ICommand`/`IController` 均可用）。
- 读写：业务直接持有索引对象引用。
- 跨组件取用：`TryGetCarrierIndex<TIndex>(name, out index)`。

---

## 六、落地分期

| 期 | 内容 | 验证 |
|---|---|---|
| **P0（首期）** | `ICarrierIndex`、`CarrierIndex<TValue>`、`CarrierGroupIndex<TKey>`、`ICarrierManager.RegisterIndex/UnregisterIndex/GetIndex/TryGetIndex/GetAllIndexes`、`ICarrierIndexOwner` 注入与有效性校验、**§2.2.1 引用完整性清理**、`RegisterCarrierIndex` 扩展方法、`Init/Deinit/Dispose/Shutdown` 对字典的语义 | 单元测试：绑定/反查/**值冲突覆盖+驱逐旧主（正反恒镜像）**/无效载体拒绝/自我重绑/重名拒绝/类型不符取回/**所有权守卫防误删**/**逐条异常隔离**/锁外派发/`GetAllIndexes` 快照语义/**被引用 Carrier 销毁：分组 key 整组清除**/**被引用 Carrier 销毁：反向 value 级联驱逐且 source 存活**/**自引用销毁** |
| **P1** | 改造 `GASEntityMapModel`：仅 `CarrierId↔Entity` 迁为索引；`Entity↔GameObject` **保留**（假 null 语义，见 §3.4） | GAS 现网行为回归（创建/绑定/解绑/销毁全链路 + GameObject 销毁后 `GetGameObject` 返回 null） |
| **P2（可选）** | `CarrierManager.traitReverseIndex` 内部换用 `CarrierGroupIndex<Type>`（自注册） | 载体反向索引测试 |
| **P3（待评估）** | 只读视图 `IReadOnlyCarrierIndex`、批量清理 API、`GetAllIndexes` 诊断面板 | — |

---

## 七、待决问题（含审查后新增）

**【已决】**

1. **重名策略** —— 重名一律 **拒绝覆盖**（警告 + 保留先者）。设计原则：**改表 = 改数据，不是换表**；需要变更时操作表内条目，不得注册同名新表替换整表。

**【待你拍板】**

2. **值冲突策略** —— **覆盖（后者覆盖前者）**：`Bind` 时若值已被他人持有，驱逐旧主，值归后者。连带要求：**反向映射强制维护**（否则无法反查旧主驱逐）。注意后果——旧 Carrier 静默失去绑定；多对一关系须改用 `CarrierGroupIndex`（见 §2.2 规则 2）。
3. **表名管理**：`const string` 常量类（推荐）之外，是否需要 `enum` 或 Attribute 声明式注册？倾向不做，保持 string + 常量（框架不维护业务表名清单）。
4. **并发**：容器是否加内部锁？`DestroyCarrier` 可从任意线程调用，派发在 `CarrierManager` 锁外、不持容器锁（审查 B4）。架构上本项目偏单线程（ECS 走 Job），**倾向 P0 不加锁、文档强制单线程**；若实际有多线程写索引需求，P0 就加。
5. **引用类型假 null**：是否为 `TValue` 提供可选有效性谓词（`Func<TValue,bool>`）以支持 `GameObject` 迁移？**P0 倾向不做**（`Entity↔GameObject` 保留在 Model 内），列入 P3 或按需。

**【已识别、P0 可暂不处理】**

6. **只读视图时机**：`IReadOnlyCarrierIndex` 是否 P0 就引入（堵住跨组件取用即写权限的口，审查 B7）？本稿推迟到 P3，靠「取用者只读」约定。
7. **锁内/锁外分流**：内部 `traitReverseIndex` 锁内清、注册索引锁外清。P2 若 `traitReverseIndex` 也自注册，则统一锁外——是否值得？
8. **索引归属生命周期**：索引随 Architecture 存活（不由 Model `Deinit` 自动反注册），是否为短生命周期 Model 提供"自动反注册"钩子？
9. **`GetAllIndexes` 分配**：每次销毁 `Values.ToArray()` 有分配（审查 E2），高频销毁场景可优化为复用缓冲——低优先。
10. **命名**：`CarrierIndex` / `CarrierGroupIndex` 与现有 `TagRuntimeIndex` 对齐度；扩展方法类 `CanGetCarrierIndexExtension`。

---

## 附：命名与文件落位

```
Assets/NexusFramework/DataCarrier/
├── ICarrierIndex.cs            # 索引契约（含 Name）
├── CarrierIndex.cs             # CarrierId ↔ TValue 双射表（表名构造 + 写入三规则）
├── CarrierGroupIndex.cs        # (TKey) → {CarrierId} 分组表（表名构造）
```

接口与扩展方法改动落在：
- `CarrierManager.cs` — `ICarrierManager` 新增名称索引 API + `ICarrierIndexOwner`（可由框架内部持有，无需独立文件）；`DestroyCarrier` 同锁取快照、锁外逐条隔离派发；`Init/Deinit/Dispose` 语义（§4.4）；
- `Framework.cs` — `CanGetCarrierIndexExtension` 扩展方法（与 `CanGetCarrierExtension` 同风格）。

---

## 附：审查记录（2026-10-10）

对 D005 做了一轮对抗性审查（对照 `CarrierManager.cs` / `Framework.cs` / `GASEntityMapModel.cs` / `FrameworkArchitecture.cs` 实际核对）。**已在本文档修订的问题**：

| 编号 | 问题 | 处置 |
|---|---|---|
| A1 | `CarrierIndex` 值冲突时正反字典不一致，`OnCarrierRemoved` 误删他人条目 | §2.2 规则 2「覆盖 + 强制反向」驱逐旧主，正反恒镜像；`OnCarrierRemoved` 保留所有权守卫兜底 |
| A2 | `Bind` 无载体有效性校验 → 孤儿条目永不被清 | §2.2 规则 1 + `ICarrierIndexOwner` |
| B3 | 派发循环无异常隔离 → 一个索引抛异常连累全部 + 事件失效 | §2.3 `try/finally` 逐条隔离 |
| B5 | 快照与移除不原子（两处写法矛盾） | 统一为同锁取快照 |
| B6 | `GameObject` 假 null 语义丢失 | §3.4 迁移限制 + §七 Q5 |
| B7 | 跨组件取用暴露可写引用 | §2.3 约定 + §七 Q6（只读视图时机） |
| B8 | `OnCarrierRemoved` 内 `DestroyCarrier` 重入未定义 | §4.3 文档化 |
| B9 | `Init/Deinit/Dispose/Shutdown` 对字典语义未定义 | §4.4 新增 |
| B10 | 不变式 1「锁内回调即死锁」理由错误 | §4.2 更正为锁序嵌套/持锁执行外部代码 |
| A2/不变式3 | 「注册即受管、无窗口期」过度承诺 | §4.2 改为「注册后受管」 |

**补充审查（同一日，业务驱动新增需求）**：

| 项 | 问题 | 处置 |
|---|---|---|
| R1 | `CarrierGroupIndex<TKey=CarrierId>` 的分组 key 载体被销毁 → 分组残留悬垂 key | §2.2.1 情形① 整组清除 |
| R2 | 对称缺口：`CarrierIndex<TValue=CarrierId>` 的被引用目标载体销毁 → 指向它的条目悬垂 | §2.2.1 情形② 级联驱逐引用者 |
| R3 | 引用完整性作为独立不变式未显式声明 | §4.2 新增不变式 4 |

**审查确认**：值类型（`Entity`/`CarrierId`）作键无装箱、哈希正确（两者实现 `IEquatable`）；§五「框架层零 ECS 依赖」成立（`NexusFramework.asmdef` 无 `Unity.Entities` 引用）。
