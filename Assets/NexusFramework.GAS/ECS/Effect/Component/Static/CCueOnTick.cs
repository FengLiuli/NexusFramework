using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CCueOnTick : IComponentData  
    {  
        public NativeArray<Entity> cues;  
    }

    public sealed class ConfCueOnTick : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var entities = CreateCueEntityArray(ge);
            _entityManager.AddComponent<CCueOnTick>(ge);
            _entityManager.SetComponentData(ge, new CCueOnTick
            {
                cues = entities
            });
        }
    }
}