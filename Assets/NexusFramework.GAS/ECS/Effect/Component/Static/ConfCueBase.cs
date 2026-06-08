using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public abstract class ConfCueBase : GameplayEffectComponentConfig
    {
        public GameplayCueConfig[] cues;

        public NativeArray<Entity> CreateCueEntityArray(Entity ge, IGASEntityResolver resolver = null)
        {
            bool HasTags(int[] tags) => tags != null && tags.Length > 0;

            var entities = new Entity[cues.Length];
            for (var i = 0; i < cues.Length; i++)
            {
                entities[i] = _entityManager.CreateEntity();
                var c = cues[i];

                _entityManager.AddComponent<ECCuePlayable>(entities[i]);
                _entityManager.SetComponentEnabled<ECCuePlayable>(entities[i], false);

                _entityManager.AddComponent<ECCuePlaying>(entities[i]);
                _entityManager.SetComponentEnabled<ECCuePlaying>(entities[i], false);

                _entityManager.AddComponent<ECKillCue>(entities[i]);
                _entityManager.SetComponentEnabled<ECKillCue>(entities[i], false);

                _entityManager.AddComponent<MCCue>(entities[i]);
                var instantCue = CueHelper.InitInstantCueFromEffect(
                    new MCCue(c.CreateCue(resolver)), entities[i], ge);
                _entityManager.SetComponentData(entities[i], instantCue);

                if (HasTags(c.ImmunityAllTags) || HasTags(c.ImmunityAnyTags) || HasTags(c.ImmunityNoneTags))
                {
                    _entityManager.AddComponent<CPlayImmunitedTags>(entities[i]);
                    _entityManager.SetComponentData(entities[i], new CPlayImmunitedTags
                    {
                        requirement = new TagRequirementData
                        {
                            all = new NativeArray<int>(c.ImmunityAllTags ?? System.Array.Empty<int>(), Allocator.Persistent),
                            any = new NativeArray<int>(c.ImmunityAnyTags ?? System.Array.Empty<int>(), Allocator.Persistent),
                            none = new NativeArray<int>(c.ImmunityNoneTags ?? System.Array.Empty<int>(), Allocator.Persistent)
                        }
                    });
                }

                if (HasTags(c.RequiredAllTags) || HasTags(c.RequiredAnyTags) || HasTags(c.RequiredNoneTags))
                {
                    _entityManager.AddComponent<CPlayRequiredTags>(entities[i]);
                    _entityManager.SetComponentData(entities[i], new CPlayRequiredTags
                    {
                        requirement = new TagRequirementData
                        {
                            all = new NativeArray<int>(c.RequiredAllTags ?? System.Array.Empty<int>(), Allocator.Persistent),
                            any = new NativeArray<int>(c.RequiredAnyTags ?? System.Array.Empty<int>(), Allocator.Persistent),
                            none = new NativeArray<int>(c.RequiredNoneTags ?? System.Array.Empty<int>(), Allocator.Persistent)
                        }
                    });
                }
            }

            return new NativeArray<Entity>(entities, Allocator.Persistent);
        }

        public virtual void LoadToGameplayCueEntity(Entity cueEntity)
        {
        }
    }
}
