using Unity.Burst;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [UpdateInGroup(typeof(SGDeactivateEffect))]
    [UpdateBefore(typeof(SDeactivateEnd))]
    [DisableAutoCreation]
    public partial struct SEffectRemoveGrantedTags : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WipDeactivateEffect>();
            state.RequireForUpdate<CEffectInstance>();
            state.RequireForUpdate<CEffectGrantedTags>();
            state.RequireForUpdate<CEffectInUsage>();
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var removalList = new Unity.Collections.NativeList<TagRemoval>(Unity.Collections.Allocator.Temp);
            foreach (var (_, _, grantedTags, inUsage, ge) in
                     SystemAPI.Query<
                         RefRO<CEffectInstance>,
                         RefRO<WipDeactivateEffect>,
                         RefRO<CEffectGrantedTags>,
                         RefRO<CEffectInUsage>>().WithEntityAccess())
            {
                var tags = grantedTags.ValueRO.tags;
                var targetAsc = inUsage.ValueRO.Target;
                foreach (var tag in tags)
                {
                    var tempTags = state.EntityManager.GetBuffer<BTemporaryTag>(targetAsc);
                    for (var i = 0; i < tempTags.Length; i++)
                    {
                        if (tempTags[i].tag == tag && tempTags[i].source == ge)
                        {
                            removalList.Add(new TagRemoval { TargetAsc = targetAsc, BufferIndex = i });
                            break;
                        }
                    }
                }
            }

            // 反向删除以避免索引偏移
            for (int i = removalList.Length - 1; i >= 0; i--)
            {
                var r = removalList[i];
                var buf = state.EntityManager.GetBuffer<BTemporaryTag>(r.TargetAsc);
                buf.RemoveAt(r.BufferIndex);
            }

            removalList.Dispose();
        }

        struct TagRemoval
        {
            public Entity TargetAsc;
            public int BufferIndex;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}
