using Unity.Collections;
using UnityEngine;
using NexusFramework;
using NexusFramework.GAS;
using NexusFramework.GAS.Config;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using GameplayCueConfig = NexusFramework.GAS.Config.GameplayCueConfig;
using MMCConfig = NexusFramework.GAS.Config.MMCConfig;

namespace NexusFramework.GAS.Demo
{
    /// <summary>
    /// Demo 游戏架构 —— 程序化注册所有 Effect、Ability、Cue、MMC、Tag 配置
    /// </summary>
    public class DemoGameArchitecture : GASArchitecture
    {
        public DemoGameArchitecture() : base() { ArchitectureId = 1; }

        protected override IConfigLoader CreateConfigLoader()
        {
            return new DemoConfigLoader();
        }

        protected override void OnInit()
        {
            base.OnInit();

            Debug.Log("[DemoArchitecture] Registering all configs...");
            var model = GetModel<ConfigModel>();
            DemoConfigLoader.Populate(model);

            Debug.Log("[DemoArchitecture] Registering demo cues...");
            RegisterDemoCues();

            Debug.Log("[DemoArchitecture] Architecture initialized successfully!");
        }

        private void RegisterDemoCues()
        {
            CueHelper.RegisterCue<DemoLogCue>(nameof(DemoLogCue), typeof(XParamString));
            CueHelper.RegisterCue<DemoColorCue>(nameof(DemoColorCue), typeof(XParamFloat));
            Debug.Log($"[DemoArchitecture] Registered cues: DemoLogCue, DemoColorCue");
        }
    }

    public class DemoConfigLoader : IConfigLoader
    {
        public bool Initialized { get; set; }
        void ICanInit.Init() => Initialized = true;
        void ICanInit.Deinit() => Initialized = false;

        public string LoadRaw(string fullPath) => null;
        public GameplayEffectComponentConfig[] ParseGameplayEffect(string json) => null;
        public AbilityComponentConfig[] ParseAbility(string json) => null;
        public GameplayCueConfig ParseGameplayCue(string json) => default;
        public MMCConfig ParseMmc(string json) => default;
        public TagHierarchyData ParseTagHierarchy(string json) => default;
        public AscConfigData? GetAscConfig(int ascId) => null;
        public AttrSetDef? GetAttrSetDef(int attrSetId) => null;

        public static void Populate(ConfigModel model)
        {
            RegisterEffects(model);
            RegisterAbilities(model);
            RegisterTags(model);
            RegisterMmcs(model);
        }

