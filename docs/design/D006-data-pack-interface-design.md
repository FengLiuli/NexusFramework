---
layer: design
status: draft
topic: data-pack-interface
origin:
  - docs/brainstorms/2026-08-01-data-pack-requirements.md
  - docs/plans/2026-08-01-001-feat-data-pack-interfaces-plan.md
  - docs/progress/2026-11-17-data-pack-plan-review.md
created: 2026-11-17
updated: 2026-11-17
---

# 设计：NexusFramework.Config 接口契约

## 方案概述

本文档是 plan `2026-08-01-001` 的 **Unit 1 交付物设计稿**：只定义接口、值类型与常量，不含实现。目标是在写实现之前，把"表怎么存、包怎么注册、行怎么查、合并怎么诊断"全部固定下来，供评审确认。

接口设计遵循三条硬约束：

1. **框架对表类型零知识**：所有行类型经 `Type` 参数传入，内部按约定反射；
2. **Convention over configuration**：主键 `ID`、废除列 `is_void`、反序列化器 `Deserialize{TypeName}` 作为唯一约定来源；
3. **失败不抛异常**：注册与查询失败经结果类型 + 诊断文本返回。

---

## 一、分层与依赖

```
NexusFramework.Config          → NexusFramework + Luban.Runtime
NexusFramework.Config.Editor   → NexusFramework.Config + UnityEditor
业务数据包程序集（如 Demo.DataPack） → NexusFramework + NexusFramework.Config + Luban.Runtime
物品子系统                     → NexusFramework（可选：NexusFramework.Config，见 D007）
```

`NexusFramework.Config` **不得**引用 GAS、Demo 或任何具体表类型所在程序集。

---

## 二、核心契约

### 2.1 包标识与清单

```csharp
namespace NexusFramework.Config
{
    /// <summary>数据包标识。小写字母/数字/点/下划线，长度 1..64。</summary>
    public readonly struct DataPackId : IEquatable<DataPackId>
    {
        public string Value { get; }
        public static DataPackId FromString(string value);   // 非法取值抛 ArgumentException
        public static bool TryParse(string value, out DataPackId id);
        public override string ToString();
    }

    /// <summary>包清单（Editor 期维护，运行时以 IDataPackEntry 为准）。</summary>
    [Serializable]
    public sealed class DataPackManifest
    {
        public string packId;
        public int priority;
        public string[] dependencies;      // 仅预留，本模块不消费
        public string[] tables;            // 仅预留
    }

    public static class DataPackPriorities
    {
        public const int Base = 0;
        public const int Dlc = 100;
        public const int Mod = 200;
    }

    /// <summary>对 Luban 生成形状的唯一约定来源。</summary>
    public static class DataPackConventions
    {
        public const string PrimaryKeyField = "ID";
        public const string VoidField = "is_void";
        public const string DeserializerPrefix = "Deserialize";

        public static bool IsValidPackId(string value);
        public static bool IsValidTableName(string tableName);  // 小写，允许字母数字下划线
        public static bool IsValidDataName(string dataName);
    }
}
```

**说明**

- 包清单只作 Editor 期元数据；运行时注册只读 `IDataPackEntry`，避免序列化依赖。
- `is_void` 与 `ID` 是唯一约定来源；内省器发现非 `ID` 主键时**拒绝并诊断**（复核 R-8：`exgas.TbgameplayTags` 使用小写 `id`，不纳入新包规范）。

### 2.2 注册

```csharp
    /// <summary>单表声明：表名 + 行类型 + 数据文件名。</summary>
    public readonly struct DataPackTableDeclaration
    {
        public string TableName { get; }   // 如 "tbitem"
        public Type RowType { get; }       // 如 typeof(cfg.item)
        public string DataName { get; }    // 如 "tbitem"（不含 .json）
    }

    /// <summary>包注册入口。由生成器产出，业务不手写。</summary>
    public interface IDataPackEntry
    {
        DataPackId PackId { get; }
        int Priority { get; }
        IReadOnlyList<DataPackTableDeclaration> Tables { get; }
    }

    public enum DataPackTableStatus
    {
        New,                    // 新建表
        Overlayed,              // 合并成功
        SchemaMismatch,         // 列/主键/废除列不一致
        DeserializeFailed,      // 行 JSON 与行类型不兼容
        VoidSemanticsUnsupported, // 已注册表无 is_void，覆盖方含废除行
        AlreadyRegistered,      // 同 PackId 重复注册
        DataMissing             // 数据源未返回该表
    }

    public readonly struct DataPackTableResult
    {
        public string TableName { get; }
        public DataPackTableStatus Status { get; }
        public string Diagnostic { get; }   // 字段级差异文本，成功时为 null
        public bool IsSuccess { get; }
    }

    public readonly struct DataPackRegisterResult
    {
        public DataPackId PackId { get; }
        public bool IsSuccess { get; }      // 全部表成功才为 true
        public IReadOnlyList<DataPackTableResult> Tables { get; }
        public string Diagnostic { get; }   // 包级诊断（重复包等）
    }

    /// <summary>表级原语：诊断、测试与未来远程/热更表源的扩展点。</summary>
    public readonly struct DataPackTableRegistration
    {
        public string TableName { get; }
        public Type RowType { get; }
        public int Priority { get; }
        public DataPackId? PackId { get; }              // 包级注册时填充
        public Luban.SimpleJSON.JSONNode Rows { get; }  // 原始行 JSON
    }

    public interface IDataPackRegistry
    {
        DataPackRegisterResult RegisterDataPack(IDataPackEntry entry, IDataPackDataSource dataSource = null);
        DataPackTableResult RegisterTable(DataPackTableRegistration registration);
        bool IsDataPackRegistered(DataPackId packId);
        IReadOnlyCollection<DataPackId> RegisteredPackIds { get; }
    }
```

