using Unity.Entities;
using Unity.Mathematics;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    ///     GameplayEffect 的公共辅助方法。
    ///     与 <see cref="GEConfigHelper"/> 的区别：后者只负责"按配置建 GE 原型 Entity"，
    ///     这里负责运行时行为——立即生效、应用、移除、堆叠查找。
    /// </summary>
    public static class GameplayEffectHelper
    {
        /// <summary>
        ///     立即应用 Instant 类型的 GameplayEffect（直接修改 BaseValue）。
        ///     用于 Cost 或其他需要绕过 ECS GE 管线立即生效的场景。
        ///     <para>
        ///         该方法不走 GE 生命周期管线，不产生 GE 实例 Entity。
        ///         属性变更经 <see cref="GASInternalBridge"/> 投递，
        ///         因此本方法<b>不是</b> Burst 安全的，调用方必须处于托管上下文。
        ///     </para>
        /// </summary>
        /// <param name="entityManager">目标 World 的 EntityManager</param>
        /// <param name="ecb">用于标记属性重算，调用方负责 Playback</param>
        /// <param name="gameplayEffect">GE 的原型 Entity（需含 MCModifiers）</param>
        public static void ApplyGameplayEffectImmediate(EntityManager entityManager, EntityCommandBuffer ecb,
            Entity gameplayEffect, Entity target, Entity source)
        {
            if (!entityManager.HasComponent<MCModifiers>(gameplayEffect)) return;

            var modifiers = entityManager.GetComponentData<MCModifiers>(gameplayEffect);
            var attrSets = entityManager.GetBuffer<BEAttrSet>(target);
            var change = false;

            foreach (var modifier in modifiers.Modifiers)
            {
                var attrSetIndex = attrSets.IndexOfAttrSetCode(modifier.AttrSetCode);
                if (attrSetIndex == -1) continue;

                var attrSet = attrSets[attrSetIndex];
                var attributes = attrSet.Attributes;

                var attrIndex = attributes.IndexOfAttrCode(modifier.AttrCode);
                if (attrIndex == -1) continue;

                var data = attributes[attrIndex];
                var oldValue = data.BaseValue;
                var newValue = GasMmcHelper.Calculate(entityManager, gameplayEffect, modifier, data.BaseValue, source, target);

                if (data.IsClampMin) newValue = math.max(newValue, data.MinValue);
                if (data.IsClampMax) newValue = math.min(newValue, data.MaxValue);

                data.BaseValue = newValue;

                if (newValue != oldValue)
                {
                    data.Dirty = true;
                    change = true;
                    GASInternalBridge.Enqueue(new AttributeBaseChangedEvent
                    {
                        Target = target,
                        AttrSetCode = modifier.AttrSetCode,
                        AttrCode = modifier.AttrCode,
                        OldValue = oldValue,
                        NewValue = newValue
                    });
                }

                attrSet.Attributes[attrIndex] = data;
                attrSets[attrSetIndex] = attrSet;
            }

            if (change)
                ecb.AddComponent<CAttributeIsDirty>(target);
        }

        /// <summary>
        ///     在目标的 GE 容器中查找按来源聚合的堆叠效果。
        /// </summary>
        public static Entity GetStackingEffectBySource(EntityManager entityManager, int stackingCode,
            Entity targetAsc, Entity sourceAsc)
        {
            var effects = entityManager.GetBuffer<BGameplayEffect>(targetAsc);

            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i].GameplayEffect;

                if (!entityManager.HasComponent<CStacking>(effect)) continue;

                var stacking = entityManager.GetComponentData<CStacking>(effect);
                if (stacking.StackType != EffectStackType.AggregateBySource) continue;

                var source = entityManager.GetComponentData<CEffectInUsage>(effect).Source;
                if (source != sourceAsc) continue;

                if (stacking.StackingCode == stackingCode)
                    return effect;
            }

            return Entity.Null;
        }

        /// <summary>
        ///     在目标的 GE 容器中查找按目标聚合的堆叠效果。
        /// </summary>
        public static Entity GetStackingEffectByTarget(EntityManager entityManager, int stackingCode,
            Entity targetAsc)
        {
            var effects = entityManager.GetBuffer<BGameplayEffect>(targetAsc);

            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i].GameplayEffect;

                if (!entityManager.HasComponent<CStacking>(effect)) continue;

                var stacking = entityManager.GetComponentData<CStacking>(effect);
                if (stacking.StackType != EffectStackType.AggregateByTarget) continue;

                if (stacking.StackingCode == stackingCode)
                    return effect;
            }

            return Entity.Null;
        }
    }
}
