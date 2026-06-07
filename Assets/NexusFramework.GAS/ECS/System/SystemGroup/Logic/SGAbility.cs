using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SGLogic))]
    [UpdateAfter(typeof(SGlobalTimer))]
    public partial class SGAbility : ComponentSystemGroup
    {
    }
}