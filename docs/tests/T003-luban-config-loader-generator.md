---
layer: test
status: draft
task: T003
source_files:
  - Assets/NexusFramework.GAS/Editor/CodeGen/LubanConfigLoaderGenerator.cs
  - Assets/NexusFramework.GAS/Editor/CodeGen/IndentedWriter.cs
  - Assets/DataGenerated/Luban/LubanConfigLoader.cs
  - Assets/Tests/Editor/LubanConfigLoaderGeneratorTests.cs
created: 2026-06-13
updated: 2026-06-13
---

# 测试文档：LubanConfigLoader 生成器

## 概述

LubanConfigLoader 生成器是一个 Editor 代码生成器，扫描 `cfg.*` 命名空间中的 Luban 表格类型，
生成 `LubanConfigLoader.cs` 桥接代码，将表格数据转换为 GAS 运行时配置。

测试分为两层：
1. **Editor 测试** — 验证生成器本身的逻辑正确性（编译、代码生成、字段映射）
2. **集成回归测试** — 验证生成后的 `LubanConfigLoader.cs` 编译通过，且现有 GAS 功能不受影响

## 测试环境

- **模式**：EditMode 测试（生成器仅在 Editor 中运行）
- **程序集**：`Tests.asmdef`（依赖 `NexusFramework.GAS.Editor` 和 `NexusFramework.GAS`）
- **前置条件**：Luban 表格 JSON 数据已导出到 `TableOutputPath`

## 测试计划

### 测试范围

| 模块 | 覆盖情况 | 说明 |
|------|---------|------|
| 生成器编译 | ✅ 待实现 | 生成器源代码可编译无错误 |
| 菜单项触发 | ✅ 待实现 | `NF.GAS/Generate/LubanConfigLoader` 菜单项可执行 |
| 输出文件生成 | ✅ 待实现 | 执行后 `LubanConfigLoader.cs` 被创建/更新 |
| 输出文件编译 | ✅ 已验证 | Unity 控制台 0 errors / 0 warnings |
| 字段映射正确性 | ✅ 待实现 | GrantedAbility 枚举强转、ConfStacking 字段名 |
| 命名空间消歧义 | ✅ 待实现 | `typeof(ECS.MMCNone)` 等 |
| Tag 层级解析 | ✅ 待实现 | `t.Id` + `Array.Empty<int>()` Children |
| TimelineAbility 暂不生成 | ✅ 待验证 | `WriteTimelineAbilityMethod` 调用已注释 |
| 回归验证 | ⏳ 待执行 | 现有 79 个 T001 测试用例不受影响 |

### 不覆盖的范围

- **PlayMode 测试** — 不涉及场景加载和运行时渲染管线
- **Luban 导出过程** — 假设 Excel → JSON 导出由外部工具链完成
- **性能测试** — 生成器运行时间不是一个关注点（Editor 工具）

### 测试用例

#### 生成器基础测试组 (`LubanConfigLoaderGeneratorTests`)

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| GEN-01 | 生成器菜单项存在 | 检查菜单路径 | `NF.GAS/Generate/LubanConfigLoader` 有效 | 单元 |
| GEN-02 | 生成器执行不抛异常 | 调用 `Generator.Execute()` | 无异常 | 集成 |
| GEN-03 | 生成后输出文件存在 | 执行后检查路径 | `LubanConfigLoader.cs` 存在且非空 | 集成 |

#### 输出代码编译测试组

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| CMP-01 | 生成的代码无编译错误 | 编译全部脚本 | AssetDatabase 无错误 | 集成 |
| CMP-02 | `LubanConfigLoader` 类可实例化 | `new LubanConfigLoader()` | 实例非 null | 单元 |

#### 字段映射正确性测试组

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| MAP-01 | `GetEffectConfig` 返回非 null | configId=1 | `GameplayEffectComponentConfig[]` 非 null | 集成 |
| MAP-02 | GrantedAbility 枚举正确强转 | 含 GrantedAbility 的 GE | `ActivationPolicy` 等字段为有效枚举值 | 集成 |
| MAP-03 | ConfStacking 字段映射 | 含 Stacking 的 GE | `EffectDurationRefreshPolicy` 等字段正确赋值 | 集成 |
| MAP-04 | MMC 类型消歧义 | `typeof(MMCNone)` | 编译时使用 `ECS.MMCNone` | 编译期 |
| MAP-05 | Tag 层级 | 含子标签的数据 | `Code = t.Id`, `Children = Array.Empty<int>()` | 集成 |
| MAP-06 | GameplayCue 配置 | `GetCueConfig(id)` | 返回有效的 `GameplayCueConfig` | 集成 |
| MAP-07 | 不存在的 ID 返回 null | configId=99999 | `null` 且不抛异常 | 容错 |

