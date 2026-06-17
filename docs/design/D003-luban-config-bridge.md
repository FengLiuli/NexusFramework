---
layer: design
status: draft
task: T003
created: 2026-06-13
updated: 2026-06-13
parent: D002
---

# 设计：Luban 配置桥接层

## 方案概述

Luban 是一个 Excel/JSON → C# 代码生成工具，生成强类型的 `cfg.*` 配置类型。
但 GAS 框架使用 ECS `ConfigModel` + `XParam` 作为运行时配置格式。

**Luban 桥接层**解决两种格式之间的鸿沟：

```
Luban 导出 (JSON 字节流)
    │
    ▼
LubanConfigLoader (代码生成)
    ├── 读取 cfg.Tables 中的表数据
    ├── 转换为 Conf* 组件配置数组
    └── 写入 ConfigModel
```

同时，桥接层需要运行时类型映射以桥接**字符串类型名（来自 Luban 配置）**和**运行时 C# Type 对象（用于反射创建实例）**。

## 架构设计

### 整体流程

```
Luban Excel 配置
    │ (Luban 工具链)
    ▼
cfg.* 强类型类 (工程级代码，生成在 Unity 项目中)
    │
    ├── cfg.Tables           → 表访问入口
    ├── cfg.TbXxx            → 具体表类型（如 cfg.TbGameplayEffect）
    ├── cfg.AbilityTaskBase  → AbilityTask 配置行
    │
    ▼
LubanConfigLoaderGenerator ([编辑器工具])
    扫描 cfg 命名空间中的表类型
    为每个表生成对应的桥接代码:
    └── LubanConfigLoader.cs (生成文件)
        │
        ├── LoadRaw(string)    → 重定向到 cfg.Tables 访问
        ├── ParseGameplayEffect → cfg → Conf*[] 转换
        ├── ParseAbility        → cfg → AbilityComponentConfig[]
        ├── ParseGameplayCue    → cfg → GameplayCueConfig[]
        ├── ParseMmc            → cfg → MMCConfig[]
        └── ParseTagHierarchy   → cfg → TagHierarchyData
        │
        ▼
    ConfigModel (运行时缓存)
        │
        ▼
    ECS Entity + Components
```

### LubanConfigLoader 生成策略

**模板模式**：生成器遍历所有 cfg.* 类型，对每种 GAS 相关类型生成对应的 Parse 方法。

关键设计决策：

| 决策 | 选择 | 理由 |
|------|------|------|
| 生成时机 | 编辑器菜单手动触发 | 配置表变更频率低，无需自动增量生成 |
| 输出位置 | `Assets/` 下 C# 文件 | 编译为运行时代码，直接引用 cfg.* 类型 |
| 类型查找 | `cfg.Tables` 属性反射 | 无需硬编码表名，自动适配项目级配置 |
| 字段映射 | 按字段名规则匹配 `Conf*` → ECS 组件 | 见 D002 配置系统设计 |
| 空值处理 | `LoadRaw` 返回 `null`（不抛异常） | 与 `IConfigLoader.LoadRaw` 约定的"无数据则 null"一致 |

### 运行时类型映射系统

Luban 配置中使用**字符串类型名**（如 `MMCNone`、`ALApplyEffect`）而非 C# `Type` 引用。
运行时需要将字符串解析为实际 Type 以创建实例和传递泛型参数。

#### 映射数据流

```
Luban 配置 (MMCType="MMCNone")
    │
    ▼
LubanConfigLoader.ParseMmc()
    ├── GetMmcType("MMCNone")     → typeof(MMCNone)
    ├── GetMmcParamType("MMCNone") → typeof(XParamNone)
    │
    ▼
MMCConfig { MmcType = "MMCNone", Param = new XParamNone() }
```

#### 双轨注册策略

| 注册路径 | 触发时机 | 实现方式 |
|---------|---------|---------|
| **自动扫描** | Service.OnInit() | `ScanAndRegisterAll()` 反射扫描程序集 + `InferParamType()` 推断泛型参数 |
| **手动注册** | 应用启动前 | 显式调用 `Register*` 方法，可用于外部扩展类型 |

自动扫描入口：

```
┌─────────────────────────┐
│ CueService.OnInit()     │
│  ScanAndRegisterAll()   │
│  ├── GameplayCueBase<T> │
│  │   → InferParamType() │
│  │   → CueHelper.RegisterCue(name, type, paramType)
│  │
│  └── ModMagnificationCalculationBase<T>
│      → InferParamType()
│      → GasMmcHelper.RegisterMmc(name, type, paramType)
└─────────────────────────┘

┌─────────────────────────────┐
│ AbilityService.OnInit()     │
│  ScanAndRegisterAll()       │
│  └── AbilityLogicBase<T>    │
│      → InferParamType()     │
│      → AbilityLogicFactory.RegisterAbilityLogicParam()
└─────────────────────────────┘
```

#### InferParamType 反射算法

