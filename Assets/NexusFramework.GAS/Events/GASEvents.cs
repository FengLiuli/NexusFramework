using NexusFramework.DataCarrier;

namespace NexusFramework.GAS.Events
{
    public struct GASAttributeChangedEvent
    {
        public CarrierId CarrierId;
        public int AttrSetCode;
        public int AttrCode;
        public float OldValue;
        public float NewValue;
    }

    public struct GASEffectAppliedEvent
    {
        public CarrierId Target;
        public CarrierId Source;
        public int ConfigId;
    }

    public struct GASAbilityActivatedEvent
    {
        public CarrierId CarrierId;
        public int AbilityCode;
        public bool Success;
    }

    public struct GASCarrierCreatedEvent
    {
        public CarrierId CarrierId;
        public string TypeName;
    }

    public struct GASCarrierDestroyedEvent
    {
        public CarrierId CarrierId;
    }
}
