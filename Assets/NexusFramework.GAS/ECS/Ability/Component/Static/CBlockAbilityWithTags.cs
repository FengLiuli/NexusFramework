using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CBlockAbilityWithTags : IComponentData
    {
        public NativeArray<int> tags;
    }
    
    public sealed class ConfBlockAbilityWithTags:AbilityComponentConfig
    {
        public int[] tags;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.AddComponent<CBlockAbilityWithTags>(ability);
            _entityManager.SetComponentData(ability, new CBlockAbilityWithTags
            {
                tags = new NativeArray<int>(tags, Allocator.Persistent)
            });
        }
    }
}