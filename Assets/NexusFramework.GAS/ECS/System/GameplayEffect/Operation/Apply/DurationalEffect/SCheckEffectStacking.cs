using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace NexusFramework.GAS.ECS
{
    [UpdateInGroup(typeof(SGDurationalEffect))]
    [DisableAutoCreation]
    public partial struct SCheckEffectStacking : ISystem
    {
        private GlobalTimer _globalTimer;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectInstance>();
            state.RequireForUpdate<CEffectInUsage>();
            state.RequireForUpdate<WipApplyEffect>();
            state.RequireForUpdate<CStacking>();
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _globalTimer = SystemAPI.GetSingletonRW<GlobalTimer>().ValueRO;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, _, _, inUsage, stacking, ge) in SystemAPI
                         .Query<
                             RefRO<CEffectInstance>,
                             RefRO<WipApplyEffect>,
                             RefRO<CDuration>,
                             RefRO<CEffectInUsage>,
                             RefRO<CStacking>>()
                         .WithEntityAccess())
            {
                // 处理有堆叠组件的GameplayEffect
                var stackGe = stacking.ValueRO.StackType switch
                {
                    EffectStackType.AggregateBySource =>
                        GetStackingEffectBySource(state.EntityManager, stacking.ValueRO.StackingCode,
                            inUsage.ValueRO.Target, inUsage.ValueRO.Source),
                    EffectStackType.AggregateByTarget =>  
                        GetStackingEffectByTarget(state.EntityManager, stacking.ValueRO.StackingCode,  
                            inUsage.ValueRO.Target),
                    _ => Entity.Null
                };

                if (stackGe == Entity.Null)
                    AddToAscBuffList(state.EntityManager, ge, inUsage.ValueRO.Target);
                else
                {
                    ecb.RemoveComponent<CEffectInstance>(ge);
                    ecb.AddComponent<CEffectDestroy>(ge);
                }
                
                var operatedEffect = stackGe == Entity.Null ? ge : stackGe;  
                // 读取已有堆叠GE的当前StackCount，而非新传入GE的StackCount  
                var existingStacking = state.EntityManager.GetComponentData<CStacking>(operatedEffect);  
                TryChangeStackCount(state.EntityManager, ecb, operatedEffect, existingStacking, existingStacking.StackCount + 1);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private void AddToAscBuffList(EntityManager entityManager, Entity ge, Entity asc)
        {
            var geBuff = entityManager.GetBuffer<BGameplayEffect>(asc);
            var alreadyExist = false;
            foreach (var geElem in geBuff)
                if (geElem.GameplayEffect == ge)
                {
                    alreadyExist = true;
                    break;
                }

            if (!alreadyExist) geBuff.Add(new BGameplayEffect { GameplayEffect = ge });
        }

        private void TryChangeStackCount(EntityManager entityManager, EntityCommandBuffer ecb, Entity ge, CStacking stacking, int stackCount)
        {
            // 获取旧Stacking数据
            var globalFrameTimer = _globalTimer;
            var oldStackCount = entityManager.GetComponentData<CStacking>(ge).StackCount;
            var newStackCount = stackCount;  
            if (stackCount <= 0)  
            {  
                // 层数减到0，销毁GE  
                newStackCount = 0;  
                ecb.RemoveComponent<CEffectApplied>(ge);  
                ecb.AddComponent<CEffectDestroy>(ge);  
            }  
            else if (stackCount <= stacking.LimitCount)  
            {  
                // 更新栈数  
                newStackCount = stackCount;  
                stacking.StackCount = newStackCount;  
                entityManager.SetComponentData(ge, stacking);

                // 是否刷新Duration
                if (stacking.EffectDurationRefreshPolicy == EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication)
                {
                    var duration = entityManager.GetComponentData<CDuration>(ge);
                    duration = RefreshDuration(duration, globalFrameTimer);  
                    entityManager.SetComponentData(ge, duration);
                }

                // 是否重置Period
                if (stacking.EffectPeriodResetPolicy == EffectPeriodResetPolicy.ResetOnSuccessfulApplication)
                {
                    var hasPeriodTicker = entityManager.HasComponent<CPeriod>(ge);
                    if (hasPeriodTicker)
                    {
                        // 重置Period
                        var period = entityManager.GetComponentData<CPeriod>(ge);
                        var currentFrame = globalFrameTimer.Frame;
                        var currentTurn = globalFrameTimer.Turn;
                        var duration = entityManager.GetComponentData<CDuration>(ge);
                        var time = duration.timeUnit == TimeUnit.Frame ? currentFrame : currentTurn;
                        period.StartTime = time;
                        entityManager.SetComponentData(ge, period);
                    }
                }
            }
            else  
            {  
                // 1. 溢出GE生效  
                if (stacking.overflowEffects.Length > 0)  
                {  
                    var inUsage = entityManager.GetComponentData<CEffectInUsage>(ge);  
                    var target = inUsage.Target;  
                    var source = inUsage.Source;  
                    foreach (var overflowEffect in stacking.overflowEffects)  
                        ApplyGameplayEffectImmediate(entityManager, ecb, overflowEffect, target, source);  
                }  
  
                // 2. 检查是否拒绝溢出应用  
                if (stacking.denyOverflowApplication)  
                {  
                    // 当DenyOverflowApplication为True时，溢出时是否直接删除所有层数  
                    if (stacking.clearStackOnOverflow)  
                    {  
                        ecb.RemoveComponent<CEffectApplied>(ge);  
                        ecb.AddComponent<CEffectDestroy>(ge);  
                    }  
                    // denyOverflow=true 时不刷新Duration（无论策略如何）  
                }  
                else  
                {  
                    // 3. 未拒绝溢出，根据策略刷新Duration  
                    if (stacking.EffectDurationRefreshPolicy == EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication)  
                    {  
                        var duration = entityManager.GetComponentData<CDuration>(ge);  
                        duration = RefreshDuration(duration, globalFrameTimer);  
                        entityManager.SetComponentData(ge, duration);  
                    }  
                }  
            }

            if (oldStackCount != newStackCount)
            {
                GASInternalBridge.Enqueue(new EffectStackChangedEvent
                {
                    EffectEntity = ge, OldStackCount = oldStackCount, NewStackCount = newStackCount
                });
            }
        }

        /// <summary>  
        /// 刷新Duration的激活时间（用于Stacking的Duration刷新）。  
        /// 无论当前是否已激活，都强制重置 activeTime 为当前时间。  
        /// </summary>  
        private CDuration RefreshDuration(CDuration duration, GlobalTimer globalFrameTimer)  
        {  
            var currentFrame = globalFrameTimer.Frame;  
            var currentTurn = globalFrameTimer.Turn;  
  
            duration.active = true;  
            if (duration.timeUnit == TimeUnit.Frame)  
            {  
                duration.activeTime = currentFrame;  
                duration.lastActiveTime = currentFrame;  
            }  
            else  
            {  
                duration.activeTime = currentTurn;  
                duration.lastActiveTime = currentTurn;  
            }  
  
            return duration;  
        }

        private static Entity GetStackingEffectBySource(EntityManager entityManager, int stackingCode, Entity targetAsc, Entity sourceAsc)
        {
            var effects = entityManager.GetBuffer<BGameplayEffect>(targetAsc);

            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i].GameplayEffect;

                var hasStacking = entityManager.HasComponent<CStacking>(effect);
                if (!hasStacking) continue;

                var stacking = entityManager.GetComponentData<CStacking>(effect);
                if (stacking.StackType != EffectStackType.AggregateBySource) continue;

                var source = entityManager.GetComponentData<CEffectInUsage>(effect).Source;
                if (source != sourceAsc) continue;

                if (stacking.StackingCode == stackingCode)
                    return effect;
            }

            return Entity.Null;
        }

        private static Entity GetStackingEffectByTarget(EntityManager entityManager, int stackingCode, Entity targetAsc)
        {
            var effects = entityManager.GetBuffer<BGameplayEffect>(targetAsc);

            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i].GameplayEffect;

                var hasStacking = entityManager.HasComponent<CStacking>(effect);
                if (!hasStacking) continue;

                var stacking = entityManager.GetComponentData<CStacking>(effect);
                if (stacking.StackType != EffectStackType.AggregateByTarget) continue;

                if (stacking.StackingCode == stackingCode)
                    return effect;
            }

            return Entity.Null;
        }

        private static void ApplyGameplayEffectImmediate(EntityManager entityManager, EntityCommandBuffer ecb, Entity gameplayEffect, Entity target, Entity source)
        {
            if (!entityManager.HasComponent<MCModifiers>(gameplayEffect)) return;

            var modifiers = entityManager.GetComponentData<MCModifiers>(gameplayEffect);
            var attrSets = entityManager.GetBuffer<BEAttrSet>(target);
            bool change = false;

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
                }

                attrSet.Attributes[attrIndex] = data;
                attrSets[attrSetIndex] = attrSet;
            }

            if (change)
                ecb.AddComponent<CAttributeIsDirty>(target);
        }
    }
}
