using Unity.Burst;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [UpdateInGroup(typeof(SGActivateEffect))]
    [UpdateBefore(typeof(SActivateEnd))]
    [DisableAutoCreation]
    public partial struct SEffectAddGrantedTags : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WipActivateEffect>();
            state.RequireForUpdate<CEffectInstance>();
            state.RequireForUpdate<CEffectGrantedTags>();
            state.RequireForUpdate<CEffectInUsage>();
            state.RequireForUpdate<SingletonGameplayTagMap>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            var tagMap = SystemAPI.GetSingleton<SingletonGameplayTagMap>();
            foreach (var (_, _, grantedTags, inUsage,ge) in
                     SystemAPI.Query<
                         RefRO<CEffectInstance>,
                         RefRO<WipActivateEffect>,
                         RefRO<CEffectGrantedTags>,
                         RefRO<CEffectInUsage>>().WithEntityAccess())
            {

                var tags = grantedTags.ValueRO.tags;
                var targetAsc = inUsage.ValueRO.Target;
                foreach (var tag in tags)
                {
                    if (!GasTagHelperManaged.HasTemporaryTag(state.EntityManager, tagMap, targetAsc, ge, tag))
                        ecb.AppendToBuffer<BTemporaryTag>(targetAsc, new BTemporaryTag { source = ge, tag = tag });
                }
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}
