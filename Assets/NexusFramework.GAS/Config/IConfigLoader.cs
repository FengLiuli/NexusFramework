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

        // ── ASC 运行时创建支持 ──────────────────────────────

        /// <summary>获取 ASC 配置（按 ASC ID）</summary>
        AscConfigData? GetAscConfig(int ascId);

        /// <summary>获取属性集定义（按属性集 ID，含属性代码、初始值、Clamp 范围）</summary>
        AttrSetDef? GetAttrSetDef(int attrSetId);
    }

    // ── ASC 运行时创建 数据模型 ──────────────────────────────

    /// <summary>ASC 配置（从 cfg.exgas.asc 表映射）</summary>
    public struct AscConfigData
    {
        public int Level;
        public int[] Tags;
        public int[] AttrSetIds;
        public int[] AbilityIds;
    }

    /// <summary>属性集定义（从 cfg.exgas.attributeSet 表映射）</summary>
    public struct AttrSetDef
    {
        public int AttrSetCode;
        public AttrInitDef[] Attributes;
    }

    /// <summary>单条属性的初始值定义（从 cfg.AttributeInSet 映射）</summary>
    public struct AttrInitDef
    {
        public int Code;
        public float InitValue;
        public float MinValue;
        public float MaxValue;
        public bool UseMinValue;
        public bool UseMaxValue;
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
