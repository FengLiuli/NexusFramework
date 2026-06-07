using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CPlayRequiredTags : IComponentData
    {
        public TagRequirementData requirement;
    }
}
