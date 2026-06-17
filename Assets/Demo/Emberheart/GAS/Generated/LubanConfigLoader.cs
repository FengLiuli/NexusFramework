///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using NexusFramework.GAS.ECS;
using UnityEngine;
using NexusFramework.GAS.Models;
using XParam = NexusFramework.GAS.ECS.XParam;

namespace NexusFramework.GAS.Config
{
    /// <summary>
    /// 由 LubanConfigLoaderGenerator 自动生成。
    /// 将 Luban 表格数据转换为 GAS 运行时配置。
    /// </summary>
    public class LubanConfigLoader : IConfigLoader
    {
        public bool Initialized { get; set; }
        void ICanInit.Init() => Initialized = true;
        void ICanInit.Deinit() => Initialized = false;

        private cfg.Tables _tables;
        public cfg.Tables Tables => _tables;

        /// <summary>
        /// 初始化并加载所有 Luban 表格。
        /// loader: Func<string, JSONNode>，接收表名返回 JSON 数据。
        /// </summary>
        public void LoadTables(Func<string, Luban.SimpleJSON.JSONNode> loader)
        {
            if (_tables != null) return;
            _tables = new cfg.Tables(loader);
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Editor 下从本地 JSON 文件加载表格（通过 GASSettingAsset.TableOutputPath）
        /// </summary>
        public void LoadTablesForEditor(string jsonDir)
        {
            _tables = new cfg.Tables(file =>
                Luban.SimpleJSON.JSON.Parse(System.IO.File.ReadAllText($"{jsonDir}/{file}.json")));
        }
        #endif

        /// <summary>
        /// 初始化表格 + 注册配置查询委托。
        /// </summary>
        public void Init(Func<string, Luban.SimpleJSON.JSONNode> loader)
        {
            LoadTables(loader);
        }

        // =========================================================
        // IConfigLoader 接口实现
        // 注意：LubanConfigLoader 不使用 LoadRaw + Parse 模式，
        // 请使用 GetEffectConfig(id) / GetAbilityConfig(id) 等强类型方法。
        // =========================================================

        public string LoadRaw(string fullPath) => null;

        public GameplayEffectComponentConfig[] ParseGameplayEffect(string json) => null;
        public AbilityComponentConfig[] ParseAbility(string json) => null;
        public GameplayCueConfig ParseGameplayCue(string json) => default;
        public MMCConfig ParseMmc(string json) => default;
        public TagHierarchyData ParseTagHierarchy(string json) => default;

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

            var attrs = new AttrInitDef[data.Attribute.Length];
            for (int i = 0; i < data.Attribute.Length; i++)
            {
                var src = data.Attribute[i];
                attrs[i] = new AttrInitDef
                {
                    Code = src.ID,
                    InitValue = src.InitValue,
                    MinValue = src.MinValue,
                    MaxValue = src.MaxValue,
                    UseMinValue = src.UseMinValue,
                    UseMaxValue = src.UseMaxValue
                };
            }

            return new AttrSetDef
            {
                AttrSetCode = attrSetId,
                Attributes = attrs
            };
        }


        /// <summary>
        /// 将 _tables 中所有 ASC/AttrSet 配置批量注册到 ConfigModel，支持追加覆盖。
        /// 多次调用、多数据源调用均可安全叠加。
        /// </summary>
        public void RegisterAllConfigTo(ConfigModel configModel)
        {
            if (_tables == null) return;

            // 注册所有 ASC 配置
            foreach (var asc in _tables.Tbasc.DataList)
            {
                configModel.RegisterAscConfig(asc.ID, new AscConfigData
                {
                    Level = asc.Level,
                    Tags = asc.Tag,
                    AttrSetIds = asc.AttrSet,
                    AbilityIds = asc.Ability
                });
            }

            // 注册所有属性集定义
            foreach (var attrSet in _tables.TbattributeSet.DataList)
            {
                var attrs = new AttrInitDef[attrSet.Attribute.Length];
                for (int i = 0; i < attrSet.Attribute.Length; i++)
                {
                    var src = attrSet.Attribute[i];
                    attrs[i] = new AttrInitDef
                    {
                        Code = src.ID,
                        InitValue = src.InitValue,
                        MinValue = src.MinValue,
                        MaxValue = src.MaxValue,
                        UseMinValue = src.UseMinValue,
                        UseMaxValue = src.UseMaxValue
                    };
                }
                configModel.RegisterAttrSetDef(attrSet.ID, new AttrSetDef
                {
                    AttrSetCode = attrSet.ID,
                    Attributes = attrs
                });
            }
        }