**注册语义**

- 包级注册按 `entry.Tables` 顺序逐表注册；**失败不回滚已成功的表**（plan D11）；
- 同 `PackId` 重复注册 → 整包拒绝，`AlreadyRegistered`；
- `dataSource == null` → 从架构 `GetUtility<IDataPackDataSource>()` 取；仍为空则整包失败；
- 优先级只影响合并顺序：低先注册，高后覆盖；同优先级按注册顺序，后者胜。

### 2.3 存储与查询

```csharp
    /// <summary>只读表视图。Count 为有效行数，不含废除行。</summary>
    public interface ITableReader<TRow> where TRow : class
    {
        string TableName { get; }
        int Count { get; }          // 有效行数
        int TotalCount { get; }     // 含废除行
        TRow Get(int key);          // 不存在或已废除 → default
        TRow GetOrDefault(int key);
        bool TryGet(int key, out TRow row);
        bool Contains(int key);     // 存在且未废除
        bool IsVoid(int key);       // 存在且标记废除
        IEnumerable<TRow> All();    // 仅有效行
        IEnumerable<KeyValuePair<int, TRow>> Pairs();
    }

    /// <summary>跨表引用解析视图（合并后）。</summary>
    public interface ITableView
    {
        IReadOnlyCollection<string> TableNames { get; }
        bool ContainsTable(string tableName);
        bool TryGetRow(string tableName, int key, out object row);
        object GetRow(string tableName, int key);   // 缺失 → null
    }

    public sealed class TableModel : AbstractModel, IDataPackRegistry
    {
        public ITableView View { get; }
        public IReadOnlyCollection<string> TableNames { get; }
        public ITableReader<TRow> GetTable<TRow>(string tableName) where TRow : class;
        public bool TryGetTable<TRow>(string tableName, out ITableReader<TRow> table) where TRow : class;
        // IDataPackRegistry 成员见 2.2
    }
```

**查询语义**

- `GetTable<TRow>` 行类型必须等于表存储类型（最低优先级包的行类型）。类型不符 → 返回 `null` + 警告，不尝试转换（plan D8）；
- 表名统一小写，且**不含命名空间前缀**（`tbitem`，而非 `dpack.core.Tbitem`）；数据名与表名相同；
- `Count` / `All` / `Pairs` 过滤废除行；`TotalCount` / `IsVoid` 用于诊断。
- **分配约束**：`All()` / `Pairs()` 若每帧遍历大表，迭代器会产生分配。实现应缓存只读集合视图，或提供 `CopyRowsTo(List<TRow>)`。此项不影响 Carrier 路径，为预防性约束（依据 2026-11-17 性能基线：枚举 API 每次 `ToList()` 在百万级约 8 MB）。

### 2.4 数据源

```csharp
    /// <summary>纯 I/O，不持有缓存；缓存由 TableModel 管理。</summary>
    public interface IDataPackDataSource : IUtility
    {
        /// <summary>返回该表的原始行 JSON；不存在返回 null。</summary>
        Luban.SimpleJSON.JSONNode Load(string dataName);
    }

    /// <summary>默认实现：Resources/DataPacks/{packId}/{dataName}.json</summary>
    public sealed class ResourcesDataPackDataSource : IDataPackDataSource
    {
        public ResourcesDataPackDataSource(DataPackId packId);
        public DataPackId PackId { get; }
        public Luban.SimpleJSON.JSONNode Load(string dataName);
    }
```

### 2.5 合并扩展点（内部实现，接口公开便于测试与替换）

```csharp
    public interface ITableIntrospector
    {
        FieldInfo GetPrimaryKeyField(Type rowType);
        FieldInfo GetVoidField(Type rowType);                    // 可返回 null
        IReadOnlyList<FieldInfo> EnumerateColumns(Type rowType);
        int GetPrimaryKeyValue(object row);
        bool IsVoidRow(object row);
        object DeserializeRow(Type rowType, JSONNode node);       // 失败返回 null
    }

    public readonly struct SchemaCompareResult
    {
        public bool IsCompatible { get; }
        public string Diagnostic { get; }
    }

    public interface ITableSchemaValidator
    {
        SchemaCompareResult Compare(Type registeredRowType, Type incomingRowType);
        SchemaCompareResult CompareColumns(Type registeredRowType, IEnumerable<string> columnNames);
    }

    public interface ITableMerger
    {
        /// <summary>把 rowsJson 合并进 target；返回合并/拒绝结果。</summary>
        DataPackTableResult Merge(
            string tableName,
            Type registeredRowType,
            JSONNode rowsJson,
            DataPackId packId,
            ITableIntrospector introspector);
    }
```