```
输入: subType (如 MMCScalableFloat), genericBaseDef (如 ModMagnificationCalculationBase<>)
算法:
  循环遍历 subType.BaseType 链
  对每个 baseType:
    如果 baseType 是泛型类型 且 baseType.GetGenericTypeDefinition() == genericBaseDef:
      返回 baseType.GetGenericArguments()[0]

输出: typeof(MmcParaFloatScale) 或 null（当不存在泛型基类时）
```

此算法与 `CueService` 中已有的 `InferParamType` 实现一致。

### 类型映射容器设计

每个辅助类/工厂持有**两个字典**：

| 字典 | 键 | 值 | 用途 |
|------|-----|-----|------|
| `_mmcTypes` | MMC 类型名 (string) | `Type` | 实例化 MMC 逻辑 |
| `_mmcParamTypes` | MMC 类型名 (string) | `Type` | 反序列化 XParam 参数 |
| `_logicParamTypes` | AbilityLogic 类型名 (string) | `Type` | 反序列化 XParam 参数 |
| `_taskParamTypes` | AbilityTask 类型名 (string) | `Type` | 保留（始终返回 null） |

遵循 `CueHelper` 已有的 `Register* + Get*` 显式注册模式。

### AbilityTask 类型映射说明

当前 GAS 框架中不存在 `AbilityTaskBase<T>` 泛型基类。
Luban 生成的 `cfg.AbilityTaskBase` 子类是平铺的 flat 类，不携带 XParam 泛型参数。
因此 `GetAbilityTaskParamType()` 始终返回 `null`，生成器代码后备为 `XParamNone`。

## 运行时 ASC 创建

### 问题

当前 `CreateGASCarrier(string typeName, GameObject go)` 仅创建 ECS Entity 骨架（SetupGASEntity），
等级/标签/属性集/技能需要调用方手动填充。Luban 的 `cfg.exgas.asc` 和 `cfg.exgas.attributeSet` 表已包含
所有初始化数据，但缺少标准管道将其自动填充到 ECS Entity。

### 数据流

```
Luban Tables（全量内存加载）
    │
    ├── Tbasc.Get(ascId)            → exgas.asc
    │                                  { Level, Tag[], AttrSet[], Ability[] }
    │
    ├── TbattributeSet.Get(setId)   → exgas.attributeSet
    │                                  { AttributeInSet[] }
    │                                     └─ { ID, InitValue, MinValue, MaxValue, UseMinValue, UseMaxValue }
    │
    ▼
IConfigLoader.GetAscConfig(ascId)    ──→  AscConfigData
IConfigLoader.GetAttrSetDef(setId)   ──→  AttrSetDef (含 AttrInitDef[])
    │
    ▼
ConfigModel (缓存：Register* → Get*)
    │
    ▼
GASArchitecture.CreateGASCarrier(type, ascId, go)
    ├── SetupGASEntity（骨架）
    ├── CAscBasicData.Level          ← AscConfigData.Level
    ├── BFixedTag                    ← AscConfigData.Tags
    ├── BEAttrSet                    ← AttrSetDef[n].AttrInitDef[] → NativeArray<CAttributeData>
    └── AbilityService.GrantAbility  ← AscConfigData.AbilityIds
```

### 数据模型

定义在框架层 `NexusFramework.GAS.Config` 命名空间：

```
AscConfigData
  ├── Level: int
  ├── Tags: int[]
  ├── AttrSetIds: int[]
  └── AbilityIds: int[]

AttrSetDef
  ├── AttrSetCode: int
  └── Attributes: AttrInitDef[]

AttrInitDef
  ├── Code: int
  ├── InitValue: float
  ├── MinValue: float
  ├── MaxValue: float
  ├── UseMinValue: bool
  └── UseMaxValue: bool
```

### 属性集初始化映射

Luban 的 `cfg.AttributeInSet` 到 ECS `CAttributeData` 的直接映射：

| AttributeInSet | → | CAttributeData |
|----------------|---|---------------|
| ID | → | Code |
| InitValue | → | BaseValue = CurrentValue |
| MinValue | → | MinValue（仅当 UseMinValue=true） |
| MaxValue | → | MaxValue（仅当 UseMaxValue=true） |
| UseMinValue | → | IsClampMin |
| UseMaxValue | → | IsClampMax |

注意：`exgas.attribute` 表（ID/Name/Desc）仅用于元数据，运行时初始化不需要。

### 创建重载

`GASArchitecture` 新增方法：

```csharp
public CarrierId CreateGASCarrier(string typeName, int ascId, GameObject go = null)
```

行为：
1. 调 `CreateGASCarrier(typeName, go)` 搭骨架（复用已有逻辑）
2. 从 `ConfigModel.GetAscConfig(ascId)` 读取 ASC 配置
3. 设 `CAscBasicData.Level`
4. 填充 `BFixedTag` Buffer
5. 遍历 `AttrSetIds`，对每个 `attrSetId` 调 `ConfigModel.GetAttrSetDef`，创建 `NativeArray<CAttributeData>` 并添加到 `BEAttrSet`
6. 遍历 `AbilityIds`，调 `AbilityService.GrantAbility`

