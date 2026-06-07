using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CCueOnDeactivate : IComponentData
    {
        public NativeArray<Entity> cues;
    }

    public sealed class ConfCueOnDeactivate : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var entities = CreateCueEntityArray(ge);
            _entityManager.AddComponent<CCueOnDeactivate>(ge);
            _entityManager.SetComponentData(ge, new CCueOnDeactivate
            {
                cues = entities
            });
        }
    }
}