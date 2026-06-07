using System;
using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CRemoveEffectWithTags : IComponentData
    {
        public TagRequirementData requirement;
    }
    
    public sealed class ConfRemoveEffectWithTags:GameplayEffectComponentConfig
    {
        public int[] tags;
        public int[] all;
        public int[] any;
        public int[] none;
        
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<CRemoveEffectWithTags>(ge);
            _entityManager.SetComponentData(ge, new CRemoveEffectWithTags
            {
                requirement = new TagRequirementData
                {
                    all = new NativeArray<int>(all ?? Array.Empty<int>(), Allocator.Persistent),
                    any = new NativeArray<int>(any ?? tags ?? Array.Empty<int>(), Allocator.Persistent),
                    none = new NativeArray<int>(none ?? Array.Empty<int>(), Allocator.Persistent)
                }
            });
        }
    }
}