        /// <summary>通过 ID 获取 GameplayEffect 配置</summary>
        public GameplayEffectComponentConfig[] GetEffectConfig(int id)
        {
            if (_tables == null) return null;

            var data = Tables.TbgameplayEffect.Get(id);
            if (data == null)
            {
                Debug.LogError($"[LubanConfigLoader] GameplayEffect_ID:{id} 不存在.");
                return null;
            }

            var configs = new List<GameplayEffectComponentConfig>();

            // 局部辅助: TagRequirement → (all, any, none)
            (int[] all, int[] any, int[] none)? ParseTagRequirement(cfg.TagRequirementData? req)
            {
                if (req == null) return null;
                var r = req.Value;
                int[] all = null, any = null, none = null;
                if (r.All is { Count: > 0 }) all = r.All.Where(x => x > 0).ToArray();
                if (r.Any is { Count: > 0 }) any = r.Any.Where(x => x > 0).ToArray();
                if (r.None is { Count: > 0 }) none = r.None.Where(x => x > 0).ToArray();
                if (all == null && any == null && none == null) return null;
                return (all, any, none);
            }

            // AssetTags
            if (data.AssetTags is { Count: > 0 })
                configs.Add(new ConfAssetTags { tags = data.AssetTags.ToArray() });

            // GrantedTags
            if (data.GrantedTags is { Count: > 0 })
                configs.Add(new ConfEffectGrantedTags { tags = data.GrantedTags.ToArray() });

            // ApplicationRequiredTags
            {
                var result = ParseTagRequirement(data.ApplicationRequiredTags);
                if (result != null)
                    configs.Add(new ConfApplicationRequiredTags
                    { all = result.Value.all, any = result.Value.any, none = result.Value.none });
            }

            // OngoingRequiredTags
            {
                var result = ParseTagRequirement(data.OngoingRequiredTags);
                if (result != null)
                    configs.Add(new ConfOngoingRequiredTags
                    { all = result.Value.all, any = result.Value.any, none = result.Value.none });
            }

            // RemoveGameplayEffectsWithTags
            {
                var result = ParseTagRequirement(data.RemoveGameplayEffectsWithTags);
                if (result != null)
                    configs.Add(new ConfRemoveEffectWithTags
                    { all = result.Value.all, any = result.Value.any, none = result.Value.none });
            }

            // ImmunityTags
            {
                var result = ParseTagRequirement(data.ImmunityTags);
                if (result != null)
                    configs.Add(new ConfEffectImmunityTags
                    { all = result.Value.all, any = result.Value.any, none = result.Value.none });
            }

            // Duration
            if (data.Duration != null && data.Duration.Value.Time != 0)
            {
                configs.Add(new ConfDuration
                {
                    duration = data.Duration.Value.Time,
                    timeUnit = (TimeUnit)data.Duration.Value.TimeUnit,
                    ResetStartTimeWhenActivated = data.Duration.Value.ResetStartTimeWhenActivated
                });
            }

            // Period
            if (data.Period is { Time: > 0 })
            {
                var periodEffectConfigs = new List<GameplayEffectComponentConfig[]>();
                foreach (var effectID in data.Period.Value.Effects)
                    periodEffectConfigs.Add(GetEffectConfig(effectID));
                configs.Add(new ConfPeriod
                {
                    Period = data.Period.Value.Time,
                    ResetTimeCountWhenDeactivated = data.Period.Value.FirstTrigger,
                    GameplayEffectSettings = periodEffectConfigs.ToArray()
                });
            }

            // Modifiers
            if (data.Modifiers != null && data.Modifiers.Count > 0)
            {
                var modifierSettings = new ModifierSetting[data.Modifiers.Count];
                for (var i = 0; i < data.Modifiers.Count; i++)
                {
                    var info = data.Modifiers[i];
                    modifierSettings[i] = new ModifierSetting
                    {
                        AttrSetCode = info.AttrSet,
                        AttrCode = info.Attribute,
                        Magnitude = info.Magnitude,
                        Operation = (GEOperation)info.Operation,
                        MMC = GetMmcConfig(info.Mmc)
                    };
                }
                configs.Add(new MCConfModifiers { modifierSettings = modifierSettings });
            }

            // CueOnApply
            if (data.CueOnApply is { Count: > 0 })
            {
                var cues = new ECS.GameplayCueConfig[data.CueOnApply.Count];
                for (var i = 0; i < data.CueOnApply.Count; i++)
                    cues[i] = GetCueConfig(data.CueOnApply[i]);
                configs.Add(new ConfCueOnApply { cues = cues });
            }

            // CueOnTick
            if (data.CueOnTick is { Count: > 0 })
            {
                var cues = new ECS.GameplayCueConfig[data.CueOnTick.Count];
                for (var i = 0; i < data.CueOnTick.Count; i++)
                    cues[i] = GetCueConfig(data.CueOnTick[i]);
                configs.Add(new ConfCueOnTick { cues = cues });
            }

            // CueOnAdd
            if (data.CueOnAdd is { Count: > 0 })
            {
                var cues = new ECS.GameplayCueConfig[data.CueOnAdd.Count];
                for (var i = 0; i < data.CueOnAdd.Count; i++)
                    cues[i] = GetCueConfig(data.CueOnAdd[i]);
                configs.Add(new ConfCueOnAdd { cues = cues });
            }

            // CueOnRemove
            if (data.CueOnRemove is { Count: > 0 })
            {
                var cues = new ECS.GameplayCueConfig[data.CueOnRemove.Count];
                for (var i = 0; i < data.CueOnRemove.Count; i++)
                    cues[i] = GetCueConfig(data.CueOnRemove[i]);
                configs.Add(new ConfCueOnRemove { cues = cues });
            }

            // CueOnActivate
            if (data.CueOnActivate is { Count: > 0 })
            {
                var cues = new ECS.GameplayCueConfig[data.CueOnActivate.Count];
                for (var i = 0; i < data.CueOnActivate.Count; i++)
                    cues[i] = GetCueConfig(data.CueOnActivate[i]);
                configs.Add(new ConfCueOnActivate { cues = cues });
            }

            // CueOnDeactivate
            if (data.CueOnDeactivate is { Count: > 0 })
            {
                var cues = new ECS.GameplayCueConfig[data.CueOnDeactivate.Count];
                for (var i = 0; i < data.CueOnDeactivate.Count; i++)
                    cues[i] = GetCueConfig(data.CueOnDeactivate[i]);
                configs.Add(new ConfCueOnDeactivate { cues = cues });
            }

            // GrantedAbility
            if (data.GrantedAbility.Count > 0)
            {
                var grantedAbilities = new ECS.GrantedAbility[data.GrantedAbility.Count];
                for (var i = 0; i < data.GrantedAbility.Count; i++)
                {
                    var info = data.GrantedAbility[i];
                    grantedAbilities[i] = new ECS.GrantedAbility
                    {
                        AbilityConfig = new AbilityConfig(GetAbilityConfig(info.ID)),
                        ActivationPolicy = (GrantedAbilityActivationPolicy)info.ActivationPolicy,
                        DeactivationPolicy = (GrantedAbilityDeactivationPolicy)info.DeactivationPolicy,
                        Level = info.Level,
                        RemovePolicy = (GrantedAbilityRemovePolicy)info.RemovePolicy
                    };
                }
                configs.Add(new MCConfGrantedAbility { GrantedAbilities = grantedAbilities });
            }

            // Stacking
            if (data.Stacking != null && data.Stacking.Value.StackCode != 0)
            {
                var overflowEffectConfigs = new List<GameplayEffectComponentConfig[]>();
                foreach (var effectID in data.Stacking.Value.OverflowEffects)
                    overflowEffectConfigs.Add(GetEffectConfig(effectID));
                configs.Add(new ConfStacking
                {
                    StackingCode = data.Stacking.Value.StackCode,
                    StackType = (EffectStackType)data.Stacking.Value.StackingType,
                    LimitCount = data.Stacking.Value.LimitCount,
                    EffectDurationRefreshPolicy = (EffectDurationRefreshPolicy)data.Stacking.Value.DurationRefreshPolicy,
                    EffectPeriodResetPolicy = (EffectPeriodResetPolicy)data.Stacking.Value.PeriodResetPolicy,
                    EffectExpirationPolicy = (EffectExpirationPolicy)data.Stacking.Value.ExpirationPolicy,
                    denyOverflowApplication = data.Stacking.Value.DenyOverflowApplication,
                    clearStackOnOverflow = data.Stacking.Value.ClearStackOnOverflow,
                    overflowEffects = overflowEffectConfigs.ToArray()
                });
            }

            return configs.ToArray();
        }

