using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace NexusFramework.GAS.ECS
{
    public static class AttributeHelper
    {
        public static float RecalculateCurrentValue(EntityManager entityManager, Entity asc, int attrSetCode, int attrCode)
        {
            var attrSets = entityManager.GetBuffer<BEAttrSet>(asc);
            var attrSetIndex = attrSets.IndexOfAttrSetCode(attrSetCode);
            if (attrSetIndex == -1) return 0;
            var attrSet = attrSets[attrSetIndex];

            var attributes = attrSet.Attributes;
            var attrIndex = attributes.IndexOfAttrCode(attrCode);
            if (attrIndex == -1) return 0;
            var attr = attributes[attrIndex];

            var oldValue = attr.CurrentValue;
            attr.CurrentValue = attr.BaseValue;

            if (entityManager.HasBuffer<BGameplayEffect>(asc))
            {
                var gameplayEffects = entityManager.GetBuffer<BGameplayEffect>(asc);
                foreach (var buffer in gameplayEffects)
                {
                    var ge = buffer.GameplayEffect;
                    if (!entityManager.HasComponent<CDuration>(ge)) continue;
                    var cDuration = entityManager.GetComponentData<CDuration>(ge);
                    if (!cDuration.active) continue;

                    if (!entityManager.HasComponent<MCModifiers>(ge)) continue;
                    var inUsage = entityManager.GetComponentData<CEffectInUsage>(ge);
                    var mmcContext = new MmcContext { EffectEntity = ge, Source = inUsage.Source, Target = inUsage.Target };
                    var mods = entityManager.GetComponentData<MCModifiers>(ge);
                    foreach (var mod in mods.Modifiers)
                    {
                        if (mod.AttrSetCode != attrSetCode || mod.AttrCode != attrCode) continue;
                        attr.CurrentValue = mod.Apply(mmcContext, attr.CurrentValue);
                        if (attr.IsClampMin) attr.CurrentValue = math.max(attr.CurrentValue, attr.MinValue);
                        if (attr.IsClampMax) attr.CurrentValue = math.min(attr.CurrentValue, attr.MaxValue);
                    }
                }
            }

            if (attr.IsClampMin) attr.CurrentValue = math.max(attr.CurrentValue, attr.MinValue);
            if (attr.IsClampMax) attr.CurrentValue = math.min(attr.CurrentValue, attr.MaxValue);

            attr.Dirty = false;
            attrSet.Attributes[attrIndex] = attr;
            attrSets[attrSetIndex] = attrSet;

            if (math.abs(oldValue - attr.CurrentValue) > 0.0001f)
            {
                GASInternalBridge.Enqueue(new AttributeChangedEvent
                {
                    Target = asc, AttrSetCode = attrSetCode, AttrCode = attrCode,
                    OldValue = oldValue, NewValue = attr.CurrentValue
                });
            }

            return attr.CurrentValue;
        }
    }
}
