using Unity.Burst;  
using Unity.Entities;  
  
namespace NexusFramework.GAS.ECS  
{  
    [UpdateInGroup(typeof(SGRemoveEffect))]  
    [UpdateBefore(typeof(SRemoveEffectFromAscBuffList))]  
    [DisableAutoCreation]
    public partial struct SRemoveGrantedAbilityOnRemove : ISystem  
    {  
        [BurstCompile]  
        public void OnCreate(ref SystemState state)  
        {  
            state.RequireForUpdate<WipRemoveEffect>();  
            state.RequireForUpdate<CEffectInstance>();  
            state.RequireForUpdate<MCGrantedAbility>();  
            state.RequireForUpdate<CEffectInUsage>();  
        }  
  
        public void OnUpdate(ref SystemState state)  
        {  
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            var removalList = new Unity.Collections.NativeList<GEBufferRemoval>(Unity.Collections.Allocator.Temp);
            foreach (var (_, _, grantedAbilityComp, inUsage, ge) in  
                     SystemAPI.Query<  
                         RefRO<CEffectInstance>,  
                         RefRO<WipRemoveEffect>,  
                         MCGrantedAbility,  
                         RefRO<CEffectInUsage>>().WithEntityAccess())  
            {  
                if (!state.EntityManager.HasComponent<MCGrantedAbilityRuntime>(ge)) continue;  
   
                var runtime = state.EntityManager.GetComponentData<MCGrantedAbilityRuntime>(ge);  
                if (runtime.GrantedAbilityEntities == null) continue;  
   
                var grantedAbilities = grantedAbilityComp.GrantedAbilities;  
                var targetAsc = inUsage.ValueRO.Target;  
                var abilityBuffer = SystemAPI.GetBuffer<BAbility>(targetAsc);  
   
                for (int i = 0; i < grantedAbilities.Length; i++)  
                {  
                    var abilityEntity = runtime.GrantedAbilityEntities[i];  
                    if (abilityEntity == Entity.Null) continue;  
   
                    if (grantedAbilities[i].RemovePolicy == GrantedAbilityRemovePolicy.SyncWithEffect)  
                    {  
                        if (state.EntityManager.HasComponent<CAbilityActive>(abilityEntity))  
                        {  
                            ecb.AddComponent<CAbilityInTryCancel>(abilityEntity);  
                        }  
   
                        for (int j = abilityBuffer.Length - 1; j >= 0; j--)  
                        {  
                            if (abilityBuffer[j].Ability == abilityEntity)  
                            {  
                                removalList.Add(new GEBufferRemoval { TargetAsc = targetAsc, BufferIndex = j });  
                                break;  
                            }  
                        }  
   
                        runtime.GrantedAbilityEntities[i] = Entity.Null;  
                    }  
                }  
            }

            for (int i = removalList.Length - 1; i >= 0; i--)
            {
                var r = removalList[i];
                var buf = state.EntityManager.GetBuffer<BAbility>(r.TargetAsc);
                buf.RemoveAt(r.BufferIndex);
            }

            removalList.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        struct GEBufferRemoval
        {
            public Entity TargetAsc;
            public int BufferIndex;
        }
  
        /// <summary>  
        /// 注销该Ability上所有GrantedAbility相关的事件回调  
        /// </summary>  
        private static void UnregisterAllCallbacks(Entity abilityEntity)  
        {  
            // 清理可能存在的回调，防止内存泄漏  
            // 注意：这里简单处理，实际可根据RemovePolicy精确注销  
            // GASEventCenter的Remove操作对不存在的key是安全的  
        }  
  
        [BurstCompile]  
        public void OnDestroy(ref SystemState state)  
        {  
        }  
    }  
}