        /// <summary>通过 ID 获取 Ability 配置</summary>
        public AbilityComponentConfig[] GetAbilityConfig(int id)
        {
            if (_tables == null) return null;

            var data = Tables.Tbability.Get(id);
            if (data == null)
            {
                Debug.LogError($"[LubanConfigLoader] Ability_ID:{id} 不存在.");
                return new AbilityComponentConfig[0];
            }

            var configs = new List<AbilityComponentConfig>();

            // baseInfo
            configs.Add(new ConfAbilityBaseInfo { Code = id, Level = 0 });

            // cost
            if (data.Cost != 0)
                configs.Add(new ConfAbilityCost
                { CostComponentConfigs = GetEffectConfig(data.Cost) });

            // cooldown
            if (data.Cd != 0)
            {
                configs.Add(new ConfAbilityCooldown
                {
                    Cooldown = data.Cd,
                    CooldownComponentConfigs = GetEffectConfig(data.CdEffect)
                });
            }

            // assetTags
            if (data.AssetTags is { Count: > 0 })
                configs.Add(new ConfAbilityAssetTags { tags = data.AssetTags.ToArray() });

            (int[] all, int[] any, int[] none)? ParseTagRequirement(cfg.TagRequirementData? req)
            {
                if (req == null) return null;
                var r = req.Value;
                int[] all = null, any = null, none = null;
                if (r.All is { Count: > 0 }) all = r.All.Where(x => x > 0).ToArray();
                if (r.Any is { Count: > 0 }) any = r.Any.Where(x => x > 0).ToArray();
                if (r.None is { Count: > 0 }) none = r.None.Where(x => x > 0).ToArray();
                if (all == null && any == null && none == null) return null;
                return (all, any, none);
            }

            int[] PickSimpleTagSet((int[] all, int[] any, int[] none) req)
            {
                if (req.any is { Length: > 0 }) return req.any;
                if (req.all is { Length: > 0 }) return req.all;
                if (req.none is { Length: > 0 }) return req.none;
                return Array.Empty<int>();
            }

            // cancelAbilityWithTags
            {
                var cancelTags = ParseTagRequirement(data.CancelAbilityWithTags);
                if (cancelTags != null)
                    configs.Add(new ConfCancelAbilityWithTags
                    { tags = PickSimpleTagSet(cancelTags.Value) });
            }

            // blockAbilityWithTags
            {
                var blockTags = ParseTagRequirement(data.BlockAbilityWithTags);
                if (blockTags != null)
                    configs.Add(new ConfBlockAbilityWithTags
                    { tags = PickSimpleTagSet(blockTags.Value) });
            }

            // activationOwnedTags
            if (data.ActivationOwnedTags is { Count: > 0 })
                configs.Add(new ConfAbilityActivationOwnedTags
                { tags = data.ActivationOwnedTags.ToArray() });

            // activationRequiredTags
            {
                var requiredTags = ParseTagRequirement(data.ActivationRequiredTags);
                if (requiredTags != null)
                    configs.Add(new ConfAbilityActivationRequiredTags
                    { all = requiredTags.Value.all, any = requiredTags.Value.any, none = requiredTags.Value.none });
            }

            // activationBlockedTags
            {
                var blockedTags = ParseTagRequirement(data.ActivationBlockedTags);
                if (blockedTags != null)
                    configs.Add(new ConfAbilityActivationBlockedTags
                    { all = blockedTags.Value.all, any = blockedTags.Value.any, none = blockedTags.Value.none });
            }

            // abilityLogic
            {
                var logicData = data.AbilityLogic;
                var logicTypeName = logicData.GetType().Name;
                var param = CreateAbilityLogicParam(logicData);
                configs.Add(new MCConfAbilityLogic
                {
                    AbilityLogicType = logicTypeName,
                    Param = param
                });
            }

            return configs.ToArray();
        }

