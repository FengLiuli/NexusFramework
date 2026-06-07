using System.Collections.Generic;
using NexusFramework;
using NexusFramework.GAS.ECS;

namespace NexusFramework.GAS.Models
{
    /// <summary>
    /// 配置数据缓存——持有所有已加载的配置字典。
    /// 通过 RegisterEffect 等方法填充，通过 GetGameplayEffectConfig 等方法查询。
    /// </summary>
    public class ConfigModel : AbstractModel
    {
        private readonly Dictionary<int, GameplayEffectComponentConfig[]> _effects = new();
        private readonly Dictionary<int, AbilityComponentConfig[]> _abilities = new();
        private Config.GameplayCueConfig[] _cues = System.Array.Empty<Config.GameplayCueConfig>();
        private Config.MMCConfig[] _mmcs = System.Array.Empty<Config.MMCConfig>();
        private Config.TagHierarchyData _tagHierarchy;

        protected override void OnInit() { }

        protected override void OnDeinit()
        {
            _effects.Clear();
            _abilities.Clear();
            _cues = System.Array.Empty<Config.GameplayCueConfig>();
            _mmcs = System.Array.Empty<Config.MMCConfig>();
            _tagHierarchy = default;
        }

        // ── 注册 ──

        public void RegisterEffect(int id, GameplayEffectComponentConfig[] configs)
            => _effects[id] = configs;

        public void RegisterAbility(int id, AbilityComponentConfig[] configs)
            => _abilities[id] = configs;

        public void RegisterCues(Config.GameplayCueConfig[] configs)
            => _cues = configs;

        public void RegisterMmcs(Config.MMCConfig[] configs)
            => _mmcs = configs;

        public void RegisterTagHierarchy(Config.TagHierarchyData data)
            => _tagHierarchy = data;

        // ── 查询 ──

        public GameplayEffectComponentConfig[] GetGameplayEffectConfig(int id)
            => _effects.TryGetValue(id, out var c) ? c : null;

        public AbilityComponentConfig[] GetAbilityConfig(int id)
            => _abilities.TryGetValue(id, out var c) ? c : null;

        public Config.GameplayCueConfig GetGameplayCueConfig(int id)
            => id >= 0 && id < _cues.Length ? _cues[id] : default;

        public Config.MMCConfig GetMmcConfig(int id)
            => id >= 0 && id < _mmcs.Length ? _mmcs[id] : default;

        public Config.TagHierarchyData GetTagHierarchy()
            => _tagHierarchy;

        // ── 从 IConfigLoader 加载 ──

        /// <summary>加载单条 GE 配置</summary>
        public void LoadEffect(Config.IConfigLoader loader, int id, string fullPath)
            => RegisterEffect(id, loader.ParseGameplayEffect(loader.LoadRaw(fullPath)));

        /// <summary>加载单条 Ability 配置</summary>
        public void LoadAbility(Config.IConfigLoader loader, int id, string fullPath)
            => RegisterAbility(id, loader.ParseAbility(loader.LoadRaw(fullPath)));

        /// <summary>加载标签层级 JSON → 注册到缓存</summary>
        public void LoadTags(Config.IConfigLoader loader, string fullPath)
        {
            var json = loader.LoadRaw(fullPath);
            if (json == null) return;
            RegisterTagHierarchy(loader.ParseTagHierarchy(json));
        }

        /// <summary>从目录加载所有 GE（文件名 = ID.json）</summary>
        public void LoadEffectsFromDir(Config.IConfigLoader loader, string dirPath)
        {
            var dir = new System.IO.DirectoryInfo(dirPath);
            if (!dir.Exists) return;
            foreach (var file in dir.GetFiles("*.json"))
            {
                if (int.TryParse(System.IO.Path.GetFileNameWithoutExtension(file.Name), out int id))
                {
                    var json = loader.LoadRaw(file.FullName);
                    if (json != null) RegisterEffect(id, loader.ParseGameplayEffect(json));
                }
            }
        }

        /// <summary>从目录加载所有 Ability（文件名 = ID.json）</summary>
        public void LoadAbilitiesFromDir(Config.IConfigLoader loader, string dirPath)
        {
            var dir = new System.IO.DirectoryInfo(dirPath);
            if (!dir.Exists) return;
            foreach (var file in dir.GetFiles("*.json"))
            {
                if (int.TryParse(System.IO.Path.GetFileNameWithoutExtension(file.Name), out int id))
                {
                    var json = loader.LoadRaw(file.FullName);
                    if (json != null) RegisterAbility(id, loader.ParseAbility(json));
                }
            }
        }
    }
}
