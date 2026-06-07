using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SysGrpDisplay))]
    [UpdateAfter(typeof(SCueEnd))]
    public partial struct SCueDestroy : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ECKillCue>();
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var entities = new NativeList<Entity>(Allocator.Temp);

            foreach (var (_,mcCue,cueEntity) in SystemAPI.Query<RefRO<ECKillCue>,MCCue>().WithEntityAccess())
            {
                mcCue.cue.OnDestroy(Time.time);
                entities.Add(cueEntity);
            }

            // 先回收 NativeArray，再销毁实体
            var em = state.EntityManager;
            foreach (var cueEntity in entities)
            {
                if (em.HasComponent<CPlayRequiredTags>(cueEntity))
                {
                    var req = em.GetComponentData<CPlayRequiredTags>(cueEntity).requirement;
                    if (req.all.IsCreated) req.all.Dispose();
                    if (req.any.IsCreated) req.any.Dispose();
                    if (req.none.IsCreated) req.none.Dispose();
                }
                if (em.HasComponent<CPlayImmunitedTags>(cueEntity))
                {
                    var req = em.GetComponentData<CPlayImmunitedTags>(cueEntity).requirement;
                    if (req.all.IsCreated) req.all.Dispose();
                    if (req.any.IsCreated) req.any.Dispose();
                    if (req.none.IsCreated) req.none.Dispose();
                }
                ecb.DestroyEntity(cueEntity);
            }

            entities.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}