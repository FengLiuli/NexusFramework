using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    ///     在片段起点触发技能冷却：从原型克隆 CD 实例、覆写时长为技能配置值，
    ///     再走完整的 GE 管线应用（Instantiate→CheckApply→Apply→Activate），
    ///     使冷却标签真正挂到 Owner 上。
    /// </summary>
    public class TaskDoCooldown : AbilityTaskBase<XParamNone>
    {
        public TaskDoCooldown(AbilityLogicBase logic) : base(logic)
        {
        }

        protected override void OnBegin(int startFrame)
        {
            var ability = AbilityEntity;
            var em = EntityManager;

            if (ability == Entity.Null || !em.HasComponent<CAbilityCooldown>(ability))
                return;

            var cooldown = em.GetComponentData<CAbilityCooldown>(ability);
            if (cooldown.ProtoGameplayEffectCooldown == Entity.Null)
            {
                Debug.LogWarning($"[NF.GAS] TaskDoCooldown: 技能 {ability} 的冷却原型为空");
                return;
            }

            var owner = OwnerEntity;

            // 1) 从原型克隆出实例
            var instance = em.Instantiate(cooldown.ProtoGameplayEffectCooldown);

            // 2) 用技能配置的冷却时长覆写
            if (cooldown.Cooldown > 0 && em.HasComponent<CDuration>(instance))
            {
                var duration = em.GetComponentData<CDuration>(instance);
                duration.duration = cooldown.Cooldown;
                em.SetComponentData(instance, duration);
            }

            // 3) 走 GE 管线挂到 Owner
            Logic.ApplyGameplayEffectTo(instance, owner, owner);
        }
    }
}
