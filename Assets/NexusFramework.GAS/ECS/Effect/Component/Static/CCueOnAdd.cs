using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CCueOnAdd : IComponentData
    {
        /// <summary>
        ///     cue entity
        /// </summary>
        public NativeArray<Entity> cues;
    }

    public sealed class ConfCueOnAdd : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var entities = CreateCueEntityArray(ge);
            _entityManager.AddComponent<CCueOnAdd>(ge);
            _entityManager.SetComponentData(ge, new CCueOnAdd
            {
                cues = entities
            });
        }
    }
}