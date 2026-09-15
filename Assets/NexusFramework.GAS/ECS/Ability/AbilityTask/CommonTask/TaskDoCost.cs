using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    ///     在片段起点扣除技能的消耗。
    ///     消耗以"立即生效"方式应用（直接改 BaseValue，不走 GE 生命周期管线），
    ///     因为它不应产生 GE 实例，也不该被免疫/条件判定拦截。
    /// </summary>
    public class TaskDoCost : AbilityTaskBase<XParamNone>
    {
        public TaskDoCost(AbilityLogicBase logic) : base(logic)
        {
        }

        protected override void OnBegin(int startFrame)
        {
            var ability = AbilityEntity;
            var em = EntityManager;

            if (ability == Entity.Null || !em.HasComponent<CAbilityCost>(ability))
                return;

            var cost = em.GetComponentData<CAbilityCost>(ability);
            if (cost.ProtoGameplayEffectCost == Entity.Null)
            {
                Debug.LogWarning($"[NF.GAS] TaskDoCost: 技能 {ability} 的消耗原型为空");
                return;
            }

            var owner = OwnerEntity;
            using var ecb = new Unity.Entities.EntityCommandBuffer(
                Unity.Collections.Allocator.Temp);

            GameplayEffectHelper.ApplyGameplayEffectImmediate(
                em, ecb, cost.ProtoGameplayEffectCost, owner, owner);

            ecb.Playback(em);
        }
    }
}
