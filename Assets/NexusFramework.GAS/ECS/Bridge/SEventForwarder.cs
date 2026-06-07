using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct SEventForwarder : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            GASInternalBridge.Drain();
        }
    }
}
