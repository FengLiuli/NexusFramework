using System.IO;
using NexusFramework;
using NexusFramework.GAS.ECS;
using UnityEngine;

namespace NexusFramework.GAS.Config
{
    public class JsonConfigLoader : IConfigLoader
    {
        public bool Initialized { get; set; }

        void ICanInit.Init() => Initialized = true;
        void ICanInit.Deinit() => Initialized = false;

        public string LoadRaw(string fullPath)
        {
            if (!File.Exists(fullPath)) return null;
            return File.ReadAllText(fullPath);
        }

        public GameplayEffectComponentConfig[] ParseGameplayEffect(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            // TODO: 从 JSON 反序列化为 GameplayEffectComponentConfig[]
            Debug.LogWarning("[JsonConfigLoader] ParseGameplayEffect not implemented");
            return null;
        }

        public AbilityComponentConfig[] ParseAbility(string json)
        {
            Debug.LogWarning("[JsonConfigLoader] ParseAbility not implemented");
            return null;
        }

        public GameplayCueConfig ParseGameplayCue(string json)
        {
            Debug.LogWarning("[JsonConfigLoader] ParseGameplayCue not implemented");
            return default;
        }

        public MMCConfig ParseMmc(string json)
        {
            Debug.LogWarning("[JsonConfigLoader] ParseMmc not implemented");
            return default;
        }

        public TagHierarchyData ParseTagHierarchy(string json)
        {
            if (string.IsNullOrEmpty(json)) return default;
            var node = JsonUtility.FromJson<TagNode>(json);
            return new TagHierarchyData { Tags = new[] { node } };
        }
    }
}
