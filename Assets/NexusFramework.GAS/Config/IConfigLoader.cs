using NexusFramework;
using NexusFramework.GAS.ECS;

namespace NexusFramework.GAS.Config
{
    /// <summary>
    /// 配置加载器——纯 I/O + 解析工具，不持有缓存。
    /// 缓存由 ConfigModel 管理。
    /// </summary>
    public interface IConfigLoader : IUtility
    {
        /// <summary>从完整路径读取原始 JSON 文本</summary>
        string LoadRaw(string fullPath);

        /// <summary>解析 GE 配置 JSON → 组件配置数组</summary>
        GameplayEffectComponentConfig[] ParseGameplayEffect(string json);

        /// <summary>解析 Ability 配置 JSON → 组件配置数组</summary>
        AbilityComponentConfig[] ParseAbility(string json);

        /// <summary>解析 Cue 配置 JSON</summary>
        GameplayCueConfig ParseGameplayCue(string json);

        /// <summary>解析 MMC 配置 JSON</summary>
        MMCConfig ParseMmc(string json);

        /// <summary>解析标签层级 JSON</summary>
        TagHierarchyData ParseTagHierarchy(string json);
    }

    public struct GameplayCueConfig
    {
        public string CueType;
        public XParam Param;
        public int[] RequiredTags;
        public int[] ImmunityTags;
    }

    public struct MMCConfig
    {
        public string MmcType;
        public XParam Param;
    }

    public struct TagHierarchyData
    {
        public TagNode[] Tags;
    }

    [System.Serializable]
    public struct TagNode
    {
        public int Code;
        public string Name;
        public int[] Children;
    }
}