        private static void RegisterEffects(ConfigModel model)
        {
            var ecsMMC = typeof(NexusFramework.GAS.ECS.MMCConfig);

            model.RegisterEffect(1, new GameplayEffectComponentConfig[]
            {
                new ConfEffectBasicInfo { Name = "InstantDamage" },
                new DemoInstantDamageConfig()
            });

            model.RegisterEffect(2, new GameplayEffectComponentConfig[]
            {
                new ConfEffectBasicInfo { Name = "InstantHeal" },
                new DemoInstantHealConfig()
            });

            model.RegisterEffect(3, new GameplayEffectComponentConfig[]
            {
                new ConfEffectBasicInfo { Name = "DOTPoison" },
                new ConfDuration
                {
                    duration = 30,
                    timeUnit = TimeUnit.Frame,
                    ResetStartTimeWhenActivated = false,
                    StopTickWhenDeactivated = false
                }
            });

            model.RegisterEffect(4, new GameplayEffectComponentConfig[]
            {
                new ConfEffectBasicInfo { Name = "StrengthBuff" },
                new ConfDuration
                {
                    duration = 60,
                    timeUnit = TimeUnit.Frame,
                    ResetStartTimeWhenActivated = true,
                    StopTickWhenDeactivated = false
                },
                new MCConfModifiers
                {
                    modifierSettings = new[]
                    {
                        new ModifierSetting
                        {
                            AttrSetCode = 1, AttrCode = 3,
                            Operation = GEOperation.Add, Magnitude = 20f,
                            MMC = new NexusFramework.GAS.ECS.MMCConfig
                            {
                                MmcType = typeof(MMCScalableFloat),
                                MmcParameter = new MmcParaFloatScale()
                            }
                        }
                    }
                },
                new ConfStacking
                {
                    StackType = EffectStackType.AggregateByTarget,
                    StackingCode = 100, LimitCount = 3,
                    EffectDurationRefreshPolicy = EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication,
                    EffectPeriodResetPolicy = EffectPeriodResetPolicy.NeverRefresh,
                    EffectExpirationPolicy = EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration,
                    denyOverflowApplication = true, clearStackOnOverflow = false,
                    overflowEffects = new GameplayEffectComponentConfig[0][]
                }
            });

            model.RegisterEffect(5, new GameplayEffectComponentConfig[]
            {
                new ConfEffectBasicInfo { Name = "ManaRegen" },
                new ConfDuration
                {
                    duration = 20, timeUnit = TimeUnit.Frame,
                    ResetStartTimeWhenActivated = false, StopTickWhenDeactivated = false
                },
                new MCConfModifiers
                {
                    modifierSettings = new[]
                    {
                        new ModifierSetting
                        {
                            AttrSetCode = 1, AttrCode = 2,
                            Operation = GEOperation.Add, Magnitude = 5f,
                            MMC = new NexusFramework.GAS.ECS.MMCConfig
                            {
                                MmcType = typeof(MMCScalableFloat),
                                MmcParameter = new MmcParaFloatScale()
                            }
                        }
                    }
                }
            });

            model.RegisterEffect(6, new GameplayEffectComponentConfig[]
            {
                new ConfEffectBasicInfo { Name = "CostMP" },
                new DemoCostMPConfig()
            });

            model.RegisterEffect(7, new GameplayEffectComponentConfig[]
            {
                new ConfEffectBasicInfo { Name = "Stun" },
                new ConfDuration
                {
                    duration = 15, timeUnit = TimeUnit.Frame,
                    ResetStartTimeWhenActivated = false, StopTickWhenDeactivated = false
                },
                new DemoStunEffectConfig()
            });

            model.RegisterEffect(8, new GameplayEffectComponentConfig[]
            {
                new ConfEffectBasicInfo { Name = "FireDamage" },
                new DemoFireDamageConfig()
            });
        }

        private static void RegisterAbilities(ConfigModel model)
        {
            model.RegisterAbility(1001, DemoAbilityConfigs.CreateFireball());
            model.RegisterAbility(1002, DemoAbilityConfigs.CreateHeal());
            model.RegisterAbility(1003, DemoAbilityConfigs.CreatePoisonStrike());
            model.RegisterAbility(1004, DemoAbilityConfigs.CreatePowerBuff());
        }

        private static void RegisterTags(ConfigModel model)
        {
            var tagNodes = new TagNode[]
            {
                new TagNode { Code = 100, Name = "State.Debuff", Children = new int[0] },
                new TagNode { Code = 200, Name = "Ability",      Children = new int[0] },
                new TagNode { Code = 300, Name = "Event",        Children = new int[0] },
                new TagNode { Code = 400, Name = "State",        Children = new[] { 100 } },
                new TagNode { Code = 600, Name = "Cooldown",     Children = new int[0] },
            };
            model.RegisterTagHierarchy(new TagHierarchyData { Tags = tagNodes });
        }

        private static void RegisterMmcs(ConfigModel model)
        {
            var mmcs = new MMCConfig[1];
            mmcs[0] = new MMCConfig
            {
                MmcType = "MMCScalableFloat",
                Param = new MmcParaFloatScale(k: 1f, b: 0f)
            };
            model.RegisterMmcs(mmcs);
        }
    }
}
