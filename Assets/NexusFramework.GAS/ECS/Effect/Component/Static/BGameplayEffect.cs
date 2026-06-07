using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [InternalBufferCapacity(500)]
    public struct BGameplayEffect : IBufferElementData
    {
        public Entity GameplayEffect;
    }
}
