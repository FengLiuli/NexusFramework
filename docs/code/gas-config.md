---
layer: code
status: draft
task: T001
source_files:
  - Assets/NexusFramework.GAS/Config/IConfigLoader.cs
  - Assets/NexusFramework.GAS/Config/JsonConfigLoader.cs
  - Assets/NexusFramework.GAS/Config/GeneralGasChoiceHelper.cs
  - Assets/NexusFramework.GAS/ECS/Ability/ComponentConfig/AbilityComponentConfig.cs
  - Assets/NexusFramework.GAS/ECS/Effect/ComponentConfig/GameplayEffectComponentConfig.cs
  - Assets/NexusFramework.GAS/XParam.cs
  - Assets/NexusFramework.GAS/XParam*.cs
created: 2026-06-12
updated: 2026-06-12
---

# 编码文档：GAS 配置系统

## 概述

配置系统负责从 JSON 文件（由 Luban 从 Excel 导出）加载所有技能、效果、Cue 的配置数据，并填充到 `ConfigModel` 缓存中。`XParam` 系统提供了类型安全的泛型参数化配置机制，使同一 AbilityLogic 类型可以接收不同的参数。

## 数据流向

```
配置数据 (JSON/Excel via Luban)
    │
    ▼
IConfigLoader.ParseXxx()  ──►  ConfigModel (缓存)
    │
    ▼
Config.LoadToGameplayXxxEntity()  ──►  ECS Entity + Components
```

---

## IConfigLoader 接口

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

### JsonConfigLoader

```csharp
public class JsonConfigLoader : IConfigLoader
{
    // LoadRaw: 从文件系统读取
    // ParseXxx: 从 JSON 反序列化
    // ParseTagHierarchy: 已实现基本版本
}
```

> ⚠️ `JsonConfigLoader` 中 `ParseGameplayEffect`、`ParseAbility`、`ParseGameplayCue`、`ParseMmc` 方法部分待实现，当前依赖 Luban 工具链导出。

---

## ConfigModel 配置缓存

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

---

## GameplayEffectComponentConfig

所有 GE 配置的抽象基类：

```csharp
public abstract class GameplayEffectComponentConfig
{
    protected static EntityManager _entityManager;
    public abstract void LoadToGameplayEffectEntity(Entity ge);
}
```

核心 Config 类与对应 Component 对照：

| Config 类 | Component | 说明 |
|-----------|-----------|------|
| `ConfDuration` | `CDuration` | 持续时间配置 |
| `ConfPeriod` | `CPeriod` | 周期触发配置 |
| `ConfStacking` | `CStacking` | 堆叠配置 |
| `MCConfModifiers` | `MCModifiers` | 属性修改器 |
| `MCConfGrantedAbility` | `MCGrantedAbility` | 授予能力 |
| `ConfEffectBasicInfo` | `CEffectBasicInfo` | 调试名称 |
| `ConfApplicationRequiredTags` | `CApplicationRequiredTags` | 施加条件标签 |
| `ConfEffectImmunityTags` | `CEffectImmunityTags` | 免疫标签 |
| `ConfEffectAssetTags` | `CEffectAssetTags` | GE 自带标签 |
| `ConfEffectGrantedTags` | `CEffectGrantedTags` | 授予标签 |
| `ConfOngoingRequiredTags` | `COngoingRequiredTags` | 持续条件标签 |
| `ConfRemoveEffectWithTags` | `CRemoveEffectWithTags` | 按标签移除 GE |
| `ConfCueOnActivate` 等 | `CCueOnActivate` 等 | 各阶段的 Cue 配置 |

---

## AbilityComponentConfig

```csharp
public abstract class AbilityComponentConfig
{
    protected static EntityManager _entityManager;
    public static void SetEntityManager(EntityManager em);
    public abstract void LoadToGameplayAbilityEntity(Entity ability);
}
```

---

## XParam 泛型配置系统

`XParam` 系统解决同一 AbilityLogic 类型需要不同参数的问题：

```
ALApplyEffect (AbilityLogic)
  └── XParamApplyEffects (XParam 子类)
        ├── effectIDs: int[]
        └── targetStrategy: enum (施法者/目标/区域...)
```

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

### XParam 类型参考

| XParam 类型 | 用途 | 关键字段 |
|------------|------|---------|
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

### BeanField / BeanPolymorphicField

```csharp
// 用于编辑器反射，支持 Luban Excel 列映射
[BeanField(nameof(SetDamage), Comment = "伤害值", Order = 1)]
public float Damage { get; private set; }

[BeanPolymorphicField(nameof(SetTargetCatcher), Comment = "目标捕获类型", Order = 2)]
public string TargetCatcherType { get; private set; }
```

---

## 配置数据结构

```csharp
// Cue 配置
public struct GameplayCueConfig
{
    public string CueType;       // Cue 类型名称
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

---

## 已知限制

1. `JsonConfigLoader` 的 `ParseGameplayEffect`、`ParseAbility`、`ParseGameplayCue`、`ParseMmc` 方法部分待实现
2. `CueHelper.GetCueTypeCode` / `GetCueLogicParamType(int)` 返回 0/null（待实现配置索引查询）
3. `GeneralGasChoiceHelper.AttrSets()` 返回空列表（待实现）
4. 配置导出依赖 Luban 外部工具链（Excel → JSON），框架本身不包含导出逻辑

## 关联

- GAS 架构：[GAS 架构与服务层](gas-architecture.md)
- ECS 组件：[GAS ECS 组件清单](gas-ecs-components.md)
