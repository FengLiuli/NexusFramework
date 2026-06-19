using UnityEngine;
using Unity.Entities;
using NexusFramework;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using NexusFramework.GAS.Services;
using System.Collections.Generic;

namespace NexusFramework.GAS.Demo
{
    /// <summary>
    /// 静态延迟队列 —— AbilityLogic 在 ECS 更新期间不能做结构性变更，
    /// 通过此队列延迟到 LateUpdate（所有 ECS System 完成后）执行。
    /// </summary>
    public static class DemoDeferredQueue
    {
        private static readonly List<System.Action> _actions = new List<System.Action>();
        private static readonly object _lock = new object();

        public static void Enqueue(System.Action action)
        {
            lock (_lock) _actions.Add(action);
        }

        public static void Drain()
        {
            System.Action[] snapshot;
            lock (_lock)
            {
                if (_actions.Count == 0) return;
                snapshot = _actions.ToArray();
                _actions.Clear();
            }
            foreach (var action in snapshot)
                action?.Invoke();
        }
    }

    /// <summary>
    /// Fireball (1001) — 通过 DemoDeferredQueue 延迟施加效果
    /// </summary>
    public class DemoFireballLogic : AbilityLogicBase<XParamNone>
    {
        public DemoFireballLogic(Entity ability, IArchitecture architecture) : base(ability, architecture) { }

        public override void ActivateAbility(GlobalTimer timer)
        {
            var owner = GetOwnerAscEntity();
            var arch = Architecture;

            DemoDeferredQueue.Enqueue(() =>
            {
                var em = arch.GetService<WorldService>().EntityManager;
                GameplayEffectComponentConfig.SetEntityManager(em);

                // 施加火焰伤害 (HP -50)
                var configs = new GameplayEffectComponentConfig[] { new DemoFireDamageConfig() };
                var ge = em.CreateEntity();
                foreach (var cfg in configs) cfg.LoadToGameplayEffectEntity(ge);
                em.AddComponent<CEffectInUsage>(ge);
                em.AddComponent<WipInstantiateEffect>(ge);
                em.SetComponentData(ge, new CEffectInUsage { Source = owner, Target = owner });

                Debug.Log("[DemoAbility] Fireball: dealt 50 damage + consumed 30 MP");
            });
        }

        public override void EndAbility(GlobalTimer timer) { TryEndSelf(); }
        public override void CancelAbility(GlobalTimer timer) { EndAbility(timer); }
        public override void AbilityTick(GlobalTimer timer) { }
    }

    /// <summary>
    /// Heal (1002) — 自身治疗
    /// </summary>
    public class DemoHealLogic : AbilityLogicBase<XParamNone>
    {
        public DemoHealLogic(Entity ability, IArchitecture architecture) : base(ability, architecture) { }

        public override void ActivateAbility(GlobalTimer timer)
        {
            var owner = GetOwnerAscEntity();
            var arch = Architecture;

            DemoDeferredQueue.Enqueue(() =>
            {
                var em = arch.GetService<WorldService>().EntityManager;
                GameplayEffectComponentConfig.SetEntityManager(em);

                var configs = new GameplayEffectComponentConfig[] { new DemoInstantHealConfig() };
                var ge = em.CreateEntity();
                foreach (var cfg in configs) cfg.LoadToGameplayEffectEntity(ge);
                em.AddComponent<CEffectInUsage>(ge);
                em.AddComponent<WipInstantiateEffect>(ge);
                em.SetComponentData(ge, new CEffectInUsage { Source = owner, Target = owner });

                Debug.Log("[DemoAbility] Heal: restored 50 HP");
            });
        }

        public override void EndAbility(GlobalTimer timer) { TryEndSelf(); }
        public override void CancelAbility(GlobalTimer timer) { EndAbility(timer); }
        public override void AbilityTick(GlobalTimer timer) { }
    }

    /// <summary>
    /// PoisonStrike (1003) — 施加 DOT 效果
    /// </summary>
    public class DemoPoisonStrikeLogic : AbilityLogicBase<XParamNone>
    {
        public DemoPoisonStrikeLogic(Entity ability, IArchitecture architecture) : base(ability, architecture) { }

        public override void ActivateAbility(GlobalTimer timer)
        {
            var owner = GetOwnerAscEntity();
            var arch = Architecture;

            DemoDeferredQueue.Enqueue(() =>
            {
                var em = arch.GetService<WorldService>().EntityManager;
                GameplayEffectComponentConfig.SetEntityManager(em);

                var configs = new GameplayEffectComponentConfig[]
                {
                    new ConfDuration { duration = 30, timeUnit = TimeUnit.Frame, ResetStartTimeWhenActivated = false, StopTickWhenDeactivated = false },
                    new ConfPeriod { Period = 5, ResetTimeCountWhenDeactivated = false, GameplayEffectSettings = new[] { new GameplayEffectComponentConfig[] { new DemoPoisonTickConfig() } } },
                    new ConfEffectBasicInfo { Name = "DOTPoison" }
                };

                var dotGE = em.CreateEntity();
                foreach (var cfg in configs) cfg.LoadToGameplayEffectEntity(dotGE);
                em.AddComponent<CEffectInUsage>(dotGE);
                em.AddComponent<WipInstantiateEffect>(dotGE);
                em.SetComponentData(dotGE, new CEffectInUsage { Source = owner, Target = owner });

                Debug.Log("[DemoAbility] PoisonStrike: applied DOT (30F, 5 HP/5F)");
            });
        }

        public override void EndAbility(GlobalTimer timer) { TryEndSelf(); }
        public override void CancelAbility(GlobalTimer timer) { EndAbility(timer); }
        public override void AbilityTick(GlobalTimer timer) { }
    }

