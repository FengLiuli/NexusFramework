using NexusFramework.DataCarrier;

namespace NexusFramework.GAS.Events
{
    public struct GASAttributeChangedEvent
    {
        public int CarrierId;
        public int AttrSetCode;
        public int AttrCode;
        public float OldValue;
        public float NewValue;
    }

    public struct GASEffectAppliedEvent
    {
        public int Target;
        public int Source;
        public int ConfigId;
    }

    public struct GASAbilityActivatedEvent
    {
        public int CarrierId;
        public int AbilityCode;
        public bool Success;
    }

    public struct GASCarrierCreatedEvent
    {
        public int CarrierId;
        public string TypeName;
    }

    public struct GASCarrierDestroyedEvent
    {
        public int CarrierId;
    }
}
