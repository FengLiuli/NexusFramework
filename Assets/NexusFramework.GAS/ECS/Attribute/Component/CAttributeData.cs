using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CAttributeData : IComponentData
    {
        public int Code;
        public float BaseValue;
        public float CurrentValue;
        public bool IsClampMin;
        public bool IsClampMax;
        public float MinValue;
        public float MaxValue;
        public bool Dirty;
        
        public static readonly CAttributeData NULL = new()
        {
            Code = -1
        };
    }
}