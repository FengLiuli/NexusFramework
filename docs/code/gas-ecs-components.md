---
layer: code
status: draft
task: T001
source_files:
  - Assets/NexusFramework.GAS/ECS/Ability/Component/**/*.cs
  - Assets/NexusFramework.GAS/ECS/Effect/Component/**/*.cs
  - Assets/NexusFramework.GAS/ECS/Cue/Component/**/*.cs
  - Assets/NexusFramework.GAS/ECS/Attribute/Component/**/*.cs
  - Assets/NexusFramework.GAS/ECS/Tag/Component/**/*.cs
  - Assets/NexusFramework.GAS/ECS/General/**/*.cs
created: 2026-06-12
updated: 2026-06-12
---

# 编码文档：GAS ECS 组件清单

## 概述

GAS 的 ECS 组件分为三大类：**Buffer**（挂载在 ASC Entity 上的列表）、**Static Component**（配置时设定，初始化后不变）、**Dynamic Component**（运行时添加/移除的标记和状态）。

每种 Carrier 通过 `CreateGASCarrier` 创建时，自动获得以下必备 Buffer/Component：
- `BEAttrSet`、`BGameplayEffect`、`BAbility`、`BFixedTag`、`BTemporaryTag`（Buffer）
- `CAscBasicData`（Component）

---

## Buffer 类型（挂载在 ASC Entity 上）

| Buffer | Element Type | 说明 |
|---|---|---|
| `BEAttrSet` | AttributeSet with NativeArray | 属性集列表 |
| `BGameplayEffect` | Entity | 活跃 GE Entity 列表 |
| `BAbility` | Entity | 已授予 Ability Entity 列表 |
| `BFixedTag` | int | 固有标签列表 |
| `BTemporaryTag` | int + Entity source | 临时标签列表（带来源） |

---

## Ability 模块

### Static 组件（配置时设定）

| 文件 | 说明 |
|------|------|
| `BAbility.cs` | Ability 基础数据 Buffer |
| `CAbilityBaseInfo.cs` | Code, Level, Owner |
| `CAbilityAssetTags.cs` | Ability 自身标签 |
| `CAbilityActivationRequiredTags.cs` | 激活所需标签 |
| `CAbilityActivationBlockedTags.cs` | 激活阻止标签 |
| `CAbilityActivationOwnedTags.cs` | 激活后赋予标签 |
| `CAbilityCooldown.cs` | 冷却时间 + 冷却原型 GE |
| `CAbilityCost.cs` | 消耗原型 GE |
| `MCAbilityLogic.cs` | AbilityLogic 多组件配置 |
| `CCancelAbilityWithTags.cs` | 标签取消技能 |
| `CBlockAbilityWithTags.cs` | 标签阻止技能 |

### Dynamic 组件（运行时标记）

| 文件 | 说明 |
|------|------|
| `CAbilityActive.cs` | 已激活标记 |
| `CAbilityInTryActivate.cs` | 请求激活标记 |
| `CAbilityInTryCancel.cs` | 请求取消标记 |
| `CAbilityInTryEnd.cs` | 请求结束标记 |
| `CGrantedByEffect.cs` | 由 GE 授予的标记 |
| `MCGrantedAbilityRuntime.cs` | GE 授予 Ability 的运行时数据 |

### AbilityLogic

| 文件 | 说明 |
|------|------|
| `AbilityLogicBase.cs` | AbilityLogic 抽象基类（可序列化） |
| `AbilityLogicFactory.cs` | AbilityLogic 工厂 |
| `CommonAbilityLogic/ALApplyEffect.cs` | 释放时应用效果的逻辑 |
| `CommonAbilityLogic/ALDebugLog.cs` | 调试日志逻辑 |

### TargetCatcher

| 文件 | 说明 |
|------|------|
| `TargetCatcherBase.cs` | 目标捕获器基类 |
| `CatchSelf.cs` | 捕获自身 |
| `CatchTarget.cs` | 捕获指定目标 |
| `CatchAreaBase.cs` | 区域捕获基类 |
| `CatchAreaBox2D.cs` | 2D 盒形区域捕获 |
| `CatchAreaBox3D.cs` | 3D 盒形区域捕获 |
| `CatchAreaCircle2D.cs` | 2D 圆形区域捕获 |

---

## Effect 模块

### Static 组件（配置时设定）