        /// <summary>通过 ID 获取 GameplayCue 配置</summary>
        public ECS.GameplayCueConfig GetCueConfig(int id)
        {
            if (_tables == null) return null;

            var data = Tables.TbgameplayCue.Get(id);
            if (data == null)
            {
                Debug.LogError($"[LubanConfigLoader] Cue_ID:{id} 不存在.");
                return null;
            }

            var cueTypeName = data.CueLogic.GetType().Name;
            var cueLogicType = CueHelper.GetCueType(cueTypeName);
            if (cueLogicType == null)
            {
                Debug.LogError($"[LubanConfigLoader] Cue类型未注册: {cueTypeName}.");
                return null;
            }

            var param = CreateCueParam(data.CueLogic);

            (int[] all, int[] any, int[] none) ParseTagRequirement(cfg.TagRequirementData? requirement)
            {
                if (requirement == null) return (Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>());
                var r = requirement.Value;
                var all = r.All?.Where(x => x > 0).ToArray() ?? Array.Empty<int>();
                var any = r.Any?.Where(x => x > 0).ToArray() ?? Array.Empty<int>();
                var none = r.None?.Where(x => x > 0).ToArray() ?? Array.Empty<int>();
                return (all, any, none);
            }

            var requiredTag = ParseTagRequirement(data.RequiredTag);
            var immunityTag = ParseTagRequirement(data.ImmunityTag);

            return new ECS.GameplayCueConfig
            {
                CueType = cueLogicType,
                Param = param,
                RequiredAllTags = requiredTag.all,
                RequiredAnyTags = requiredTag.any,
                RequiredNoneTags = requiredTag.none,
                ImmunityAllTags = immunityTag.all,
                ImmunityAnyTags = immunityTag.any,
                ImmunityNoneTags = immunityTag.none
            };
        }

