using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CCueOnApply : IComponentData
    {
        public NativeArray<Entity> cues;
    }

    public sealed class ConfCueOnApply : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var entities = CreateCueEntityArray(ge);
            _entityManager.AddComponent<CCueOnApply>(ge);
            _entityManager.SetComponentData(ge, new CCueOnApply
            {
                cues = entities
            });
        }
    }
}