| 文件 | 说明 |
|------|------|
| `BGameplayEffect.cs` | GameplayEffect 基础数据 Buffer |
| `CEffectBasicInfo.cs` | 效果基本信息（名称） |
| `CEffectAssetTags.cs` | 效果自身标签 |
| `CApplicationCondition.cs` | 应用条件（含自定义检查） |
| `CApplicationRequiredTags.cs` | 应用所需的标签 |
| `COngoingRequiredTags.cs` | 持续需要的标签 |
| `CEffectGrantedTags.cs` | 效果赋予的标签 |
| `CEffectImmunityTags.cs` | 免疫标签 |
| `CRemoveEffectWithTags.cs` | 按标签移除效果 |
| `CDuration.cs` | 持续时长配置 |
| `CPeriod.cs` | 周期触发配置 |
| `CStacking.cs` | 堆叠规则配置 |
| `MCModifiers.cs` | 修饰器多组件集合 |
| `MCGrantedAbility.cs` | 授权技能多组件 |
| `CCueOnApply.cs` | 应用时触发 Cue |
| `CCueOnActivate.cs` | 激活时触发 Cue |
| `CCueOnAdd.cs` | 添加时触发 Cue |
| `CCueOnDeactivate.cs` | 停用时触发 Cue |
| `CCueOnRemove.cs` | 移除时触发 Cue |
| `CCueOnTick.cs` | 每帧触发 Cue |

### Dynamic 组件（运行时标记）

| 文件 | 说明 |
|------|------|
| `CEffectInstance.cs` | 效果实例标记 |
| `CEffectApplied.cs` | 已成功施加标记 |
| `CEffectInUsage.cs` | 使用中标记（含 Source/Target Entity） |
| `CEffectDestroy.cs` | 等待销毁标记 |
| `CInApplicationProgress.cs` | 正在施加过程中 |
| `CCreatedByAbility.cs` | 由哪个 Ability 创建 |

### WIP 标记（管线过渡状态）

| 组件 | 说明 |
|------|------|
| `WipInstantiateEffect` | 等待实例化 |
| `WipApplyEffect` | 等待施加 |
| `WipCheckApplyEffect` | 等待检查施加条件 |
| `WipCheckActiveEffect` | 等待检查激活条件 |
| `WipActivateEffect` | 等待激活 |
| `WipDeactivateEffect` | 等待失活 |
| `WipRemoveEffect` | 等待移除 |

---

## Cue 模块

| 文件 | 说明 |
|------|------|
| `ECCuePlayable.cs` | Cue 可播放标记（Enableable Component） |
| `ECCuePlaying.cs` | Cue 播放中标记（Enableable Component） |
| `ECKillCue.cs` | Cue 销毁标记（Enableable Component） |
| `MCCue.cs` | Cue 管理组件（含 GameplayCueBase 引用） |
| `CPlayRequiredTags.cs` | 播放条件标签 |
| `CPlayImmunitedTags.cs` | 播放免疫标签 |

---

## Attribute 模块

| 文件 | 说明 |
|------|------|
| `CAttributeData.cs` | 单个属性数据（BaseValue + CurrentValue + Clamp） |
| `CAttributeIsDirty.cs` | 属性脏标记 |
| `BEAttrSet.cs` | AttributeSet 动态 Buffer（含 NativeList 属性数组） |
| `BEAttrSetExtensions.cs` | AttributeSet 扩展方法 |
| `AttributeHelper.cs` | 属性查询辅助（RecalculateCurrentValue） |

---

## Tag 模块

| 文件 | 说明 |
|------|------|
| `GameplayTag.cs` | GameplayTag 结构体（层级标签，含 Parents/Children） |
| `BFixedTag.cs` | 固定标签 Buffer |
| `BTemporaryTag.cs` | 临时标签 Buffer |
| `SingletonGameplayTagMap.cs` | 全局标签映射表（NativeHashMap） |
| `GameplayTagChangeEvent.cs` | 标签变更事件 |
| `TagRequirementData.cs` | 标签需求数据（all/any/none） |

---

## Bridge 模块

| 文件 | 说明 |
|------|------|
| `GASEntityRef.cs` | Entity 引用组件（MonoBehaviour，ECS → GameObject） |
| `IGASEntityResolver.cs` | Entity↔GameObject 解析接口 |
| `GASEvents.cs` | ECS 事件定义 |
| `GASInternalBridge.cs` | ECS ↔ 框架桥接数据（Enqueue/Drain 队列） |
| `SEventForwarder.cs` | ECS 事件转发 System |

---

## XParam（泛型参数化配置）

| 文件 | 说明 |
|------|------|
| `XParam.cs` | XParam 基类 |
| `XParamBool/Int/Float/String/Vector2/3.cs` | 基础类型 XParam |
| `XParamArrayInt/Float.cs` | 数组 XParam |
| `XParamCue.cs` | Cue 配置 XParam |
| `XParamApplyEffects.cs` | 应用效果 XParam |
| `XParamEffectIDs.cs` / `XParamCueIDs.cs` | ID 列表 XParam |
| `XParamAnimator.cs` | 动画参数 XParam |
| `XParamMMCScalable.cs` | MMC 可缩放浮点 XParam |

## 关联

- GAS 架构：[GAS 架构与服务层](gas-architecture.md)
- Effect 管线：[GAS Effect 管线](gas-effect-pipeline.md)
- Ability 管线：[GAS Ability 管线](gas-ability-pipeline.md)
- 配置系统：[GAS 配置系统](gas-config.md)