        /// <summary>通过 ID 获取 MMC 配置</summary>
        public ECS.MMCConfig GetMmcConfig(int id)
        {
            if (_tables == null) return new ECS.MMCConfig();

            var data = Tables.Tbmmc.Get(id);
            if (data == null)
            {
                Debug.LogError($"[LubanConfigLoader] MMC_ID:{id} 不存在.");
                return new ECS.MMCConfig();
            }

            var mmcTypeName = data.MmcLogic.GetType().Name;
            var mmcLogicType = GasMmcHelper.GetMmcType(mmcTypeName);

            return new ECS.MMCConfig
            {
                MmcType = mmcLogicType ?? typeof(ECS.MMCNone),
                MmcParameter = CreateMmcParam(data.MmcLogic)
            };
        }

        /// <summary>
        /// 通过 ASC ID 获取 AbilitySystemCell 配置
        /// 返回 (tags, attrSets, abilities, level) 元组
        /// </summary>
        public (int[] tags, object[] attrSetConfigs, AbilityComponentConfig[][] abilities, int level)
            GetAscConfig(int id)
        {
            if (_tables == null) return (Array.Empty<int>(), Array.Empty<object>(), Array.Empty<AbilityComponentConfig[]>(), 0);

            var data = Tables.Tbasc.Get(id);
            if (data == null)
            {
                Debug.LogError($"[LubanConfigLoader] ASC_ID:{id} 不存在.");
                return (Array.Empty<int>(), Array.Empty<object>(), Array.Empty<AbilityComponentConfig[]>(), 0);
            }

            // 加载 Ability 配置
            var abilityIds = data.Ability;
            var abilities = new AbilityComponentConfig[abilityIds.Length][];
            for (var i = 0; i < abilityIds.Length; i++)
                abilities[i] = GetAbilityConfig(abilityIds[i]);

            // 加载 AttrSet 配置
            var attrSets = new object[data.AttrSet.Length];
            for (var i = 0; i < data.AttrSet.Length; i++)
                attrSets[i] = data.AttrSet[i];

            return (data.Tag, attrSets, abilities, data.Level);
        }

