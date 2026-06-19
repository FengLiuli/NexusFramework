using Unity.Collections;
using Unity.Entities;
using NexusFramework.GAS.ECS;

namespace NexusFramework.GAS.Demo
{
    // ============================================================
    // 瞬时伤害效果 (configId=1)
    // 作用于目标，HP -30，带 ScalableFloat MMC
    // ============================================================
    public sealed class DemoInstantDamageConfig : GameplayEffectComponentConfig
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<MCModifiers>(ge);
            _entityManager.SetComponentData(ge, new MCModifiers(new[]
            {
                new EffectModifier
                {
                    AttrSetCode = 1,
                    AttrCode = 1, // HP
                    Operation = GEOperation.Minus,
                    Magnitude = 30f,
                    MMC = new MMCScalableFloat()   // 直通 MMC，伤害 30
                }
            }));
        }
    }

    // ============================================================
    // 瞬时治疗效果 (configId=2)
    // 作用于自身，HP +50
    // ============================================================
    public sealed class DemoInstantHealConfig : GameplayEffectComponentConfig
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<MCModifiers>(ge);
            _entityManager.SetComponentData(ge, new MCModifiers(new[]
            {
                new EffectModifier
                {
                    AttrSetCode = 1,
                    AttrCode = 1, // HP
                    Operation = GEOperation.Add,
                    Magnitude = 50f,
                    MMC = new MMCScalableFloat()
                }
            }));
        }
    }

    // ============================================================
    // DOT 毒效果 (configId=3)
    // 持续 30 帧，每 5 帧 -5 HP
    // ============================================================
    public sealed class DemoDOTPoisonConfig : GameplayEffectComponentConfig
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            // 持续时间
            _entityManager.AddComponent<CDuration>(ge);
            _entityManager.SetComponentData(ge, new CDuration
            {
                duration = 30,
                timeUnit = TimeUnit.Frame,
                active = false,
                ResetStartTimeWhenActivated = false,
                StopTickWhenDeactivated = false
            });

            // 基本信息
            _entityManager.SetName(ge, "GE_DOTPoison");
            _entityManager.AddComponent<CEffectBasicInfo>(ge);
            _entityManager.SetComponentData(ge, new CEffectBasicInfo { name = "DOTPoison" });
        }
    }

    // ============================================================
    // 力量 Buff (configId=4)
    // 持续 60 帧，+20 Attack，可堆叠 (limit=3)
    // ============================================================
    public sealed class DemoStrengthBuffConfig : GameplayEffectComponentConfig
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            // 持续时间
            _entityManager.AddComponent<CDuration>(ge);
            _entityManager.SetComponentData(ge, new CDuration
            {
                duration = 60,
                timeUnit = TimeUnit.Frame,
                active = false,
                ResetStartTimeWhenActivated = false,
                StopTickWhenDeactivated = false
            });

            // 基本信息
            _entityManager.SetName(ge, "GE_StrengthBuff");
            _entityManager.AddComponent<CEffectBasicInfo>(ge);
            _entityManager.SetComponentData(ge, new CEffectBasicInfo { name = "StrengthBuff" });

            // 属性修改：+20 Attack
            _entityManager.AddComponent<MCModifiers>(ge);
            _entityManager.SetComponentData(ge, new MCModifiers(new[]
            {
                new EffectModifier
                {
                    AttrSetCode = 1,
                    AttrCode = 3, // Attack
                    Operation = GEOperation.Add,
                    Magnitude = 20f,
                    MMC = new MMCScalableFloat()
                }
            }));

            // 堆叠配置：可按目标聚合，limit=3
            _entityManager.AddComponent<CStacking>(ge);
            _entityManager.SetComponentData(ge, new CStacking
            {
                StackType = EffectStackType.AggregateByTarget,
                StackingCode = 100,
                LimitCount = 3,
                StackCount = 0,
                EffectDurationRefreshPolicy = EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication,
                EffectPeriodResetPolicy = EffectPeriodResetPolicy.NeverRefresh,
                EffectExpirationPolicy = EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration,
                denyOverflowApplication = true,
                clearStackOnOverflow = false,
                overflowEffects = new NativeArray<Entity>(0, Allocator.Persistent)
            });
        }
    }

    // ============================================================
    // 法力恢复效果 (configId=5)
    // 持续 20 帧，每 3 帧 +5 MP
    // ============================================================
    public sealed class DemoManaRegenConfig : GameplayEffectComponentConfig
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<CDuration>(ge);
            _entityManager.SetComponentData(ge, new CDuration
            {
                duration = 20,
                timeUnit = TimeUnit.Frame,
                active = false,
                ResetStartTimeWhenActivated = false,
                StopTickWhenDeactivated = false
            });

            _entityManager.SetName(ge, "GE_ManaRegen");
            _entityManager.AddComponent<CEffectBasicInfo>(ge);
            _entityManager.SetComponentData(ge, new CEffectBasicInfo { name = "ManaRegen" });

            _entityManager.AddComponent<MCModifiers>(ge);
            _entityManager.SetComponentData(ge, new MCModifiers(new[]
            {
                new EffectModifier
                {
                    AttrSetCode = 1,
                    AttrCode = 2, // MP
                    Operation = GEOperation.Add,
                    Magnitude = 5f,
                    MMC = new MMCScalableFloat()
                }
            }));
        }
    }

    // ============================================================
    // 消耗 MP 效果 (configId=6)
    // 瞬时，-30 MP，用于技能消耗
    // ============================================================
    public sealed class DemoCostMPConfig : GameplayEffectComponentConfig
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<MCModifiers>(ge);
            _entityManager.SetComponentData(ge, new MCModifiers(new[]
            {
                new EffectModifier
                {
                    AttrSetCode = 1,
                    AttrCode = 2, // MP
                    Operation = GEOperation.Minus,
                    Magnitude = 30f,
                    MMC = new MMCScalableFloat()
                }
            }));
        }
    }

    // ============================================================
    // 眩晕效果 (configId=7)
    // 持续 15 帧，带免疫标签 (100 = State.Debuff)
    // ============================================================
    public sealed class DemoStunEffectConfig : GameplayEffectComponentConfig
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<CDuration>(ge);
            _entityManager.SetComponentData(ge, new CDuration
            {
                duration = 15,
                timeUnit = TimeUnit.Frame,
                active = false,
                ResetStartTimeWhenActivated = false,
                StopTickWhenDeactivated = false
            });

            _entityManager.SetName(ge, "GE_Stun");
            _entityManager.AddComponent<CEffectBasicInfo>(ge);
            _entityManager.SetComponentData(ge, new CEffectBasicInfo { name = "Stun" });

            // 免疫标签：目标有 Debuff 类标签 (100) 则免疫
            _entityManager.AddComponent<CEffectImmunityTags>(ge);
            _entityManager.SetComponentData(ge, new CEffectImmunityTags
            {
                requirement = new TagRequirementData
                {
                    all = new NativeArray<int>(0, Allocator.Persistent),
                    any = new NativeArray<int>(new[] { 100 }, Allocator.Persistent),
                    none = new NativeArray<int>(0, Allocator.Persistent)
                }
            });
        }
    }

    // ============================================================
    // 瞬时火焰伤害效果 (configId=8)
    // -50 HP，用于技能 Fireball
    // ============================================================
    public sealed class DemoFireDamageConfig : GameplayEffectComponentConfig
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<MCModifiers>(ge);
            _entityManager.SetComponentData(ge, new MCModifiers(new[]
            {
                new EffectModifier
                {
                    AttrSetCode = 1,
                    AttrCode = 1, // HP
                    Operation = GEOperation.Minus,
                    Magnitude = 50f,
                    MMC = new MMCScalableFloat()
                }
            }));
        }
    }

    // ============================================================
    // DOT 毒周期性伤害 (configId=9)
    // 每 Tick -5 HP，由 DOTPoison GE 的 Period 触发
    // ============================================================
    public sealed class DemoPoisonTickConfig : GameplayEffectComponentConfig
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<MCModifiers>(ge);
            _entityManager.SetComponentData(ge, new MCModifiers(new[]
            {
                new EffectModifier
                {
                    AttrSetCode = 1,
                    AttrCode = 1, // HP
                    Operation = GEOperation.Minus,
                    Magnitude = 5f,
                    MMC = new MMCScalableFloat()
                }
            }));
        }
    }
}