当 `ascId` 不存在时优雅降级（返回骨架 Entity），当 `attrSetId` 不存在时跳过该属性集。

### CreateGASCarrier 方案对比

| 方案 | 描述 | 选择理由 |
|------|------|---------|
| **重载（选中）** | `CreateGASCarrier(type, ascId, go)` | 调用方一行完成，与现有 API 命名一致 |
| 扩展方法 | `arch.CreateGASCarrierFromLuban(...)` | 无法调用 `protected` 成员 |
| 虚方法钩子 | `OnCarrierCreated` | 抽象层次不够，不如直接走 IConfigLoader 标准管道 |

## 无法确定的 LoadRaw 行为

`IConfigLoader.LoadRaw(string)` 的语义在 Luban 场景下不明确：

| 备选行为 | 优点 | 缺点 |
|---------|------|------|
| 返回 null | 简单，调用方已处理 null | 不符合"加载"的直觉 |
| `throw NotSupportedException` | 明确标记不支持 | 破坏 `IConfigLoader` 约定，需要调用方额外 try-catch |
| 返回空字符串 | 不抛异常 | 无实际意义，调用方仍需检查空字符串 |

**当前选择**：返回 `null`，因为 `ConfigModel.Load*` 的入口已处理 `null` 返回。
（见 R003 第 3 节和 gas-config.md 已知限制）

## 各子系统变更

| 子系统 | 文件 | 变更内容 |
|--------|------|---------|
| MMC 辅助 | `GasMmcHelper.cs` | +3 方法：`RegisterMmc`、`GetMmcType`、`GetMmcParamType` |
| Ability 工厂 | `AbilityLogicFactory.cs` | +4 方法：`RegisterAbilityLogicParam`、`GetAbilityLogicParamType`、`RegisterAbilityTaskParam`、`GetAbilityTaskParamType` |
| Cue 服务 | `CueService.cs` | `ScanAndRegisterAll` 增加 MMC param 类型推断和注册 |
| Ability 服务 | `AbilityService.cs` | `ScanAndRegisterAll` 增加 param 类型推断和注册；新增 `InferParamType` 私有方法 |
| 代码生成器 | `LubanConfigLoaderGenerator.cs` | 引用上述方法生成桥接代码；新增 `GetAscConfig`/`GetAttrSetDef` 接口实现 |
| 编辑器配置 | `GASSettingAsset.cs` | 添加输出路径配置 |
| **IConfigLoader** | `IConfigLoader.cs` | **+2 方法：`GetAscConfig`、`GetAttrSetDef`** |
| **数据模型** | （新建 `AscConfigData.cs` 等） | **新增 `AscConfigData`、`AttrSetDef`、`AttrInitDef` 三个 struct** |
| **ConfigModel** | `ConfigModel.cs` | **新增 ASC/属性集缓存的 Register/Get/Load 方法** |
| **GASArchitecture** | `GASArchitecture.cs` | **新增 `CreateGASCarrier(type, ascId, go)` 重载** |

## 风险与权衡

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| `InferParamType` 反射在大型程序集中缓慢 | 启动延迟增加 | 只在 `OnInit()` 中执行一次，缓存结果 |
| Luban 工具链版本升级可能导致 cfg.* 生成代码变化 | 生成器输出不兼容 | 捕获生成失败，提示重新生成 |
| XParam 类名与 Luban 配置字符串不匹配 | 加载运行时返回 null，功能异常 | 验收标准要求每个类型通过 Get* 验证 |
| AbilityTaskBase<T> 暂不存在 | GetAbilityTaskParamType 始终返回 null | 生成器后备为 XParamNone，后续框架版本可扩展 |
| **ASC 配置中引用的属性集 ID 在 TbattributeSet 中不存在** | **该属性集初始化被跳过** | **CreateGASCarrier 遍历时跳过不存在的 AttrSetId，不阻塞整体流程** |
| **NativeArray<CAttributeData> 未及时 Dispose** | **内存泄漏** | **在 DestroyGASCarrier 中已有清理逻辑（遍历 BEAttrSet 调用 attrs.Dispose）** |

## 关联

- 需求文档：[R003 LubanConfigLoader 配套类型映射](../requirements/R003-luban-config-loader-helper-methods.md)
- 需求文档：[R004 运行时从 Luban 配置创建 ASC 对象](../requirements/R004-runtime-asc-creation-from-luban.md)
- 父设计：[D002 GAS 设计文档](D002-gas-design.md)（配置系统 -> XParam 泛型配置）
- 编码实现：[GAS 配置系统](../code/gas-config.md)（运行时类型映射小节 + ASC 运行时创建小节）
- 任务：[T003 LubanConfigLoader 生成器](../tasks/T003-luban-config-loader-generator.md)
