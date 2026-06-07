using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class GASManagerInputSystem : SystemBase
    {
        private Entity _managerEntity;

        protected override void OnCreate()
        {
            _managerEntity = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntity();
        }

        protected override void OnUpdate()
        {
            if (!EntityManager.HasComponent<CGASRunningTag>(_managerEntity))
                EntityManager.AddComponent<CGASRunningTag>(_managerEntity);
        }
    }
}