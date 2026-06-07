using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [UpdateInGroup(typeof(SGRemoveEffect))]
    [UpdateBefore(typeof(SEffectRemoveEnd))]
    [DisableAutoCreation]
    public partial struct SRemoveEffectFromAscBuffList : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WipRemoveEffect>();
            state.RequireForUpdate<CEffectInstance>();
            state.RequireForUpdate<CEffectInUsage>();
            state.RequireForUpdate<CDuration>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var removalList = new NativeList<GEBufferRemoval>(Allocator.Temp);
            foreach (var (_, _, _, inUsage, ge) in SystemAPI
                         .Query<
                             RefRO<CEffectInstance>,
                             RefRO<WipRemoveEffect>,
                             RefRO<CDuration>,
                             RefRO<CEffectInUsage>>()
                         .WithEntityAccess())
            {
                var asc = inUsage.ValueRO.Target;
                var geBuff = SystemAPI.GetBuffer<BGameplayEffect>(asc);
                for (var i = geBuff.Length - 1; i >= 0; i--)
                {
                    if (geBuff[i].GameplayEffect != ge) continue;
                    removalList.Add(new GEBufferRemoval { TargetAsc = asc, BufferIndex = i });
                    break;
                }
            }

            // 按 ASC 分组反向删除（避免索引偏移）
            for (int i = removalList.Length - 1; i >= 0; i--)
            {
                var r = removalList[i];
                var buf = state.EntityManager.GetBuffer<BGameplayEffect>(r.TargetAsc);
                if (r.BufferIndex < buf.Length && buf[r.BufferIndex].GameplayEffect == Entity.Null)
                    continue; // 已删除
                buf.RemoveAt(r.BufferIndex);
            }

            removalList.Dispose();
        }

        struct GEBufferRemoval
        {
            public Entity TargetAsc;
            public int BufferIndex;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        private void CheckEffectAttrDirty(EntityManager entityManager, EntityCommandBuffer ecb, Entity asc, Entity ge)
        {
            if (!entityManager.HasComponent<MCModifiers>(ge)) return;
            
            var modifiers = entityManager.GetComponentData<MCModifiers>(ge);
            if (modifiers.Modifiers.Length == 0) return;
            
            var attrSets = entityManager.GetBuffer<BEAttrSet>(asc);
            foreach (var modifier in modifiers.Modifiers)
            {
                var attrSetIndex = attrSets.IndexOfAttrSetCode(modifier.AttrSetCode);
                if (attrSetIndex == -1) continue;

                var attrSet = attrSets[attrSetIndex];
                var attributes = attrSet.Attributes;

                var attrIndex = attributes.IndexOfAttrCode(modifier.AttrCode);
                if (attrIndex == -1) continue;

                var data = attributes[attrIndex];
                // 标记Dirty
                data.Dirty = true;
                attrSet.Attributes[attrIndex] = data;
                attrSets[attrSetIndex] = attrSet;
            }
            
            ecb.AddComponent<CAttributeIsDirty>(asc);
        }
    }
}