#### 回归验证测试组

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| REG-01 | Smoke 测试全部通过 | 执行 T001 Smoke 组 | 10 个用例全部通过 | 回归 |
| REG-02 | Ability 管线测试全部通过 | 执行 T001 Ability 组 | 9 个用例全部通过 | 回归 |
| REG-03 | Effect 管线测试全部通过 | 执行 T001 Effect 组 | 4 个用例全部通过 | 回归 |
| REG-04 | 堆叠测试全部通过 | 执行 T001 Stacking 组 | 4 个用例全部通过 | 回归 |
| REG-05 | 标签测试全部通过 | 执行 T001 Tag 组 | 5 个用例全部通过 | 回归 |
| REG-06 | Cue 管线测试全部通过 | 执行 T001 Cue 组 | 2 个用例全部通过 | 回归 |
| REG-07 | Service 测试全部通过 | 执行 T001 Service 组 | 15 个用例全部通过 | 回归 |
| REG-08 | 绑定测试全部通过 | 执行 T001 Binding 组 | 24 个用例全部通过 | 回归 |
| REG-09 | 内存安全测试全部通过 | 执行 T001 Memory 组 | 6 个用例全部通过 | 回归 |

### 测试代码

```csharp
// Assets/Tests/Editor/LubanConfigLoaderGeneratorTests.cs

using System.IO;
using System.Reflection;
using NexusFramework.GAS.Config;
using NexusFramework.GAS.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NexusFramework.GAS.Tests.Editor
{
    [TestFixture]
    public class LubanConfigLoaderGeneratorTests
    {
        private const string GeneratedFilePath = "Assets/DataGenerated/Luban/LubanConfigLoader.cs";

        /// <summary>
        /// 验证菜单项存在且可调用（通过反射触发菜单方法）
        /// </summary>
        [Test]
        public void Generator_MenuItem_Exists()
        {
            // 通过反射检查方法上的 MenuItem 属性
            var type = typeof(LubanConfigLoaderGenerator);
            var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            bool hasMenuItem = false;
            foreach (var m in methods)
            {
                var attr = m.GetCustomAttribute<MenuItem>();
                if (attr != null && attr.menuItem == "NF.GAS/Generate/LubanConfigLoader")
                {
                    hasMenuItem = true;
                    break;
                }
            }
            Assert.That(hasMenuItem, Is.True, "菜单项 NF.GAS/Generate/LubanConfigLoader 应存在");
        }

        /// <summary>
        /// 执行生成器不抛异常
        /// </summary>
        [Test]
        public void Generator_Execute_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LubanConfigLoaderGenerator.Generate());
        }

        /// <summary>
        /// 执行后输出文件存在且非空
        /// </summary>
        [Test]
        public void Generator_OutputFile_ExistsAndNotEmpty()
        {
            LubanConfigLoaderGenerator.Generate();
            var fullPath = Path.GetFullPath(GeneratedFilePath);
            Assert.That(File.Exists(fullPath), Is.True, "生成文件应存在");
            var content = File.ReadAllText(fullPath);
            Assert.That(content, Is.Not.Empty, "生成文件不应为空");
        }

        /// <summary>
        /// 生成的 LubanConfigLoader 可实例化
        /// </summary>
        [Test]
        public void Generated_Loader_CanInstantiate()
        {
            var loader = new LubanConfigLoader();
            Assert.That(loader, Is.Not.Null);
            Assert.That(loader.Initialized, Is.False, "新实例 Initialized 应为 false");
        }

        /// <summary>
        /// 不存在的 ID 返回 null 不抛异常
        /// </summary>
        [Test]
        public void GetEffectConfig_UnknownId_ReturnsNull()
        {
            var result = LubanConfigLoader.GetEffectConfig(99999);
            Assert.That(result, Is.Null);
        }

        /// <summary>
        /// 不存在的 Ability ID 返回 null 不抛异常
        /// </summary>
        [Test]
        public void GetAbilityConfig_UnknownId_ReturnsNull()
        {
            var result = LubanConfigLoader.GetAbilityConfig(99999);
            Assert.That(result, Is.Null);
        }

        /// <summary>
        /// 不存在的 Cue ID 返回 default 不抛异常
        /// </summary>
        [Test]
        public void GetCueConfig_UnknownId_ReturnsDefault()
        {
            var result = LubanConfigLoader.GetCueConfig(99999);
            Assert.That(result, Is.Null);
        }

        /// <summary>
        /// 生成的代码中 LoadTablesForEditor 不依赖 GASSettingAsset
        /// </summary>
        [Test]
        public void LoadTablesForEditor_Signature_NoAssetDependency()
        {
            var method = typeof(LubanConfigLoader).GetMethod(
                "LoadTablesForEditor",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(method, Is.Not.Null,
                "LoadTablesForEditor(string jsonDir) 应存在且只接受一个 string 参数");
        }

        /// <summary>
        /// TagHierarchy 数据使用 t.Id 而非 t.ID
        /// </summary>
        [Test]
        public void GetTagHierarchyData_UsesIdProperty()
        {
            // 这个方法更偏向编译期验证——只要编译通过就正确
            var result = LubanConfigLoader.GetTagHierarchyData();
            Assert.That(result.Tags, Is.Not.Null);
            // Tags 数组可能为空（无实际表格数据），但不抛异常
        }
    }
}
```

