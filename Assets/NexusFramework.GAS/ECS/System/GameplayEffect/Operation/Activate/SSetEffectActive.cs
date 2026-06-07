using Unity.Burst;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [UpdateInGroup(typeof(SGActivateEffect))]
    [UpdateBefore(typeof(SActivateEnd))]
    [DisableAutoCreation]
    public partial struct SSetEffectActive : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate<WipActivateEffect>();
            state.RequireForUpdate<CEffectInstance>();
            state.RequireForUpdate<CDuration>();
            state.RequireForUpdate<CEffectInUsage>();
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var globalTimer = SystemAPI.GetSingleton<GlobalTimer>();
            foreach (var (_, _,inUsage, durationComp, _, ge) in
                     SystemAPI.Query<
                         RefRO<CEffectInstance>,
                         RefRO<WipActivateEffect>,
                         RefRO<CEffectInUsage>,
                         RefRW<CDuration>,
                         RefRO<CEffectInUsage>>().WithEntityAccess())
            {
                var duration = durationComp.ValueRW;
                duration.active = true;
                duration.activeTime = 
                    duration.timeUnit == TimeUnit.Frame
                    ? globalTimer.Frame
                    : globalTimer.Turn;
                durationComp.ValueRW = duration;
                
                GASInternalBridge.Enqueue(new GEActivatedEvent { Target = inUsage.ValueRO.Target, EffectCode = ge.Index });
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}
