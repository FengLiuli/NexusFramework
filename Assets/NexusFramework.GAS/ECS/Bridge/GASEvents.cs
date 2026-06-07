using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    /// <summary>GE 施加到目标</summary>
    public struct GEAppliedEvent
    {
        public Entity Target;
        public Entity Source;
        public int EffectCode;
    }

    /// <summary>GE 激活（持续型生效）</summary>
    public struct GEActivatedEvent
    {
        public Entity Target;
        public int EffectCode;
    }

    /// <summary>GE 从目标移除</summary>
    public struct GERemovedEvent
    {
        public Entity Target;
        public int EffectCode;
    }

    /// <summary>属性当前值变化</summary>
    public struct AttributeChangedEvent
    {
        public Entity Target;
        public int AttrSetCode;
        public int AttrCode;
        public float OldValue;
        public float NewValue;
    }

    /// <summary>属性基础值变化</summary>
    public struct AttributeBaseChangedEvent
    {
        public Entity Target;
        public int AttrSetCode;
        public int AttrCode;
        public float OldValue;
        public float NewValue;
    }

    /// <summary>能力激活</summary>
    public struct AbilityActivatedEvent
    {
        public Entity Owner;
        public int AbilityCode;
    }

    /// <summary>能力结束</summary>
    public struct AbilityEndedEvent
    {
        public Entity Owner;
        public int AbilityCode;
    }

    /// <summary>能力取消</summary>
    public struct AbilityCancelledEvent
    {
        public Entity Owner;
        public int AbilityCode;
    }

    /// <summary>效果堆叠层数变化</summary>
    public struct EffectStackChangedEvent
    {
        public Entity EffectEntity;
        public int OldStackCount;
        public int NewStackCount;
    }
}
