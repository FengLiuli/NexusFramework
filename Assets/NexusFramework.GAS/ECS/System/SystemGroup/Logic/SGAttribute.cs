using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SGLogic))]
    [UpdateAfter(typeof(SGEffect))]
    public partial class SGAttribute : ComponentSystemGroup
    {
    }
}