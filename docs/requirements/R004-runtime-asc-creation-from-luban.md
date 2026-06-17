---
layer: requirements
status: draft
task: T003
created: 2026-06-16
updated: 2026-06-16
---

# 需求：运行时从 Luban 配置创建 ASC 对象

## 1. 背景

T003 已实现 `LubanConfigLoader` 生成器，提供 GE/Ability/Cue/MMC/Tag 配置的强类型查询方法。

但**运行时创建 ASC 对象**仍然需要手动拼装 ECS Buffer：

```csharp
// 当前状态——手动步骤过多
var carrierId = arch.CreateGASCarrier("Hero", heroGo);
var entity = model.GetGASEntity(carrierId);

// 手写等级
em.SetComponentData(entity, new CAscBasicData { Level = 1 });

// 手写标签
var tagBuf = em.GetBuffer<BFixedTag>(entity);
tagBuf.Add(new BFixedTag { tag = 1 });

// 手写属性集（NativeArray 分配 + 逐字段赋值）
var attrs = new NativeArray<CAttributeData>(2, Allocator.Persistent);
attrs[0] = new CAttributeData { Code = 3, BaseValue = 100, CurrentValue = 100, ... };
attrs[1] = new CAttributeData { Code = 4, BaseValue = 50, CurrentValue = 50, ... };
buf.Add(new BEAttrSet { Code = 1, Attributes = attrs });

// 手写技能
abilityService.GrantAbility(carrierId, 1001, arch);
```

Luban 的 `cfg.exgas.asc` 和 `cfg.exgas.attributeSet` 表已经包含所有初始化所需数据。需要一条标准管道，将表格数据自动填充到 ECS Entity 上。

## 2. 需求

### 2.1 数据模型

定义三个轻量 struct，放置在框架层（`NexusFramework.GAS.Config` 命名空间）：

**AscConfigData** — ASC 配置：

| 字段 | 类型 | 说明 |
|------|------|------|
| Level | int | ASC 等级 |
| Tags | int[] | 初始固有标签（BFixedTag） |
| AttrSetIds | int[] | 属性集 ID 列表 |
| AbilityIds | int[] | 初始技能 ID 列表 |

**AttrInitDef** — 单条属性的初始值定义：

| 字段 | 类型 | 说明 |
|------|------|------|
| Code | int | 属性代码 |
| InitValue | float | 初始值（BaseValue = CurrentValue） |
| MinValue | float | 最小值 |
| MaxValue | float | 最大值 |
| UseMinValue | bool | 是否启用下限 |
| UseMaxValue | bool | 是否启用上限 |

**AttrSetDef** — 属性集定义（内嵌 AttrInitDef 数组）：

| 字段 | 类型 | 说明 |
|------|------|------|
| AttrSetCode | int | 属性集代码 |
| Attributes | AttrInitDef[] | 该属性集包含的所有属性及其初始值 |

### 2.2 扩展 IConfigLoader

新增两个查询方法：

```csharp
public interface IConfigLoader : IUtility
{
    // ... 现有方法

    /// <summary>获取 ASC 配置（按 ASC ID）</summary>
    AscConfigData? GetAscConfig(int ascId);

    /// <summary>获取属性集定义（按属性集 ID）</summary>
    AttrSetDef? GetAttrSetDef(int attrSetId);
}
```

### 2.3 扩展 ConfigModel

新增缓存字典和配套方法：

```csharp
// 缓存
private readonly Dictionary<int, AscConfigData> _ascConfigs = new();
private readonly Dictionary<int, AttrSetDef> _attrSetDefs = new();

// 注册
public void RegisterAscConfig(int ascId, AscConfigData config)
public void RegisterAttrSetDef(int attrSetId, AttrSetDef def)

// 查询
public AscConfigData? GetAscConfig(int ascId)
public AttrSetDef? GetAttrSetDef(int attrSetId)

// 从 IConfigLoader 加载
public void LoadAscConfig(IConfigLoader loader, int ascId)
public void LoadAttrSetDef(IConfigLoader loader, int attrSetId)

// OnDeinit 清理新增缓存
```

### 2.4 扩展 GASArchitecture

新增 `CreateGASCarrier` 重载：

