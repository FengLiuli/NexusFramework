---
layer: code
status: draft
task: T001
source_files:
  - Assets/NexusFramework.GAS/ECS/Attribute/Component/CAttributeData.cs
  - Assets/NexusFramework.GAS/ECS/Attribute/Component/CAttributeIsDirty.cs
  - Assets/NexusFramework.GAS/ECS/AttributeSet/Component/BEAttrSet.cs
  - Assets/NexusFramework.GAS/ECS/AttributeSet/Component/BEAttrSetExtensions.cs
  - Assets/NexusFramework.GAS/ECS/AttributeSet/Component/AttributeHelper.cs
  - Assets/NexusFramework.GAS/ECS/System/SUpdateAttributeCurrentValue.cs
  - Assets/NexusFramework.GAS/ECS/Effect/MMC/*.cs
  - Assets/NexusFramework.GAS/Services/AttributeService.cs
created: 2026-06-12
updated: 2026-06-12
---

# 编码文档：GAS 属性与 MMC

## 概述

属性系统是 GAS 的数值基础。每个 Carrier 拥有若干 `BEAttrSet`（属性集），每个属性集包含多个 `CAttributeData`（单个属性）。`MMC`（Mod Magnitude Calculation）负责动态计算 Modifier 的实际数值。

---

## 属性数据结构

### CAttributeData（单个属性）

```csharp
public struct CAttributeData : IComponentData
{
    public int Code;           // 属性代码（如 1=HP, 2=MP, 3=Attack）
    public float BaseValue;    // 基础值（未经 GE 修改）
    public float CurrentValue; // 当前值（BaseValue + ΣAdd × ΠMultiply）
    public bool IsClampMin;    // 是否限制最小值
    public bool IsClampMax;    // 是否限制最大值
    public float MinValue;     // 最小值
    public float MaxValue;     // 最大值
    public bool Dirty;         // 是否需要重算
}
```

### BEAttrSet（属性集 Buffer）

```csharp
public struct BEAttrSet : IBufferElementData
{
    public int Code;                            // 属性集代码
    public NativeArray<CAttributeData> Attributes;  // 该集合中的所有属性
}
```

> ⚠️ `NativeArray<CAttributeData>` 是手动管理的非托管内存，创建和销毁 Carrier 时必须正确 Dispose。

---

## AttributeService API

```csharp
var attrService = arch.GetService<AttributeService>();

// 获取当前值（BaseValue + 所有活跃 GE Modifier）
float hp = attrService.GetCurrentValue(heroId, attrSetCode: 1, attrCode: 1);

// 获取基础值（未修改的裸值）
float baseAtk = attrService.GetBaseValue(heroId, attrSetCode: 1, attrCode: 3);

// 设置基础值（标记 Dirty，触发 ECS 重算）
attrService.SetBaseValue(heroId, attrSetCode: 1, attrCode: 1, value: 150f);

// 设置当前值（不触发重算，仅初始化时使用）
attrService.SetCurrentValue(heroId, attrSetCode: 1, attrCode: 2, value: 0f);

// 检查属性是否存在
bool hasAttr = attrService.HasAttribute(heroId, attrSetCode: 1, attrCode: 5);
```

---

## 属性重算流程

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
    ├── CurrentValue = BaseValue
    ├── + Σ(Add 型 Modifier.Magnitude)
    ├── × Π(Multiply 型 Modifier.Magnitude)
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

## MMC 修饰器强度计算

### ModMagnitudeCalculationBase

```csharp
public abstract class ModMagnitudeCalculationBase
{
    public string Description;

    public abstract void InitParameters(XParam parameter);

    // 核心方法：magnitude = 配置原始值，返回 = 计算后的最终数值
    public abstract float CalculateMagnitude(MmcContext mmcContext, float magnitude);

    // 生命周期：GE 添加/移除时通知
    public void OnAddMmc(Entity gameplayEffect, EntityManager em, int targetAttrSetCode, int targetAttrCode);
    public void OnRemoveMmc();
    protected virtual void OnAdded(MmcContext context, int targetAttrSetCode, int targetAttrCode) { }
    protected virtual void OnRemoved() { }
}

// 泛型版本
public abstract class ModMagnitudeCalculationBase<T> : ModMagnitudeCalculationBase where T : XParam
{
    public T Parameter { get; private set; }
}
```

### MmcContext

```csharp
public sealed class MmcContext
{
    public Entity Source;       // 来源 ASC Entity
    public Entity Target;       // 目标 ASC Entity
    public Entity EffectEntity; // GE 自身 Entity
}
```

### 内置 MMC 实现

| MMC | 参数类型 | 计算公式 |
|-----|---------|---------|
| `MMCNone` | `XParamNone` | `magnitude`（直通） |
| `MMCScalableFloat` | `MmcParaFloatScale` | `magnitude × K + B` |
| `MMCAttributeBased` | `AttributeBasedMmcParam` | 捕获 Source/Target 属性值 × K + B |

### MMCAttributeBased 详解

```csharp
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

- **Track**：每次属性重算时实时读取源属性值
- **SnapShot**：GE 施加时捕获一次值，后续不跟踪变化

---

## GEOperation

```csharp
public enum GEOperation
{
    Add = 0,       // CurrentValue + magnitude
    Minus = 3,     // CurrentValue - magnitude
    Multiply = 1,  // CurrentValue * magnitude
    Divide = 4,    // CurrentValue / magnitude
    Override = 2   // magnitude（覆盖）
}
```

### 属性重算公式

```
CurrentValue = BaseValue
    + Σ(GE_i.Magnitude)  for each GE_i where Operation = Add/Minus
    × Π(GE_j.Magnitude)  for each GE_j where Operation = Multiply/Divide

最后 Clamp(MinValue, MaxValue)
```

## 关联

- Effect 管线：[GAS Effect 管线](gas-effect-pipeline.md)
- ECS 组件：[GAS ECS 组件清单](gas-ecs-components.md)
- 架构：[GAS 架构与服务层](gas-architecture.md)
- Tag 系统：[GAS Tag 与 Bridge](gas-tag-bridge.md)
