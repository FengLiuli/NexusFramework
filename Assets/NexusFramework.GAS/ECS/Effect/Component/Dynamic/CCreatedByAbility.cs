using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CCreatedByAbility : IComponentData
    {
        public Entity sourceAbility;
    }
}