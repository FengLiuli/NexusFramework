using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public enum EffectStackType
    {
        AggregateBySource,
        AggregateByTarget
    }

    public enum EffectDurationRefreshPolicy
    {
        NeverRefresh,
        RefreshOnSuccessfulApplication
    }

    public enum EffectPeriodResetPolicy
    {
        NeverRefresh,
        ResetOnSuccessfulApplication
    }

    public enum EffectExpirationPolicy
    {
        ClearEntireStack,
        RemoveSingleStackAndRefreshDuration,
        RefreshDuration
    }

    public struct CStacking : IComponentData
    {
        public EffectStackType StackType;
        public int StackingCode;
        public int LimitCount;

        public EffectDurationRefreshPolicy EffectDurationRefreshPolicy;
        public EffectPeriodResetPolicy EffectPeriodResetPolicy;
        public EffectExpirationPolicy EffectExpirationPolicy;

        // Overflow 溢出逻辑处理
        public bool denyOverflowApplication; //对应于StackDurationRefreshPolicy，如果为True则多余的Apply不会刷新Duration
        public bool clearStackOnOverflow; //当DenyOverflowApplication为True是才有效，当Overflow时是否直接删除所有层数
        public NativeArray<Entity> overflowEffects; // 超过StackLimitCount数量的Effect被Apply时将会调用该Over
        
        
        // -------------------------------------以下是RUNTIME数据，不需要初始化---------------------------------------//
        public int StackCount;
    }
    
    public sealed class ConfStacking:GameplayEffectComponentConfig
    {
        public EffectStackType StackType;
        public int StackingCode;
        public int LimitCount;

        public EffectDurationRefreshPolicy EffectDurationRefreshPolicy;
        public EffectPeriodResetPolicy EffectPeriodResetPolicy;
        public EffectExpirationPolicy EffectExpirationPolicy;

        public bool denyOverflowApplication;
        public bool clearStackOnOverflow;
        public GameplayEffectComponentConfig[][] overflowEffects;

        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var overflowEntities = new NativeArray<Entity>(overflowEffects.Length, Allocator.Persistent);
            for (var i = 0; i < overflowEffects.Length; i++)
            {
                var comConfigs = overflowEffects[i];
                overflowEntities[i] = GEConfigHelper.CreateGameplayEffectEntity(_entityManager, comConfigs);
            }
            
            _entityManager.AddComponent<CStacking>(ge);
            _entityManager.SetComponentData(ge, new CStacking
            {
                StackType = StackType,
                StackingCode = StackingCode,
                LimitCount = LimitCount,
                EffectDurationRefreshPolicy = EffectDurationRefreshPolicy,
                EffectPeriodResetPolicy = EffectPeriodResetPolicy,
                EffectExpirationPolicy = EffectExpirationPolicy,
                denyOverflowApplication = denyOverflowApplication,
                clearStackOnOverflow = clearStackOnOverflow,
                overflowEffects = overflowEntities,
            });
        }
    }
}