## 测试报告

### 初次创建日期：2026-06-13
### 最近更新：2026-06-14（添加 10 个 Luban JSON 测试数据文件）

### 结果摘要

| 组 | 用例数 | 通过 | 失败 | 跳过 |
|---|--------|------|------|------|
| 生成器基础 | 3 | ⏳（待执行） | ⏳ | ⏳ |
| 输出代码编译 | 2 | ✅ 已通过 | - | - |
| 字段映射正确性 | 7 | ⏳（数据就绪，待执行） | ⏳ | ⏳ |
| ASC 配置加载 | 6 | 🔄 待实现 | 🔄 | 🔄 |
| ASC 运行时创建 | 7 | 🔄 待实现 | 🔄 | 🔄 |
| 回归验证 | 9 | ⏳（待执行） | ⏳ | ⏳ |
| **总计** | **34** | ⏳ | ⏳ | ⏳ |

## 测试数据

### Luban JSON 测试数据文件

为验证 GAS 模块功能完整性而创建的 10 个 Luban JSON 数据文件（`LubanConfigLoader.LoadTablesForEditor()` 可完整加载）：

| # | 文件名 | 条目数 | 覆盖特性 |
|---|--------|--------|---------|
| 1 | `exgas_tbgameplaytags.json` | 10 | 标签层级（Effect.Buff/Debuff/Fire, Ability.Fire, State.Stun, Damage.Physical/Magic, Gameplay.Immune, DOT） |
| 2 | `exgas_tbattribute.json` | 5 | 基础属性定义（Health, Mana, Attack, Defense, Speed） |
| 3 | `exgas_tbattributeset.json` | 2 | WarriorAttrSet(Attack+Defense), MageAttrSet(Health+Mana+Attack) |
| 4 | `exgas_tbmmc.json` | 3 | MMCNone, MMCAttributeBased(K=1,B=0 on Attack), MMCScalableFloat(K=1.5,B=10) |
| 5 | `exgas_tbgameplayeffect.json` | 8 | 瞬时/持续/周期效果、Stacking、GrantedAbility、TagRequirement、Cue 引用、Modifier |
| 6 | `exgas_tbability.json` | 3 | ALAApplyEffect(x2)、ALDebugLog、Cost/CdEffect/Cd、ActivationRequiredTags |
| 7 | `exgas_tbgameplaycue.json` | 3 | CuePlaySound、CueLog、CueLogging（含 RequiredTag/ImmunityTag） |
| 8 | `exgas_tbasc.json` | 2 | PlayerASC(多属性集+多技能)、EnemyASC(单属性集+单技能) |
| 9 | `exgas_tbtimelineability.json` | 1 | 2 Tracks x 6 Clips（TaskApplyEffects+CatchSelf, TaskPlayCue+XParamCue, TaskDebug, TaskDoCooldown, TaskDoCost, TaskDoNothing） |
| 10 | `tbunit.json` | 2 | Player+Enemy 关联 ASC（无 exgas_ 前缀） |

数据文件位置：`Assets/DataGenerated/Luban/Json/GAS/`

验证结果：10 张表全部加载成功，全部多态类型（AbilityLogicBase、GameplayCueBase、ModMagnificationCalculationBase、AbilityTaskBase、TargetCatcherBase）及其嵌套的 XParam 类型反序列化正确。

