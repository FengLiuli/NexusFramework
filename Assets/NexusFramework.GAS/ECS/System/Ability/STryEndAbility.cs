using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [UpdateInGroup(typeof(SGAbility))]
    [UpdateAfter(typeof(STryCancelAbility))]
    [DisableAutoCreation]
    public partial struct STryEndAbility : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CAbilityInTryEnd>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var globalTimer = SystemAPI.GetSingletonRW<GlobalTimer>();
            var cachedTimer = globalTimer.ValueRO;
            var tagRemovalList = new NativeList<TagRemoval>(Allocator.Temp);
            var pendingEnds = new NativeList<Entity>(Allocator.Temp);

            foreach (var (_, baseInfo, ability) in SystemAPI.Query<RefRO<CAbilityInTryEnd>, RefRO<CAbilityBaseInfo>>()
                         .WithEntityAccess())
            {
                var result = state.EntityManager.HasComponent<CAbilityActive>(ability);
                if (result)
                {
                    ecb.RemoveComponent<CAbilityActive>(ability);
                    CollectDynamicTagRemovals(state.EntityManager, ability, ref tagRemovalList);
                    pendingEnds.Add(ability);
                }

                ecb.RemoveComponent<CAbilityInTryEnd>(ability);
            }

            for (int i = tagRemovalList.Length - 1; i >= 0; i--)
            {
                var r = tagRemovalList[i];
                var buf = state.EntityManager.GetBuffer<BTemporaryTag>(r.OwnerEntity);
                buf.RemoveAt(r.BufferIndex);
            }

            tagRemovalList.Dispose();
            ecb.Playback(state.EntityManager);

            // ECB 播放后再执行 AbilityLogic.EndAbility（可能包含 Entity 创建等结构性变更）
            foreach (var ability in pendingEnds)
            {
                if (state.EntityManager.HasComponent<MCAbilityLogic>(ability))
                {
                    var abilityLogic = state.EntityManager.GetComponentData<MCAbilityLogic>(ability);
                    abilityLogic.logic?.EndAbility(cachedTimer);
                }
            }

            ecb.Dispose();
            pendingEnds.Dispose();
        }

        struct TagRemoval
        {
            public Entity OwnerEntity;
            public int BufferIndex;
        }

        private static void CollectDynamicTagRemovals(EntityManager entityManager, Entity source, ref NativeList<TagRemoval> removalList)
        {
            bool hasActivationOwnedTags = entityManager.HasComponent<CAbilityActivationOwnedTags>(source);
            if (hasActivationOwnedTags)
            {
                var activationOwnedTags = entityManager.GetComponentData<CAbilityActivationOwnedTags>(source);
                var abilityBaseInfo = entityManager.GetComponentData<CAbilityBaseInfo>(source);
                foreach (var tag in activationOwnedTags.tags)
                {
                    var tempTags = entityManager.GetBuffer<BTemporaryTag>(abilityBaseInfo.Owner);
                    for (var i = 0; i < tempTags.Length; i++)
                    {
                        if (tempTags[i].tag == tag && tempTags[i].source == source)
                        {
                            removalList.Add(new TagRemoval { OwnerEntity = abilityBaseInfo.Owner, BufferIndex = i });
                            break;
                        }
                    }
                }
            }
        }
    }
}
