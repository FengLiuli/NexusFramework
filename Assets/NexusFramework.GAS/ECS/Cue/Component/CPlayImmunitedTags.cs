using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CPlayImmunitedTags : IComponentData
    {
        public TagRequirementData requirement;
    }
}
