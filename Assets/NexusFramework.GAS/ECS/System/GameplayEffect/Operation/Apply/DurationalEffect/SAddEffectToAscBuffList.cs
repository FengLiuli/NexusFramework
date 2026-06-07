using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [UpdateInGroup(typeof(SGDurationalEffect))]
    [DisableAutoCreation]
    public partial struct SAddEffectToAscBuffList : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectInstance>();
            state.RequireForUpdate<CEffectInUsage>();
            state.RequireForUpdate<WipApplyEffect>();
            state.RequireForUpdate<CDuration>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (_,_, _,inUsage, ge) in SystemAPI
                         .Query<
                             RefRO<CEffectInstance>, 
                             RefRO<WipApplyEffect>, 
                             RefRO<CDuration>, 
                             RefRO<CEffectInUsage>>()
                         .WithNone<CStacking>()
                         .WithEntityAccess())
            {
                // 处理没有Stacking堆叠组件的GameplayEffect
                var asc = inUsage.ValueRO.Target;
                var geBuff = SystemAPI.GetBuffer<BGameplayEffect>(asc);
                var alreadyExist = false;
                foreach (var geElem in geBuff)
                    if (geElem.GameplayEffect == ge)
                    {
                        alreadyExist = true;
                        break;
                    }

                if (!alreadyExist) 
                    ecb.AppendToBuffer<BGameplayEffect>(asc, new BGameplayEffect { GameplayEffect = ge });
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