### MAP 测试用例对应关系

| 用例 | 涉及的测试数据 |
|------|--------------|
| MAP-01 `GetEffectConfig` 非 null | `exgas_tbgameplayeffect.json` ID=1..8 |
| MAP-02 GrantedAbility 枚举 | `exgas_tbgameplayeffect.json` ID=7 (GrantFireBall) |
| MAP-03 ConfStacking 字段 | `exgas_tbgameplayeffect.json` ID=5 (Shield, StackingType=1, LimitCount=3) |
| MAP-04 MMC 类型消歧义 | `exgas_tbmmc.json` ID=1..3 (MMCNone, MMCAttributeBased, MMCScalableFloat) |
| MAP-05 Tag 层级 | `exgas_tbgameplaytags.json` ID=1..10 |
| MAP-06 GameplayCue 配置 | `exgas_tbgameplaycue.json` ID=1..3 |
| MAP-07 不存在的 ID 返回 null | 参数 99999 |
| MAP-08 `GetAscConfig` 配置字段 | `exgas_tbasc.json` ID=1 (PlayerASC) |
| MAP-09 `GetAttrSetDef` 属性初始值 | `exgas_tbattributeset.json` ID=1 (Warrior: Attack=100, Defense=50) |
| MAP-10 `CreateGASCarrier(ascId)` 属性集完整性 | ASC ID=1 引用 attrSetId=1,2 |
| MAP-11 不存在的 ascId 优雅降级 | ASC ID=99999 |
	
#### ASC 配置加载测试组 (`ASCConfigTests`)

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| ASC-01 | `GetAscConfig` 返回非 null | ascId=1 (PlayerASC) | `AscConfigData` 非 null，Level=1 | 集成 |
| ASC-02 | `GetAscConfig` 配置字段正确 | ascId=1 | Tags=[1], AttrSetIds=[1,2], AbilityIds=[1001,1002] | 集成 |
| ASC-03 | `GetAscConfig` 不存在的 ascId 返回 null | ascId=99999 | null | 容错 |
| ASC-04 | `GetAttrSetDef` 返回非 null | attrSetId=1 (Warrior) | `AttrSetDef` 非 null，含 2 个 Attribute | 集成 |
| ASC-05 | `GetAttrSetDef` 字段映射正确 | attrSetId=1 | Attack(InitValue=100, Min=0, Max=9999), Defense(InitValue=50, Min=0, Max=9999) | 集成 |
| ASC-06 | `GetAttrSetDef` 不存在的 attrSetId 返回 null | attrSetId=99999 | null | 容错 |

#### ASC 运行时创建测试组 (`ASCRuntimeCreationTests`)

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| CRT-01 | `CreateGASCarrier(type, ascId)` 返回有效 Carrier | type="Hero", ascId=1 | CarrierId.IsValid=true | 集成 |
| CRT-02 | Carrier 的 Entity 存在所有 GAS Buffer | 同上 | BEAttrSet / BAbility / BFixedTag / CAscBasicData | 集成 |
| CRT-03 | CAscBasicData.Level 与配置一致 | ascId=1 (Level=1) | Level == 1 | 集成 |
| CRT-04 | BFixedTag 与配置一致 | ascId=1 (Tags=[1]) | 包含 tag=1 | 集成 |
| CRT-05 | BEAttrSet 内容与属性集定义一致 | ascId=1 引用 attrSetId=1 | Attack attr: Code=3, Base=100, Min=0, Max=9999 | 集成 |
| CRT-06 | BAbility 已授予技能 | ascId=1 (Ability=[1001,1002]) | 2 个 Ability 已授予 | 集成 |
| CRT-07 | 不存在的 ascId 优雅降级 | ascId=99999 | 返回有效 Carrier，无标签/属性/技能 | 容错 |

### 测试代码更新

## 关联

- 需求：[R003 LubanConfigLoader 生成器配套运行时类型映射](../requirements/R003-luban-config-loader-helper-methods.md)
- 需求：[R004 运行时从 Luban 配置创建 ASC 对象](../requirements/R004-runtime-asc-creation-from-luban.md)
- 设计：[D003 Luban 配置桥接设计](../design/D003-luban-config-bridge.md)
- 任务：[T003](../tasks/T003-luban-config-loader-generator.md)
- 编码：[GAS 配置系统](../code/gas-config.md)
