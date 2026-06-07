using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CCueOnRemove : IComponentData
    {
        /// <summary>
        ///     cue entity
        /// </summary>
        public NativeArray<Entity> cues;
    }

    public sealed class ConfCueOnRemove : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var entities = CreateCueEntityArray(ge);
            _entityManager.AddComponent<CCueOnRemove>(ge);
            _entityManager.SetComponentData(ge, new CCueOnRemove
            {
                cues = entities
            });
        }
    }
}