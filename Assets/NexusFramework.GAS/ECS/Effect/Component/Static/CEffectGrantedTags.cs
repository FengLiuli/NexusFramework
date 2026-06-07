using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CEffectGrantedTags : IComponentData
    {
        public NativeArray<int> tags;
    }

    public sealed class ConfEffectGrantedTags : GameplayEffectComponentConfig
    {
        public int[] tags;

        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<CEffectGrantedTags>(ge);
            _entityManager.SetComponentData(ge, new CEffectGrantedTags
            {
                tags = new NativeArray<int>(tags, Allocator.Persistent)
            });
        }
    }
}