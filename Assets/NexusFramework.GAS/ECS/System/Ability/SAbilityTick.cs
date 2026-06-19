using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [UpdateInGroup(typeof(SGAbility))]
    [UpdateAfter(typeof(STryEndAbility))]
    [DisableAutoCreation]
    public partial struct SAbilityTick : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CAbilityActive>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // 缓存计时器值
            var globalTimer = SystemAPI.GetSingletonRW<GlobalTimer>();
            var cachedTimer = globalTimer.ValueRO;

            // 先收集所有活跃 Ability 的 logic 引用
            // ——必须提前结束 Query 遍历后再调用回调，因为 AbilityTick 内部可能触发
            //   结构性变更（TryEndSelf → AddComponent），违反 ECS 的安全规则
            var tickList = new System.Collections.Generic.List<MCAbilityLogic>();
            foreach (var (_, abilityLogic) in SystemAPI.Query<RefRO<CAbilityActive>, MCAbilityLogic>())
            {
                tickList.Add(abilityLogic);
            }

            // 遍历结束后再执行 AbilityTick（此时允许结构性变更）
            for (int i = 0; i < tickList.Count; i++)
            {
                tickList[i].logic?.AbilityTick(cachedTimer);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
