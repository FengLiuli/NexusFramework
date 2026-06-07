using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace NexusFramework.GAS.ECS
{
    [UpdateInGroup(typeof(SGInstantEffect))]
    [DisableAutoCreation]
    public partial struct SExecuteInstantEffectModifiers : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectInstance>();
            state.RequireForUpdate<MCModifiers>();
            state.RequireForUpdate<CEffectInUsage>();
            state.RequireForUpdate<WipApplyEffect>();
        }


        public void OnUpdate(ref SystemState state)
        {
            // Instant Effect = 没有CDuration组件的Effect

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_,_, modifiers, inUsage, ge) in
                     SystemAPI.Query<
                         RefRO<CEffectInstance>,
                         RefRO<WipApplyEffect>,
                         MCModifiers,
                         RefRO<CEffectInUsage>
                     >().WithNone<CDuration>().WithEntityAccess())
            {
                var change = false;
                var asc = inUsage.ValueRO.Target;
                var attrSets = SystemAPI.GetBuffer<BEAttrSet>(asc);
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
                    var newValue = GasMmcHelper.Calculate(state.EntityManager, ge, modifier, data.BaseValue);
                    // 钳制计算处理
                    if (data.IsClampMin) newValue = math.max(newValue, data.MinValue);
                    if (data.IsClampMax) newValue = math.min(newValue, data.MaxValue);
                    
                    data.BaseValue = newValue;

                    if (newValue != oldValue)
                    {
                        data.Dirty = true;
                        change = true;
                        GASInternalBridge.Enqueue(new AttributeBaseChangedEvent
                        {
                            Target = asc, AttrSetCode = modifier.AttrSetCode, AttrCode = modifier.AttrCode,
                            OldValue = oldValue, NewValue = newValue
                        });
                    }

                    attrSet.Attributes[attrIndex] = data;
                    attrSets[attrSetIndex] = attrSet;
                }

                // 触发刷新CurrentValue的事件
                if (change) ecb.AddComponent<CAttributeIsDirty>(asc);
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