---

## 三、关键设计决策

| # | 决策 | 说明 |
|---|---|---|
| C1 | 查询形状 `GetTable<TRow>(string tableName)` | 表名 + 行类型，框架零表类型知识（沿用 plan D1） |
| C2 | `ITableReader<TRow>` **不变型**（无 `out`） | `TryGet(int, out TRow)` 的 `out` 参数禁止协变 |
| C3 | 注册结果用 `readonly struct` + 状态枚举 | 不抛异常；调用方可按状态分支 |
| C4 | `IDataPackDataSource : IUtility` | 与 GAS `IConfigLoader` 同构，经 `RegisterUtility` 注入 |
| C5 | 数据源目录定为 `Resources/DataPacks/{packId}/{dataName}.json` | 复核 R-6：下游（物品）已产生依赖，提前固定 |
| C6 | 内省/校验/合并接口公开 | 单元测试可直接构造与断言；不会因 internal 而只能做端到端测试 |
| C7 | 主键仅接受 `ID` 大写 | 复核 R-8：小写 `id` 表拒绝并诊断，避免约定分裂 |
| C8 | `TableModel` **只由业务层注册**，不进 `GASArchitecture` | 复核 R-5：保持 GAS asmdef 对 Config 零引用 |
| C9 | 架构 ID 处理沿用复核 R-4 方案 A | Demo 启动前 `ArchitectureFactory.ReserveId(1)`，本次不重构架构生命周期 |
| C10 | Luban 版本钉定后再开工 | 复核 R-2：接口中的 `JSONNode` 类型与 `Deserialize{TypeName}` 约定依赖 Luban 版本 |

---

## 四、生命周期与调用时序

```
启动：
  arch.RegisterModel(new TableModel())
  arch.RegisterUtility(new ResourcesDataPackDataSource(DataPackId.FromString("core")))
  CoreDataPackBootstrapper.RegisterTo(arch.GetModel<TableModel>())
      → TableModel.RegisterDataPack(entry, null)
          → dataSource = 参数 ?? arch.GetUtility<IDataPackDataSource>()
          → foreach declaration: RegisterTable(...)
              → 表不存在：建容器(Type=RowType)
              → 表存在：SchemaValidator → Merger → 覆盖/追加
      → 返回 DataPackRegisterResult（失败不抛异常）

查询：
  var table = arch.GetModel<TableModel>().GetTable<item>("tbitem")
  var row = table.GetOrDefault(1001)
```

---

## 五、待确认问题

| # | 问题 | 默认建议 |
|---|---|---|
| CQ1 | `ITableReader` 是否需要 `GetKey(object row)`，供业务从行反查主键 | 不加；主键约定已是 `ID`，业务可直接读字段 |
| CQ2 | `IDataPackEntry` 的 `Priority` 是否允许运行时覆盖 | 不允许；优先级属包元数据，运行时可读不可改 |
| CQ3 | `ITableView.GetRow` 返回 `object` 是否需要强类型重载 | 不加；ref 解析属内部能力，业务经 `GetTable<TRow>` 取值 |
| CQ4 | `DataPackTableRegistration` 是否公开构造 | 公开；表级原语是诊断与未来远程源的扩展点 |

---

## 六、Unit 1 落地文件清单

```
Assets/NexusFramework.Config/
├── NexusFramework.Config.asmdef
├── Packaging/
│   ├── DataPackId.cs
│   ├── DataPackManifest.cs
│   ├── DataPackPriorities.cs
│   └── DataPackConventions.cs          // 新增：约定常量唯一来源
├── Registration/
│   ├── IDataPackRegistry.cs
│   ├── IDataPackEntry.cs
│   ├── DataPackTableDeclaration.cs     // 新增：声明值类型
│   ├── DataPackTableRegistration.cs
│   ├── DataPackRegisterResult.cs
│   └── DataPackTableResult.cs
├── Storage/
│   ├── TableModel.cs                   // 仅类声明 + 接口挂接
│   ├── ITableReader.cs
│   └── ITableView.cs
├── Merging/
│   ├── ITableIntrospector.cs
│   ├── ITableSchemaValidator.cs
│   └── ITableMerger.cs
└── Sources/
    ├── IDataPackDataSource.cs
    └── ResourcesDataPackDataSource.cs
```

对照 plan 的 Output Structure：`TableContainer` / `TableView` 为实现类，不进 Unit 1；`DataPackConventions` 与 `DataPackTableDeclaration` 为本次接口设计新增的独立文件。