    /// <summary>
    /// PowerBuff (1004) — 堆叠 Attack Buff
    /// </summary>
    public class DemoPowerBuffLogic : AbilityLogicBase<XParamNone>
    {
        public DemoPowerBuffLogic(Entity ability, IArchitecture architecture) : base(ability, architecture) { }

        public override void ActivateAbility(GlobalTimer timer)
        {
            var owner = GetOwnerAscEntity();
            var arch = Architecture;

            DemoDeferredQueue.Enqueue(() =>
            {
                var em = arch.GetService<WorldService>().EntityManager;
                GameplayEffectComponentConfig.SetEntityManager(em);

                var configs = new GameplayEffectComponentConfig[]
                {
                    new ConfDuration { duration = 60, timeUnit = TimeUnit.Frame, ResetStartTimeWhenActivated = true, StopTickWhenDeactivated = false },
                    new ConfEffectBasicInfo { Name = "StrengthBuff" },
                    new MCConfModifiers
                    {
                        modifierSettings = new[]
                        {
                            new ModifierSetting
                            {
                                AttrSetCode = 1, AttrCode = 3,
                                Operation = GEOperation.Add, Magnitude = 20f,
                                MMC = new NexusFramework.GAS.ECS.MMCConfig { MmcType = typeof(MMCScalableFloat), MmcParameter = new MmcParaFloatScale() }
                            }
                        }
                    },
                    new ConfStacking
                    {
                        StackType = EffectStackType.AggregateByTarget, StackingCode = 100, LimitCount = 3,
                        EffectDurationRefreshPolicy = EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication,
                        EffectPeriodResetPolicy = EffectPeriodResetPolicy.NeverRefresh,
                        EffectExpirationPolicy = EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration,
                        denyOverflowApplication = true, clearStackOnOverflow = false,
                        overflowEffects = new GameplayEffectComponentConfig[0][]
                    }
                };

                var buffGE = em.CreateEntity();
                foreach (var cfg in configs) cfg.LoadToGameplayEffectEntity(buffGE);
                em.AddComponent<CEffectInUsage>(buffGE);
                em.AddComponent<WipInstantiateEffect>(buffGE);
                em.SetComponentData(buffGE, new CEffectInUsage { Source = owner, Target = owner });

                Debug.Log("[DemoAbility] PowerBuff: +20 ATK for 60F (stackable x3)");
            });
        }

        public override void EndAbility(GlobalTimer timer) { TryEndSelf(); }
        public override void CancelAbility(GlobalTimer timer) { EndAbility(timer); }
        public override void AbilityTick(GlobalTimer timer) { }
    }

    // ============================================================
    // Ability 配置
    // ============================================================
    public static class DemoAbilityConfigs
    {
        public static AbilityComponentConfig[] CreateFireball()
        {
            return new AbilityComponentConfig[]
            {
                new ConfAbilityBaseInfo { Code = 1001, Level = 1 },
                new MCConfAbilityLogic { AbilityLogicType = nameof(DemoFireballLogic) },
                new ConfAbilityCooldown
                {
                    Cooldown = 30,
                    CooldownComponentConfigs = new GameplayEffectComponentConfig[]
                    {
                        new ConfDuration { duration = 30, timeUnit = TimeUnit.Frame },
                        new ConfEffectGrantedTags { tags = new[] { 600 } }
                    }
                },
                new ConfAbilityCost
                {
                    CostComponentConfigs = new GameplayEffectComponentConfig[]
                    {
                        new DemoCostMPConfig()
                    }
                }
            };
        }

        public static AbilityComponentConfig[] CreateHeal()
        {
            return new AbilityComponentConfig[]
            {
                new ConfAbilityBaseInfo { Code = 1002, Level = 1 },
                new MCConfAbilityLogic { AbilityLogicType = nameof(DemoHealLogic) },
                new ConfAbilityCooldown
                {
                    Cooldown = 20,
                    CooldownComponentConfigs = new GameplayEffectComponentConfig[]
                    {
                        new ConfDuration { duration = 20, timeUnit = TimeUnit.Frame },
                        new ConfEffectGrantedTags { tags = new[] { 601 } }
                    }
                }
            };
        }

        public static AbilityComponentConfig[] CreatePoisonStrike()
        {
            return new AbilityComponentConfig[]
            {
                new ConfAbilityBaseInfo { Code = 1003, Level = 1 },
                new MCConfAbilityLogic { AbilityLogicType = nameof(DemoPoisonStrikeLogic) },
                new ConfAbilityCooldown
                {
                    Cooldown = 40,
                    CooldownComponentConfigs = new GameplayEffectComponentConfig[]
                    {
                        new ConfDuration { duration = 40, timeUnit = TimeUnit.Frame },
                        new ConfEffectGrantedTags { tags = new[] { 602 } }
                    }
                }
            };
        }

        public static AbilityComponentConfig[] CreatePowerBuff()
        {
            return new AbilityComponentConfig[]
            {
                new ConfAbilityBaseInfo { Code = 1004, Level = 1 },
                new MCConfAbilityLogic { AbilityLogicType = nameof(DemoPowerBuffLogic) },
                new ConfAbilityCooldown
                {
                    Cooldown = 60,
                    CooldownComponentConfigs = new GameplayEffectComponentConfig[]
                    {
                        new ConfDuration { duration = 60, timeUnit = TimeUnit.Frame },
                        new ConfEffectGrantedTags { tags = new[] { 603 } }
                    }
                }
            };
        }
    }
}
