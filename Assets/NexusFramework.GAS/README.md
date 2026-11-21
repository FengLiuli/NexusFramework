# NexusFramework GAS

`com.nexusframework.gas` —— 基于 NexusFramework Core 与 Unity DOTS/ECS 的 Gameplay Ability System，提供属性（Attribute）、效果（GameplayEffect）、能力（Ability）、Tag、Cue 与 MMC 的完整管线。

## 安装

要求 Unity 2022.3.0f1 或更高版本。UPM 只支持精确版本，不支持 `^`、`~` 或范围表达式。

必须同时声明 Core 与 GAS 两个 Git 依赖（UPM 无法从 `?path=` 推断同仓库的另一个包）：

```json
{
  "dependencies": {
    "com.nexusframework.core": "https://github.com/FengLiuli/NexusFramework.git?path=/Assets/NexusFramework#core-v2.0.0",
    "com.nexusframework.gas": "https://github.com/FengLiuli/NexusFramework.git?path=/Assets/NexusFramework.GAS#gas-v2.0.0"
  }
}
```

将 tag 替换为目标版本。宿主声明的 Core 版本必须 ≥ 本包 `package.json` 中要求的 `2.0.0`，否则在用到新 API 时会编译失败，且 UPM 不会自动纠正。

## 依赖与要求

| 项 | 要求 |
|---|---|
| Unity | 2022.3.0f1 或更高 |
| `com.nexusframework.core` | 2.0.0 |
| `com.unity.entities` | 1.3.15 |
| `com.unity.mathematics` | 1.3.2 |
| `com.unity.collections` | 2.5.7 |
| `com.unity.burst` | 1.8.19 |

运行时代码还会使用 Unity 内置模块 Physics、Audio、Animation、ParticleSystem 与 JsonSerialize，Unity 默认全部启用。

## 最小示例

以下示例不依赖 Luban/JSON：`JsonConfigLoader` 目前只实现了 `LoadRaw` 与 Tag 层级解析，因此用 `ConfigModel.Register*` 编程式注册配置。

```csharp
using System.Collections;
using NexusFramework;
using NexusFramework.DataCarrier;
using NexusFramework.GAS;
using NexusFramework.GAS.Config;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using NexusFramework.GAS.Queries;
using NexusFramework.GAS.Services;
using UnityEngine;

public sealed class MyGasArchitecture : GASArchitecture
{
    public const int AttrSetId = 1;
    public const int HpCode = 1;
    public const int HeroAscId = 1;
    public const int DamageEffectId = 100;
    public const int HitAbilityCode = 1001;

    public MyGasArchitecture() : base() { }
    public MyGasArchitecture(byte id) : base(id) { }

    protected override void OnInit()
    {
        base.OnInit();

        var config = GetModel<ConfigModel>();

        // Tag 必须先在层级中注册，否则 AddTag / HasTag 会失败
        config.RegisterTagHierarchy(new TagHierarchyData
        {
            Tags = new[]
            {
                new TagNode { Code = 1, Name = "State" },
                new TagNode { Code = 2, Name = "State.Debuff" }
            }
        });

        // 属性集：HP 初始 100，范围 [0, 9999]
        config.RegisterAttrSetDef(AttrSetId, new AttrSetDef
        {
            AttrSetCode = AttrSetId,
            Attributes = new[]
            {
                new AttrInitDef
                {
                    Code = HpCode,
                    InitValue = 100f,
                    UseMinValue = true, MinValue = 0f,
                    UseMaxValue = true, MaxValue = 9999f
                }
            }
        });

        // ASC 预设：等级、初始 Tag、属性集、授予的技能
        config.RegisterAscConfig(HeroAscId, new AscConfigData
        {
            Level = 1,
            Tags = new[] { 1 },
            AttrSetIds = new[] { AttrSetId },
            AbilityIds = new[] { HitAbilityCode }
        });

        // Instant 效果：HP -25
        config.RegisterEffect(DamageEffectId, new GameplayEffectComponentConfig[]
        {
            new ConfEffectBasicInfo { Name = "InstantDamage" },
            new MCConfModifiers
            {
                modifierSettings = new[]
                {
                    new ModifierSetting
                    {
                        AttrSetCode = AttrSetId,
                        AttrCode = HpCode,
                        Operation = GEOperation.Minus,
                        Magnitude = 25f,
                        MMC = new NexusFramework.GAS.ECS.MMCConfig
                        {
                            MmcType = typeof(MMCNone)
                        }
                    }
                }
            }
        });

        // 技能：激活时对自身施加 DamageEffectId
        config.RegisterAbility(HitAbilityCode, new AbilityComponentConfig[]
        {
            new ConfAbilityBaseInfo { Code = HitAbilityCode, Level = 1 },
            new MCConfAbilityLogic
            {
                AbilityLogicType = nameof(ALApplyEffect),
                Param = new XParamEffectIDs(new[] { DamageEffectId })
            }
        });
    }
}

public sealed class GasQuickStart : MonoBehaviour
{
    private MyGasArchitecture mArchitecture;

    private IEnumerator Start()
    {
        mArchitecture = new MyGasArchitecture();
        mArchitecture.Initialize();

        // 创建 Carrier 与 ECS Entity，并按 AscConfig 初始化属性、Tag 与技能
        int hero = mArchitecture.CreateGASCarrier("Hero", MyGasArchitecture.HeroAscId);

        ITagService tags = mArchitecture.GetTagService();
        bool hasState = tags.HasTag(hero, "State");

        // 效果是异步的，由 FixedStep 管线处理
        mArchitecture.GetService<EffectService>()
            .ApplyEffect(MyGasArchitecture.DamageEffectId, hero, hero);

        for (int i = 0; i < 5; i++)
            yield return new WaitForFixedUpdate();

        var attributes = mArchitecture.GetService<AttributeService>();
        float hp = attributes.GetCurrentValue(
            hero, MyGasArchitecture.AttrSetId, MyGasArchitecture.HpCode);
        Debug.Log($"HP={hp}, State={hasState}"); // 75, true

        // 只读查询走 Query
        float queried = mArchitecture.SendQuery(new QueryAttributeValue
        {
            Target = hero,
            AttrSetCode = MyGasArchitecture.AttrSetId,
            AttrCode = MyGasArchitecture.HpCode
        });
        Debug.Log($"Query HP={queried}");
    }

    private void OnDestroy() => mArchitecture?.Dispose();
}
```

