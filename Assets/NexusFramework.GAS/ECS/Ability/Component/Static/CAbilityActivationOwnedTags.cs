using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CAbilityActivationOwnedTags : IComponentData
    {
        public NativeArray<int> tags;
    }

    public sealed class ConfAbilityActivationOwnedTags : AbilityComponentConfig
    {
        public int[] tags;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.AddComponent<CAbilityActivationOwnedTags>(ability);
            _entityManager.SetComponentData(ability, new CAbilityActivationOwnedTags
            {
                tags = new NativeArray<int>(tags, Allocator.Persistent)
            });
        }
    }
}