```csharp
/// <summary>
/// 创建 Carrier + ECS Entity + 从 ConfigModel 读取 ASC 配置自动初始化。
/// 等级、标签、属性集、技能授予一步完成。
/// </summary>
public CarrierId CreateGASCarrier(string typeName, int ascId, GameObject go = null)
```

初始化顺序：

1. `SetupGASEntity` 搭骨架（已有）
2. 读取 `ConfigModel.GetAscConfig(ascId)`
3. 设置 `CAscBasicData.Level`
4. 填充 `BFixedTag`（从 AscConfigData.Tags）
5. 遍历 `AscConfigData.AttrSetIds`：
   - 读 `ConfigModel.GetAttrSetDef(attrSetId)`
   - 创建 `NativeArray<CAttributeData>`，从 `AttrInitDef[]` 逐条映射
   - 添加到 `BEAttrSet` Buffer
6. 遍历 `AscConfigData.AbilityIds`：
   - 调用 `AbilityService.GrantAbility(carrierId, abilityId, this)`

### 2.5 LubanConfigLoader 实现

生成器为 `IConfigLoader` 的两个新方法生成实现：

```csharp
AscConfigData? IConfigLoader.GetAscConfig(int ascId)
{
    if (_tables == null) return null;
    var data = _tables.Tbasc.GetOrDefault(ascId);
    if (data == null) return null;
    return new AscConfigData
    {
        Level = data.Level,
        Tags = data.Tag,
        AttrSetIds = data.AttrSet,
        AbilityIds = data.Ability
    };
}

AttrSetDef? IConfigLoader.GetAttrSetDef(int attrSetId)
{
    if (_tables == null) return null;
    var data = _tables.TbattributeSet.GetOrDefault(attrSetId);
    if (data == null) return null;
    // 从 cfg.AttributeInSet[] 映射到 AttrInitDef[]
    // 含 InitValue / MinValue / MaxValue / UseMinValue / UseMaxValue
}
```

### 2.6 JsonConfigLoader 占位

两个新方法返回 `null`，与现有 `Parse*` 方法行为一致。

## 3. 非功能性需求

- 新增 struct 不引入额外依赖，放置在框架层
- `ConfigModel` 的 ASC 和属性集缓存遵循与 Effect/Ability 一致的 `Register* + Get*` 模式
- `CreateGASCarrier(type, ascId, go)` 不依赖 Luban 类型，任何实现了 `IConfigLoader` 的加载器均可使用
- 当 `ascId` 不存在时优雅降级（返回骨架 Entity + 不抛异常）
- 当 `AttrSetDef` 或属性集不存在时跳过该属性集，不阻塞整体创建

## 4. 验收标准

1. `ConfigModel.RegisterAscConfig(id, config)` 后 `GetAscConfig(id)` 返回相同数据
2. `ConfigModel.RegisterAttrSetDef(id, def)` 后 `GetAttrSetDef(id)` 返回相同数据
3. `arch.CreateGASCarrier("Hero", ascId: 1, heroGo)` 返回的 CarrierId 有效
4. 创建的 Entity 具有正确的 `CAscBasicData.Level`
5. Entity 上的 `BFixedTag` 包含 ASC 配置中指定的标签
6. Entity 上的 `BEAttrSet` 内容与 `attributeSet` 表中的 `AttributeInSet[]` 一致
7. Entity 上的 `BAbility` 包含 ASC 配置中指定的技能
8. 不存在的 ascId 返回正常的骨架 Entity（无标签/属性/技能）
9. 不存在的 attrSetId 跳过该属性集，不影响其他属性集的初始化
10. Luban 加载场景下，生成的 `LubanConfigLoader.cs` 在 Unity Editor 中编译通过
11. JsonConfigLoader 场景下，两个新方法返回 null，不抛异常

## 5. 关联

- 前置需求：[R003 LubanConfigLoader 生成器配套运行时类型映射](R003-luban-config-loader-helper-methods.md)
- 设计：[D003 Luban 配置桥接设计](../design/D003-luban-config-bridge.md)（需更新）
- 任务：[T003 LubanConfigLoader 生成器](../tasks/T003-luban-config-loader-generator.md)
