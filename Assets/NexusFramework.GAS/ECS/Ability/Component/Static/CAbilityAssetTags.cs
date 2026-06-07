using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CAbilityAssetTags : IComponentData
    {
        public NativeArray<int> tags;
    }
    
    public sealed class ConfAbilityAssetTags:AbilityComponentConfig
    {
        public int[] tags;
        
        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.AddComponent<CAbilityAssetTags>(ability);
            _entityManager.SetComponentData(ability, new CAbilityAssetTags
            {
                tags = new NativeArray<int>(tags, Allocator.Persistent)
            });
        }
    }
}