## 核心概念

- **GASArchitecture**：GAS 子系统宿主，`OnInit` 中注册 `WorldService`、`TimerService`、`EventBridgeService`、`EffectService`、`AbilityService`、`CueService`、`AttributeService`、`ConfigModel`、`GASEntityMapModel` 与配置加载器。
- **Carrier / ASC**：Carrier 是 Core 的 `int` 句柄；`CreateGASCarrier` 在其上创建 ECS Entity 并挂载属性集、效果、技能与 Tag 缓冲。
- **Effect**：通过 `EffectService.ApplyEffect(configId, target, source)` 施加，由 FixedStep ECS 管线异步执行。
- **Ability**：通过 `AbilityService.GrantAbility / TryActivate / TryEnd / TryCancel / RemoveAbility` 管理。
- **Attribute**：`AttributeService` 读写基础值与当前值；查询可用 `QueryAttributeValue`。
- **Tag**：由 `GetTagService()` 返回的 `ITagService` 管理，点分层级匹配。
- **Cue / MMC**：表现层与数值计算的扩展点，分别通过 `CueService` / `CueHelper` 与 `GasMmcHelper` 注册。

## 关键约束

- `GASArchitecture` 用工厂创建时，子类必须提供 `public (byte instanceId)` 构造函数；直接 `new` 则必须手动调用 `Initialize()`。
- `EffectService.ApplyEffect` 是异步的：施加后需要等待若干 FixedStep 才能读到更新后的属性值。
- `SetBaseValue` 只标记 Dirty，`CurrentValue` 在下一个固定帧由 ECS 系统重算。
- Tag 必须先在 `TagHierarchyData` 中注册；层级由点分名推导。
- 事件桥接发送的是 ECS 事件（如 `NexusFramework.GAS.ECS.AttributeChangedEvent`）；订阅时注意 `Target` 是 `Entity`，需用 `GASEntityMapModel.GetCarrierId` 反查 Carrier。
- `AbilityLogicBase` 的 `CreateGameplayEffectEntity` / `ApplyGameplayEffectTo` 是程序集内部方法，包外自定义 AbilityLogic 不能调用。
- `NexusFramework.GAS.Config.MMCConfig` 与 `NexusFramework.GAS.ECS.MMCConfig` 是不同类型，示例中使用全限定名避免歧义。

## 文档与链接

- [CHANGELOG.md](./CHANGELOG.md)
- [GAS 设计 D002](https://github.com/FengLiuli/NexusFramework/blob/main/docs/design/D002-gas-design.md)
- [GAS 使用指南](https://github.com/FengLiuli/NexusFramework/blob/main/docs/gas-usage-guide.md)（部分示例仍基于旧 API，以本 README 与代码为准）
- [包版本策略 D009](https://github.com/FengLiuli/NexusFramework/blob/main/docs/design/D009-upm-package-versioning.md)

## 过渡 API

<!-- BEGIN TRANSITIONAL APIS（由扫描器生成，勿手改） -->

## Transitional APIs

| Deprecated API | Replacement | Since | Remove in |
|---|---|---|---|

<!-- END TRANSITIONAL APIS -->

## 与 Core 的兼容关系

| 本包版本 | 依赖 Core（最低） | 已验证 Core 范围 | Unity 最低 | 备注 |
|---|---|---|---|---|
| `2.0.0` | `2.0.0` | `2.0.x` | 2022.3 | 首个正式版本 |
