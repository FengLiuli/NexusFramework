using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [InternalBufferCapacity(100)]
    public struct BAbility : IBufferElementData
    {
        public Entity Ability;
    }
}