        /// <summary>加载标签层级数据</summary>
        public TagHierarchyData GetTagHierarchyData()
        {
            if (_tables == null) return new TagHierarchyData { Tags = Array.Empty<TagNode>() };

            var tags = Tables.TbgameplayTags.DataList;
            if (tags == null || tags.Count == 0)
                return new TagHierarchyData { Tags = Array.Empty<TagNode>() };

            var nodes = new TagNode[tags.Count];
            for (var i = 0; i < tags.Count; i++)
            {
                var t = tags[i];
                nodes[i] = new TagNode
                {
                    Code = t.Id,
                    Name = t.Name,
                    Children = Array.Empty<int>()
                };
            }

            return new TagHierarchyData { Tags = nodes };
        }

        // =========================================================
        // 多态类型参数创建辅助方法
        // 由生成器根据 cfg.* 命名空间中的类型自动生成
        // =========================================================

        /// <summary>创建 AbilityLogic 参数实例</summary>
        private XParam CreateAbilityLogicParam(cfg.AbilityLogicBase logicData)
        {
            if (logicData == null) return null;
            var logicTypeName = logicData.GetType().Name;
            var paramType = AbilityLogicFactory.GetAbilityLogicParamType(logicTypeName);
            if (paramType == null) return null;
            var param = Activator.CreateInstance(paramType) as XParam;
            if (param == null) return null;

            // 按实际类型赋值
            // 注意: 泛型参数通过 BeanField/BeanPolymorphicField 特性在运行时反射赋值
            // 此处预留扩展点——如果字段映射简单可直接通过特性反射填充
            // 复杂类型（Vector3 等）需在 XParam 实现类的 DecodeExcelData 中处理

            return param;
        }

        /// <summary>创建 Cue 参数实例</summary>
        private XParam CreateCueParam(cfg.GameplayCueBase cueData)
        {
            if (cueData == null) return null;
            var cueTypeName = cueData.GetType().Name;
            var paramType = CueHelper.GetCueLogicParamType(cueTypeName);
            if (paramType == null) return null;
            var param = Activator.CreateInstance(paramType) as XParam;
            if (param == null) return null;

            return param;
        }

        /// <summary>创建 MMC 参数实例</summary>
        private XParam CreateMmcParam(cfg.ModMagnitudeCalculationBase mmcData)
        {
            if (mmcData == null) return null;
            var mmcTypeName = mmcData.GetType().Name;
            var paramType = GasMmcHelper.GetMmcParamType(mmcTypeName);
            if (paramType == null) return null;
            var param = Activator.CreateInstance(paramType) as XParam;
            if (param == null) return null;

            return param;
        }

        /// <summary>创建 AbilityTask 参数实例</summary>
        private XParam CreateAbilityTaskParam(cfg.AbilityTaskBase taskData)
        {
            if (taskData == null) return null;
            var taskTypeName = taskData.GetType().Name;
            var paramType = AbilityLogicFactory.GetAbilityTaskParamType(taskTypeName);
            if (paramType == null) return null;
            var param = Activator.CreateInstance(paramType) as XParam;
            if (param == null) return null;

            return param;
        }

    }
}
