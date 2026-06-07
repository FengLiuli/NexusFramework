using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CCueOnActivate : IComponentData
    {
        public NativeArray<Entity> cues;
    }

    public sealed class ConfCueOnActivate : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var entities = CreateCueEntityArray(ge);
            _entityManager.AddComponent<CCueOnActivate>(ge);
            _entityManager.SetComponentData(ge, new CCueOnActivate
            {
                cues = entities
            });
        }
    }
}