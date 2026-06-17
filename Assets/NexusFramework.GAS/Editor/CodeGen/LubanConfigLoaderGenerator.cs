using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NexusFramework.GAS.Editor
{
    /// <summary>
    /// LubanConfigLoader 生成器
    /// 扫描已加载程序集中的 cfg.Tables 类型，自动生成 Luban 表格 → GAS 运行时配置的桥接代码。
    /// 生成的文件实现了 IConfigLoader 接口，将 Luban 的强类型表格数据转换为 GAS 运行时所需的配置数组。
    /// </summary>
    public static class LubanConfigLoaderGenerator
    {
        private const string DEFAULT_OUTPUT_PATH = "Assets/DataGenerated/Luban/LubanConfigLoader.cs";

        [MenuItem("NF.GAS/Generate/LubanConfigLoader")]
        public static void Generate()
        {
            var setting = GASSettingAsset.LoadOrCreate();
            var outputPath = setting.GetLubanConfigLoaderOutputPath();
            var dir = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using var writer = new IndentedWriter(new StreamWriter(outputPath));
            WriteHeader(writer);
            WriteNamespace(writer);

            Debug.Log($"[NF.GAS] LubanConfigLoader generated at: {outputPath}");
            AssetDatabase.Refresh();
        }

        private static void WriteHeader(IndentedWriter writer)
        {
            writer.WriteLine("///////////////////////////////////");
            writer.WriteLine("//// This is a generated file. ////");
            writer.WriteLine("////     Do not modify it.     ////");
            writer.WriteLine("///////////////////////////////////");
            writer.WriteLine("");
            writer.WriteLine("using System;");
            writer.WriteLine("using System.Collections.Generic;");
            writer.WriteLine("using System.Linq;");
            writer.WriteLine("using cfg;");
            writer.WriteLine("using NexusFramework.GAS.ECS;");
            writer.WriteLine("using UnityEngine;");
            writer.WriteLine("using NexusFramework.GAS.Models;");
            writer.WriteLine("using XParam = NexusFramework.GAS.ECS.XParam;");
            writer.WriteLine("");
        }

        private static void WriteNamespace(IndentedWriter writer)
        {
            writer.WriteLine("namespace NexusFramework.GAS.Config");
            writer.WriteLine("{");
            writer.Indent++;
            {
                WriteClass(writer);
            }
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteClass(IndentedWriter writer)
        {
            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// 由 LubanConfigLoaderGenerator 自动生成。");
            writer.WriteLine("/// 将 Luban 表格数据转换为 GAS 运行时配置。");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("public class LubanConfigLoader : IConfigLoader");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("public bool Initialized { get; set; }");
                writer.WriteLine("void ICanInit.Init() => Initialized = true;");
                writer.WriteLine("void ICanInit.Deinit() => Initialized = false;");
                writer.WriteLine("");

                // Tables 静态实例
                writer.WriteLine("private cfg.Tables _tables;");
                writer.WriteLine("public cfg.Tables Tables => _tables;");
                writer.WriteLine("");

                // 初始化方法
                writer.WriteLine("/// <summary>");
                writer.WriteLine("/// 初始化并加载所有 Luban 表格。");
                writer.WriteLine("/// loader: Func<string, JSONNode>，接收表名返回 JSON 数据。");
                writer.WriteLine("/// </summary>");
                writer.WriteLine("public void LoadTables(Func<string, Luban.SimpleJSON.JSONNode> loader)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("if (_tables != null) return;");
                writer.WriteLine("_tables = new cfg.Tables(loader);");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // Editor 快捷加载
                writer.WriteLine("#if UNITY_EDITOR");
                writer.WriteLine("/// <summary>");
                writer.WriteLine("/// Editor 下从本地 JSON 文件加载表格（通过 GASSettingAsset.TableOutputPath）");
                writer.WriteLine("/// </summary>");
                writer.WriteLine("public void LoadTablesForEditor(string jsonDir)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("_tables = new cfg.Tables(file =>");
                writer.WriteLine("    Luban.SimpleJSON.JSON.Parse(System.IO.File.ReadAllText($\"{jsonDir}/{file}.json\")));");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("#endif");
                writer.WriteLine("");

                // Init 方法（完整初始化）
                writer.WriteLine("/// <summary>");
                writer.WriteLine("/// 初始化表格 + 注册配置查询委托。");
                writer.WriteLine("/// </summary>");
                writer.WriteLine("public void Init(Func<string, Luban.SimpleJSON.JSONNode> loader)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("LoadTables(loader);");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // ── IConfigLoader 接口实现（兼容 JSON 路径）──
                writer.WriteLine("// =========================================================");
                writer.WriteLine("// IConfigLoader 接口实现");
                writer.WriteLine("// 注意：LubanConfigLoader 不使用 LoadRaw + Parse 模式，");
                writer.WriteLine("// 请使用 GetEffectConfig(id) / GetAbilityConfig(id) 等强类型方法。");
                writer.WriteLine("// =========================================================");
                writer.WriteLine("");

                writer.WriteLine("public string LoadRaw(string fullPath) => null;");
                writer.WriteLine("");

                writer.WriteLine("public GameplayEffectComponentConfig[] ParseGameplayEffect(string json) => null;");
                writer.WriteLine("public AbilityComponentConfig[] ParseAbility(string json) => null;");
                writer.WriteLine("public GameplayCueConfig ParseGameplayCue(string json) => default;");
                writer.WriteLine("public MMCConfig ParseMmc(string json) => default;");
                writer.WriteLine("public TagHierarchyData ParseTagHierarchy(string json) => default;");
                writer.WriteLine("");

                // ── ASC 配置查询（IConfigLoader 接口实现）──
                WriteIConfigLoaderAscMethods(writer);
                writer.WriteLine("");
                
                WriteRegisterAllConfigToMethod(writer);
                writer.WriteLine("");

                // ── 强类型查询方法 ──
                WriteEffectConfigMethod(writer);
                WriteAbilityConfigMethod(writer);
                WriteCueConfigMethod(writer);
                WriteMmcConfigMethod(writer);
                WriteAscConfigMethod(writer);
                WriteTagHierarchyMethod(writer);
                // WriteTimelineAbilityMethod(writer); // TODO: 需要运行时支持 TimelineAbility 后再实现
                WriteUtilityMethods(writer);
            }
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteEffectConfigMethod(IndentedWriter writer)
        {
            writer.WriteLine("/// <summary>通过 ID 获取 GameplayEffect 配置</summary>");
            writer.WriteLine("public GameplayEffectComponentConfig[] GetEffectConfig(int id)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("if (_tables == null) return null;");
                writer.WriteLine("");
                writer.WriteLine("var data = Tables.TbgameplayEffect.Get(id);");
                writer.WriteLine("if (data == null)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("Debug.LogError($\"[LubanConfigLoader] GameplayEffect_ID:{id} 不存在.\");");
                writer.WriteLine("return null;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");
                writer.WriteLine("var configs = new List<GameplayEffectComponentConfig>();");
                writer.WriteLine("");

                // TagRequirement 解析局部函数
                writer.WriteLine("// 局部辅助: TagRequirement → (all, any, none)");
                writer.WriteLine("(int[] all, int[] any, int[] none)? ParseTagRequirement(cfg.TagRequirementData? req)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("if (req == null) return null;");
                writer.WriteLine("var r = req.Value;");
                writer.WriteLine("int[] all = null, any = null, none = null;");
                writer.WriteLine("if (r.All is { Count: > 0 }) all = r.All.Where(x => x > 0).ToArray();");
                writer.WriteLine("if (r.Any is { Count: > 0 }) any = r.Any.Where(x => x > 0).ToArray();");
                writer.WriteLine("if (r.None is { Count: > 0 }) none = r.None.Where(x => x > 0).ToArray();");
                writer.WriteLine("if (all == null && any == null && none == null) return null;");
                writer.WriteLine("return (all, any, none);");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // AssetTags
                writer.WriteLine("// AssetTags");
                writer.WriteLine("if (data.AssetTags is { Count: > 0 })");
                writer.WriteLine("    configs.Add(new ConfAssetTags { tags = data.AssetTags.ToArray() });");
                writer.WriteLine("");

                // GrantedTags
                writer.WriteLine("// GrantedTags");
                writer.WriteLine("if (data.GrantedTags is { Count: > 0 })");
                writer.WriteLine("    configs.Add(new ConfEffectGrantedTags { tags = data.GrantedTags.ToArray() });");
                writer.WriteLine("");

                // ApplicationRequiredTags
                writer.WriteLine("// ApplicationRequiredTags");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var result = ParseTagRequirement(data.ApplicationRequiredTags);");
                writer.WriteLine("if (result != null)");
                writer.WriteLine("    configs.Add(new ConfApplicationRequiredTags");
                writer.WriteLine("    { all = result.Value.all, any = result.Value.any, none = result.Value.none });");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // OngoingRequiredTags
                writer.WriteLine("// OngoingRequiredTags");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var result = ParseTagRequirement(data.OngoingRequiredTags);");
                writer.WriteLine("if (result != null)");
                writer.WriteLine("    configs.Add(new ConfOngoingRequiredTags");
                writer.WriteLine("    { all = result.Value.all, any = result.Value.any, none = result.Value.none });");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // RemoveEffectWithTags
                writer.WriteLine("// RemoveGameplayEffectsWithTags");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var result = ParseTagRequirement(data.RemoveGameplayEffectsWithTags);");
                writer.WriteLine("if (result != null)");
                writer.WriteLine("    configs.Add(new ConfRemoveEffectWithTags");
                writer.WriteLine("    { all = result.Value.all, any = result.Value.any, none = result.Value.none });");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // ImmunityTags
                writer.WriteLine("// ImmunityTags");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var result = ParseTagRequirement(data.ImmunityTags);");
                writer.WriteLine("if (result != null)");
                writer.WriteLine("    configs.Add(new ConfEffectImmunityTags");
                writer.WriteLine("    { all = result.Value.all, any = result.Value.any, none = result.Value.none });");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // Duration
                writer.WriteLine("// Duration");
                writer.WriteLine("if (data.Duration != null && data.Duration.Value.Time != 0)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("configs.Add(new ConfDuration");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("duration = data.Duration.Value.Time,");
                writer.WriteLine("timeUnit = (TimeUnit)data.Duration.Value.TimeUnit,");
                writer.WriteLine("ResetStartTimeWhenActivated = data.Duration.Value.ResetStartTimeWhenActivated");
                writer.Indent--;
                writer.WriteLine("});");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // Period
                writer.WriteLine("// Period");
                writer.WriteLine("if (data.Period is { Time: > 0 })");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var periodEffectConfigs = new List<GameplayEffectComponentConfig[]>();");
                writer.WriteLine("foreach (var effectID in data.Period.Value.Effects)");
                writer.WriteLine("    periodEffectConfigs.Add(GetEffectConfig(effectID));");
                writer.WriteLine("configs.Add(new ConfPeriod");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("Period = data.Period.Value.Time,");
                writer.WriteLine("ResetTimeCountWhenDeactivated = data.Period.Value.FirstTrigger,");
                writer.WriteLine("GameplayEffectSettings = periodEffectConfigs.ToArray()");
                writer.Indent--;
                writer.WriteLine("});");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // Modifiers
                writer.WriteLine("// Modifiers");
                writer.WriteLine("if (data.Modifiers != null && data.Modifiers.Count > 0)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var modifierSettings = new ModifierSetting[data.Modifiers.Count];");
                writer.WriteLine("for (var i = 0; i < data.Modifiers.Count; i++)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var info = data.Modifiers[i];");
                writer.WriteLine("modifierSettings[i] = new ModifierSetting");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("AttrSetCode = info.AttrSet,");
                writer.WriteLine("AttrCode = info.Attribute,");
                writer.WriteLine("Magnitude = info.Magnitude,");
                writer.WriteLine("Operation = (GEOperation)info.Operation,");
                writer.WriteLine("MMC = GetMmcConfig(info.Mmc)");
                writer.Indent--;
                writer.WriteLine("};");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("configs.Add(new MCConfModifiers { modifierSettings = modifierSettings });");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // Cue configs
                var cueFields = new[] {
                    ("CueOnApply", "ConfCueOnApply"),
                    ("CueOnTick", "ConfCueOnTick"),
                    ("CueOnAdd", "ConfCueOnAdd"),
                    ("CueOnRemove", "ConfCueOnRemove"),
                    ("CueOnActivate", "ConfCueOnActivate"),
                    ("CueOnDeactivate", "ConfCueOnDeactivate")
                };
                foreach (var (field, confType) in cueFields)
                {
                    writer.WriteLine($"// {field}");
                    writer.WriteLine($"if (data.{field} is {{ Count: > 0 }})");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine($"var cues = new ECS.GameplayCueConfig[data.{field}.Count];");
                    writer.WriteLine($"for (var i = 0; i < data.{field}.Count; i++)");
                    writer.WriteLine($"    cues[i] = GetCueConfig(data.{field}[i]);");
                    writer.WriteLine($"configs.Add(new {confType} {{ cues = cues }});");
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.WriteLine("");
                }

                // GrantedAbility
                writer.WriteLine("// GrantedAbility");
                writer.WriteLine("if (data.GrantedAbility.Count > 0)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var grantedAbilities = new ECS.GrantedAbility[data.GrantedAbility.Count];");
                writer.WriteLine("for (var i = 0; i < data.GrantedAbility.Count; i++)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var info = data.GrantedAbility[i];");
                writer.WriteLine("grantedAbilities[i] = new ECS.GrantedAbility");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("AbilityConfig = new AbilityConfig(GetAbilityConfig(info.ID)),");
                writer.WriteLine("ActivationPolicy = (GrantedAbilityActivationPolicy)info.ActivationPolicy,");
                writer.WriteLine("DeactivationPolicy = (GrantedAbilityDeactivationPolicy)info.DeactivationPolicy,");
                writer.WriteLine("Level = info.Level,");
                writer.WriteLine("RemovePolicy = (GrantedAbilityRemovePolicy)info.RemovePolicy");
                writer.Indent--;
                writer.WriteLine("};");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("configs.Add(new MCConfGrantedAbility { GrantedAbilities = grantedAbilities });");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // Stacking
                writer.WriteLine("// Stacking");
                writer.WriteLine("if (data.Stacking != null && data.Stacking.Value.StackCode != 0)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var overflowEffectConfigs = new List<GameplayEffectComponentConfig[]>();");
                writer.WriteLine("foreach (var effectID in data.Stacking.Value.OverflowEffects)");
                writer.WriteLine("    overflowEffectConfigs.Add(GetEffectConfig(effectID));");
                writer.WriteLine("configs.Add(new ConfStacking");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("StackingCode = data.Stacking.Value.StackCode,");
                writer.WriteLine("StackType = (EffectStackType)data.Stacking.Value.StackingType,");
                writer.WriteLine("LimitCount = data.Stacking.Value.LimitCount,");
                writer.WriteLine("EffectDurationRefreshPolicy = (EffectDurationRefreshPolicy)data.Stacking.Value.DurationRefreshPolicy,");
                writer.WriteLine("EffectPeriodResetPolicy = (EffectPeriodResetPolicy)data.Stacking.Value.PeriodResetPolicy,");
                writer.WriteLine("EffectExpirationPolicy = (EffectExpirationPolicy)data.Stacking.Value.ExpirationPolicy,");
                writer.WriteLine("denyOverflowApplication = data.Stacking.Value.DenyOverflowApplication,");
                writer.WriteLine("clearStackOnOverflow = data.Stacking.Value.ClearStackOnOverflow,");
                writer.WriteLine("overflowEffects = overflowEffectConfigs.ToArray()");
                writer.Indent--;
                writer.WriteLine("});");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                writer.WriteLine("return configs.ToArray();");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
        }

        private static void WriteAbilityConfigMethod(IndentedWriter writer)
        {
            writer.WriteLine("/// <summary>通过 ID 获取 Ability 配置</summary>");
            writer.WriteLine("public AbilityComponentConfig[] GetAbilityConfig(int id)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("if (_tables == null) return null;");
                writer.WriteLine("");
                writer.WriteLine("var data = Tables.Tbability.Get(id);");
                writer.WriteLine("if (data == null)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("Debug.LogError($\"[LubanConfigLoader] Ability_ID:{id} 不存在.\");");
                writer.WriteLine("return new AbilityComponentConfig[0];");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");
                writer.WriteLine("var configs = new List<AbilityComponentConfig>();");
                writer.WriteLine("");

                // BaseInfo
                writer.WriteLine("// baseInfo");
                writer.WriteLine("configs.Add(new ConfAbilityBaseInfo { Code = id, Level = 0 });");
                writer.WriteLine("");

                // Cost
                writer.WriteLine("// cost");
                writer.WriteLine("if (data.Cost != 0)");
                writer.WriteLine("    configs.Add(new ConfAbilityCost");
                writer.WriteLine("    { CostComponentConfigs = GetEffectConfig(data.Cost) });");
                writer.WriteLine("");

                // Cooldown
                writer.WriteLine("// cooldown");
                writer.WriteLine("if (data.Cd != 0)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("configs.Add(new ConfAbilityCooldown");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("Cooldown = data.Cd,");
                writer.WriteLine("CooldownComponentConfigs = GetEffectConfig(data.CdEffect)");
                writer.Indent--;
                writer.WriteLine("});");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // AssetTags
                writer.WriteLine("// assetTags");
                writer.WriteLine("if (data.AssetTags is { Count: > 0 })");
                writer.WriteLine("    configs.Add(new ConfAbilityAssetTags { tags = data.AssetTags.ToArray() });");
                writer.WriteLine("");

                // TagRequirement 解析
                writer.WriteLine("(int[] all, int[] any, int[] none)? ParseTagRequirement(cfg.TagRequirementData? req)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("if (req == null) return null;");
                writer.WriteLine("var r = req.Value;");
                writer.WriteLine("int[] all = null, any = null, none = null;");
                writer.WriteLine("if (r.All is { Count: > 0 }) all = r.All.Where(x => x > 0).ToArray();");
                writer.WriteLine("if (r.Any is { Count: > 0 }) any = r.Any.Where(x => x > 0).ToArray();");
                writer.WriteLine("if (r.None is { Count: > 0 }) none = r.None.Where(x => x > 0).ToArray();");
                writer.WriteLine("if (all == null && any == null && none == null) return null;");
                writer.WriteLine("return (all, any, none);");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                writer.WriteLine("int[] PickSimpleTagSet((int[] all, int[] any, int[] none) req)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("if (req.any is { Length: > 0 }) return req.any;");
                writer.WriteLine("if (req.all is { Length: > 0 }) return req.all;");
                writer.WriteLine("if (req.none is { Length: > 0 }) return req.none;");
                writer.WriteLine("return Array.Empty<int>();");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // Cancel/Block ability with tags
                writer.WriteLine("// cancelAbilityWithTags");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var cancelTags = ParseTagRequirement(data.CancelAbilityWithTags);");
                writer.WriteLine("if (cancelTags != null)");
                writer.WriteLine("    configs.Add(new ConfCancelAbilityWithTags");
                writer.WriteLine("    { tags = PickSimpleTagSet(cancelTags.Value) });");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                writer.WriteLine("// blockAbilityWithTags");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var blockTags = ParseTagRequirement(data.BlockAbilityWithTags);");
                writer.WriteLine("if (blockTags != null)");
                writer.WriteLine("    configs.Add(new ConfBlockAbilityWithTags");
                writer.WriteLine("    { tags = PickSimpleTagSet(blockTags.Value) });");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // Activation tags
                writer.WriteLine("// activationOwnedTags");
                writer.WriteLine("if (data.ActivationOwnedTags is { Count: > 0 })");
                writer.WriteLine("    configs.Add(new ConfAbilityActivationOwnedTags");
                writer.WriteLine("    { tags = data.ActivationOwnedTags.ToArray() });");
                writer.WriteLine("");

                writer.WriteLine("// activationRequiredTags");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var requiredTags = ParseTagRequirement(data.ActivationRequiredTags);");
                writer.WriteLine("if (requiredTags != null)");
                writer.WriteLine("    configs.Add(new ConfAbilityActivationRequiredTags");
                writer.WriteLine("    { all = requiredTags.Value.all, any = requiredTags.Value.any, none = requiredTags.Value.none });");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                writer.WriteLine("// activationBlockedTags");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var blockedTags = ParseTagRequirement(data.ActivationBlockedTags);");
                writer.WriteLine("if (blockedTags != null)");
                writer.WriteLine("    configs.Add(new ConfAbilityActivationBlockedTags");
                writer.WriteLine("    { all = blockedTags.Value.all, any = blockedTags.Value.any, none = blockedTags.Value.none });");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // AbilityLogic (polymorphic dispatch via GetAbilityLogicParam)
                writer.WriteLine("// abilityLogic");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var logicData = data.AbilityLogic;");
                writer.WriteLine("var logicTypeName = logicData.GetType().Name;");
                writer.WriteLine("var param = CreateAbilityLogicParam(logicData);");
                writer.WriteLine("configs.Add(new MCConfAbilityLogic");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("AbilityLogicType = logicTypeName,");
                writer.WriteLine("Param = param");
                writer.Indent--;
                writer.WriteLine("});");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                writer.WriteLine("return configs.ToArray();");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
        }

        private static void WriteCueConfigMethod(IndentedWriter writer)
        {
            writer.WriteLine("/// <summary>通过 ID 获取 GameplayCue 配置</summary>");
            writer.WriteLine("public ECS.GameplayCueConfig GetCueConfig(int id)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("if (_tables == null) return null;");
                writer.WriteLine("");
                writer.WriteLine("var data = Tables.TbgameplayCue.Get(id);");
                writer.WriteLine("if (data == null)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("Debug.LogError($\"[LubanConfigLoader] Cue_ID:{id} 不存在.\");");
                writer.WriteLine("return null;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                writer.WriteLine("var cueTypeName = data.CueLogic.GetType().Name;");
                writer.WriteLine("var cueLogicType = CueHelper.GetCueType(cueTypeName);");
                writer.WriteLine("if (cueLogicType == null)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("Debug.LogError($\"[LubanConfigLoader] Cue类型未注册: {cueTypeName}.\");");
                writer.WriteLine("return null;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                writer.WriteLine("var param = CreateCueParam(data.CueLogic);");
                writer.WriteLine("");

                writer.WriteLine("(int[] all, int[] any, int[] none) ParseTagRequirement(cfg.TagRequirementData? requirement)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("if (requirement == null) return (Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>());");
                writer.WriteLine("var r = requirement.Value;");
                writer.WriteLine("var all = r.All?.Where(x => x > 0).ToArray() ?? Array.Empty<int>();");
                writer.WriteLine("var any = r.Any?.Where(x => x > 0).ToArray() ?? Array.Empty<int>();");
                writer.WriteLine("var none = r.None?.Where(x => x > 0).ToArray() ?? Array.Empty<int>();");
                writer.WriteLine("return (all, any, none);");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                writer.WriteLine("var requiredTag = ParseTagRequirement(data.RequiredTag);");
                writer.WriteLine("var immunityTag = ParseTagRequirement(data.ImmunityTag);");
                writer.WriteLine("");

                writer.WriteLine("return new ECS.GameplayCueConfig");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("CueType = cueLogicType,");
                writer.WriteLine("Param = param,");
                writer.WriteLine("RequiredAllTags = requiredTag.all,");
                writer.WriteLine("RequiredAnyTags = requiredTag.any,");
                writer.WriteLine("RequiredNoneTags = requiredTag.none,");
                writer.WriteLine("ImmunityAllTags = immunityTag.all,");
                writer.WriteLine("ImmunityAnyTags = immunityTag.any,");
                writer.WriteLine("ImmunityNoneTags = immunityTag.none");
                writer.Indent--;
                writer.WriteLine("};");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
        }

        private static void WriteMmcConfigMethod(IndentedWriter writer)
        {
            writer.WriteLine("/// <summary>通过 ID 获取 MMC 配置</summary>");
            writer.WriteLine("public ECS.MMCConfig GetMmcConfig(int id)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("if (_tables == null) return new ECS.MMCConfig();");
                writer.WriteLine("");
                writer.WriteLine("var data = Tables.Tbmmc.Get(id);");
                writer.WriteLine("if (data == null)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("Debug.LogError($\"[LubanConfigLoader] MMC_ID:{id} 不存在.\");");
                writer.WriteLine("return new ECS.MMCConfig();");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                writer.WriteLine("var mmcTypeName = data.MmcLogic.GetType().Name;");
                writer.WriteLine("var mmcLogicType = GasMmcHelper.GetMmcType(mmcTypeName);");
                writer.WriteLine("");

                writer.WriteLine("return new ECS.MMCConfig");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("MmcType = mmcLogicType ?? typeof(ECS.MMCNone),");
                writer.WriteLine("MmcParameter = CreateMmcParam(data.MmcLogic)");
                writer.Indent--;
                writer.WriteLine("};");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
        }

        private static void WriteAscConfigMethod(IndentedWriter writer)
        {
            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// 通过 ASC ID 获取 AbilitySystemCell 配置");
            writer.WriteLine("/// 返回 (tags, attrSets, abilities, level) 元组");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("public (int[] tags, object[] attrSetConfigs, AbilityComponentConfig[][] abilities, int level)");
            writer.WriteLine("    GetAscConfig(int id)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("if (_tables == null) return (Array.Empty<int>(), Array.Empty<object>(), Array.Empty<AbilityComponentConfig[]>(), 0);");
                writer.WriteLine("");
                writer.WriteLine("var data = Tables.Tbasc.Get(id);");
                writer.WriteLine("if (data == null)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("Debug.LogError($\"[LubanConfigLoader] ASC_ID:{id} 不存在.\");");
                writer.WriteLine("return (Array.Empty<int>(), Array.Empty<object>(), Array.Empty<AbilityComponentConfig[]>(), 0);");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                writer.WriteLine("// 加载 Ability 配置");
                writer.WriteLine("var abilityIds = data.Ability;");
                writer.WriteLine("var abilities = new AbilityComponentConfig[abilityIds.Length][];");
                writer.WriteLine("for (var i = 0; i < abilityIds.Length; i++)");
                writer.WriteLine("    abilities[i] = GetAbilityConfig(abilityIds[i]);");
                writer.WriteLine("");

                writer.WriteLine("// 加载 AttrSet 配置");
                writer.WriteLine("var attrSets = new object[data.AttrSet.Length];");
                writer.WriteLine("for (var i = 0; i < data.AttrSet.Length; i++)");
                writer.WriteLine("    attrSets[i] = data.AttrSet[i];");
                writer.WriteLine("");

                writer.WriteLine("return (data.Tag, attrSets, abilities, data.Level);");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
        }

        private static void WriteTagHierarchyMethod(IndentedWriter writer)
        {
            writer.WriteLine("/// <summary>加载标签层级数据</summary>");
            writer.WriteLine("public TagHierarchyData GetTagHierarchyData()");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("if (_tables == null) return new TagHierarchyData { Tags = Array.Empty<TagNode>() };");
                writer.WriteLine("");
                writer.WriteLine("var tags = Tables.TbgameplayTags.DataList;");
                writer.WriteLine("if (tags == null || tags.Count == 0)");
                writer.WriteLine("    return new TagHierarchyData { Tags = Array.Empty<TagNode>() };");
                writer.WriteLine("");

                writer.WriteLine("var nodes = new TagNode[tags.Count];");
                writer.WriteLine("for (var i = 0; i < tags.Count; i++)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var t = tags[i];");
                writer.WriteLine("nodes[i] = new TagNode");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("Code = t.Id,");
                writer.WriteLine("Name = t.Name,");
                writer.WriteLine("Children = Array.Empty<int>()");
                writer.Indent--;
                writer.WriteLine("};");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                writer.WriteLine("return new TagHierarchyData { Tags = nodes };");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
        }

        private static void WriteTimelineAbilityMethod(IndentedWriter writer)
        {
            writer.WriteLine("/// <summary>通过 ID 获取 TimelineAbility 参数（暂未实现）</summary>");
            writer.WriteLine("public XParamTimeline GetTimelineAbilityParam(int id)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("Debug.LogWarning($\"[LubanConfigLoader] TimelineAbility not implemented yet. ID:{id}\");");
                writer.WriteLine("return default;");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
        }


        private static void WriteIConfigLoaderAscMethods(IndentedWriter writer)
        {
            writer.WriteLine("AscConfigData? IConfigLoader.GetAscConfig(int ascId)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("if (_tables == null) return null;");
                writer.WriteLine("var data = _tables.Tbasc.GetOrDefault(ascId);");
                writer.WriteLine("if (data == null) return null;");
                writer.WriteLine("");
                writer.WriteLine("return new AscConfigData");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("Level = data.Level,");
                writer.WriteLine("Tags = data.Tag,");
                writer.WriteLine("AttrSetIds = data.AttrSet,");
                writer.WriteLine("AbilityIds = data.Ability");
                writer.Indent--;
                writer.WriteLine("};");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");

            writer.WriteLine("AttrSetDef? IConfigLoader.GetAttrSetDef(int attrSetId)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("if (_tables == null) return null;");
                writer.WriteLine("var data = _tables.TbattributeSet.GetOrDefault(attrSetId);");
                writer.WriteLine("if (data == null) return null;");
                writer.WriteLine("");
                writer.WriteLine("var attrs = new AttrInitDef[data.Attribute.Length];");
                writer.WriteLine("for (int i = 0; i < data.Attribute.Length; i++)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var src = data.Attribute[i];");
                writer.WriteLine("attrs[i] = new AttrInitDef");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("Code = src.ID,");
                writer.WriteLine("InitValue = src.InitValue,");
                writer.WriteLine("MinValue = src.MinValue,");
                writer.WriteLine("MaxValue = src.MaxValue,");
                writer.WriteLine("UseMinValue = src.UseMinValue,");
                writer.WriteLine("UseMaxValue = src.UseMaxValue");
                writer.Indent--;
                writer.WriteLine("};");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");
                writer.WriteLine("return new AttrSetDef");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("AttrSetCode = attrSetId,");
                writer.WriteLine("Attributes = attrs");
                writer.Indent--;
                writer.WriteLine("};");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
        }
        
        private static void WriteRegisterAllConfigToMethod(IndentedWriter writer)
        {
            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// 将 _tables 中所有 ASC/AttrSet 配置批量注册到 ConfigModel，支持追加覆盖。");
            writer.WriteLine("/// 多次调用、多数据源调用均可安全叠加。");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("public void RegisterAllConfigTo(ConfigModel configModel)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("if (_tables == null) return;");
                writer.WriteLine("");

                writer.WriteLine("// 注册所有 ASC 配置");
                writer.WriteLine("foreach (var asc in _tables.Tbasc.DataList)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("configModel.RegisterAscConfig(asc.ID, new AscConfigData");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("Level = asc.Level,");
                writer.WriteLine("Tags = asc.Tag,");
                writer.WriteLine("AttrSetIds = asc.AttrSet,");
                writer.WriteLine("AbilityIds = asc.Ability");
                writer.Indent--;
                writer.WriteLine("});");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                writer.WriteLine("// 注册所有属性集定义");
                writer.WriteLine("foreach (var attrSet in _tables.TbattributeSet.DataList)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var attrs = new AttrInitDef[attrSet.Attribute.Length];");
                writer.WriteLine("for (int i = 0; i < attrSet.Attribute.Length; i++)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var src = attrSet.Attribute[i];");
                writer.WriteLine("attrs[i] = new AttrInitDef");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("Code = src.ID,");
                writer.WriteLine("InitValue = src.InitValue,");
                writer.WriteLine("MinValue = src.MinValue,");
                writer.WriteLine("MaxValue = src.MaxValue,");
                writer.WriteLine("UseMinValue = src.UseMinValue,");
                writer.WriteLine("UseMaxValue = src.UseMaxValue");
                writer.Indent--;
                writer.WriteLine("};");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("configModel.RegisterAttrSetDef(attrSet.ID, new AttrSetDef");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("AttrSetCode = attrSet.ID,");
                writer.WriteLine("Attributes = attrs");
                writer.Indent--;
                writer.WriteLine("});");
                writer.Indent--;
                writer.WriteLine("}");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
        }

        private static void WriteUtilityMethods(IndentedWriter writer)
        {
            writer.WriteLine("// =========================================================");
            writer.WriteLine("// 多态类型参数创建辅助方法");
            writer.WriteLine("// 由生成器根据 cfg.* 命名空间中的类型自动生成");
            writer.WriteLine("// =========================================================");
            writer.WriteLine("");

            // CreateAbilityLogicParam
            writer.WriteLine("/// <summary>创建 AbilityLogic 参数实例</summary>");
            writer.WriteLine("private XParam CreateAbilityLogicParam(cfg.AbilityLogicBase logicData)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("if (logicData == null) return null;");
                writer.WriteLine("var logicTypeName = logicData.GetType().Name;");
                writer.WriteLine("var paramType = AbilityLogicFactory.GetAbilityLogicParamType(logicTypeName);");
                writer.WriteLine("if (paramType == null) return null;");
                writer.WriteLine("var param = Activator.CreateInstance(paramType) as XParam;");
                writer.WriteLine("if (param == null) return null;");
                writer.WriteLine("");

                writer.WriteLine("// 按实际类型赋值");
                writer.WriteLine("// 注意: 泛型参数通过 BeanField/BeanPolymorphicField 特性在运行时反射赋值");
                writer.WriteLine("// 此处预留扩展点——如果字段映射简单可直接通过特性反射填充");
                writer.WriteLine("// 复杂类型（Vector3 等）需在 XParam 实现类的 DecodeExcelData 中处理");
                writer.WriteLine("");

                writer.WriteLine("return param;");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");

            // CreateCueParam
            writer.WriteLine("/// <summary>创建 Cue 参数实例</summary>");
            writer.WriteLine("private XParam CreateCueParam(cfg.GameplayCueBase cueData)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("if (cueData == null) return null;");
                writer.WriteLine("var cueTypeName = cueData.GetType().Name;");
                writer.WriteLine("var paramType = CueHelper.GetCueLogicParamType(cueTypeName);");
                writer.WriteLine("if (paramType == null) return null;");
                writer.WriteLine("var param = Activator.CreateInstance(paramType) as XParam;");
                writer.WriteLine("if (param == null) return null;");
                writer.WriteLine("");

                writer.WriteLine("return param;");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");

            // CreateMmcParam
            writer.WriteLine("/// <summary>创建 MMC 参数实例</summary>");
            writer.WriteLine("private XParam CreateMmcParam(cfg.ModMagnitudeCalculationBase mmcData)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("if (mmcData == null) return null;");
                writer.WriteLine("var mmcTypeName = mmcData.GetType().Name;");
                writer.WriteLine("var paramType = GasMmcHelper.GetMmcParamType(mmcTypeName);");
                writer.WriteLine("if (paramType == null) return null;");
                writer.WriteLine("var param = Activator.CreateInstance(paramType) as XParam;");
                writer.WriteLine("if (param == null) return null;");
                writer.WriteLine("");

                writer.WriteLine("return param;");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");

            // CreateAbilityTaskParam
            writer.WriteLine("/// <summary>创建 AbilityTask 参数实例</summary>");
            writer.WriteLine("private XParam CreateAbilityTaskParam(cfg.AbilityTaskBase taskData)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("if (taskData == null) return null;");
                writer.WriteLine("var taskTypeName = taskData.GetType().Name;");
                writer.WriteLine("var paramType = AbilityLogicFactory.GetAbilityTaskParamType(taskTypeName);");
                writer.WriteLine("if (paramType == null) return null;");
                writer.WriteLine("var param = Activator.CreateInstance(paramType) as XParam;");
                writer.WriteLine("if (param == null) return null;");
                writer.WriteLine("");

                writer.WriteLine("return param;");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
        }
    }
}
