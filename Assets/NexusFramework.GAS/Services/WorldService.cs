using Unity.Entities;
using UnityEngine;
using NexusFramework;
using NexusFramework.GAS.ECS;

namespace NexusFramework.GAS.Services
{
    public class WorldService : AbstractService
    {
        public World ExWorld { get; private set; }
        public EntityManager EntityManager { get; private set; }

        private bool _running;

        protected override void OnInit()
        {
            ExWorld = new World("GAS_World");
            EntityManager = ExWorld.EntityManager;
            CreateSystems();
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(ExWorld);
        }

        public void Run()
        {
            _running = true;
        }

        public void Stop()
        {
            _running = false;
        }

        public bool IsRunning => _running;
        public bool IsInitialized => ExWorld != null && ExWorld.IsCreated;

        internal void SetupGASEntity(Entity entity)
        {
            EntityManager.AddBuffer<BEAttrSet>(entity);
            EntityManager.AddBuffer<BGameplayEffect>(entity);
            EntityManager.AddBuffer<BAbility>(entity);
            EntityManager.AddBuffer<BFixedTag>(entity);
            EntityManager.AddBuffer<BTemporaryTag>(entity);
            EntityManager.AddComponent<CAscBasicData>(entity);
        }

        private void CreateSystems()
        {
            ExWorld.CreateSystemManaged<InitializationSystemGroup>();
            var sgSimulation = ExWorld.CreateSystemManaged<SimulationSystemGroup>();
            ExWorld.CreateSystemManaged<PresentationSystemGroup>();
            var sgFixedStepSimulation = ExWorld.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            sgFixedStepSimulation.RateManager = new RateUtils.FixedRateSimpleManager(Time.fixedDeltaTime);
            sgSimulation.AddSystemToUpdateList(sgFixedStepSimulation);

            var sgLogic = ExWorld.CreateSystemManaged<SGLogic>();
            sgFixedStepSimulation.AddSystemToUpdateList(sgLogic);

            var sgAbility = ExWorld.CreateSystemManaged<SGAbility>();
            var sgAttribute = ExWorld.CreateSystemManaged<SGAttribute>();
            var sgEffect = ExWorld.CreateSystemManaged<SGEffect>();
            sgLogic.AddSystemToUpdateList(ExWorld.CreateSystem<SGlobalTimer>());
            sgLogic.AddSystemToUpdateList(sgAbility);
            sgLogic.AddSystemToUpdateList(sgAttribute);
            sgLogic.AddSystemToUpdateList(sgEffect);
            sgLogic.SortSystems();

            sgAbility.AddSystemToUpdateList(ExWorld.CreateSystem<STryActivateAbility>());
            sgAbility.AddSystemToUpdateList(ExWorld.CreateSystem<STryCancelAbility>());
            sgAbility.AddSystemToUpdateList(ExWorld.CreateSystem<STryEndAbility>());
            sgAbility.AddSystemToUpdateList(ExWorld.CreateSystem<SAbilityTick>());
            sgAbility.SortSystems();

            sgAttribute.AddSystemToUpdateList(ExWorld.CreateSystem<SUpdateAttributeCurrentValue>());
            sgAttribute.SortSystems();

            var sgEffectCreate = ExWorld.CreateSystemManaged<SGEffectCreate>();
            var sgEffectOperation = ExWorld.CreateSystemManaged<SGEffectOperation>();
            var sgEffectDestroy = ExWorld.CreateSystemManaged<SGEffectDestroy>();
            var sgEffectTick = ExWorld.CreateSystemManaged<SGEffectTick>();
            sgEffect.AddSystemToUpdateList(sgEffectCreate);
            sgEffect.AddSystemToUpdateList(sgEffectOperation);
            sgEffect.AddSystemToUpdateList(sgEffectDestroy);
            sgEffect.AddSystemToUpdateList(sgEffectTick);
            sgEffect.SortSystems();

            var sgInstantiateEffect = ExWorld.CreateSystemManaged<SGInstantiateEffect>();
            sgInstantiateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SInstantiateEffect>());
            sgInstantiateEffect.SortSystems();
            sgEffectCreate.AddSystemToUpdateList(sgInstantiateEffect);
            sgEffectCreate.SortSystems();

            var sgCheckApplyEffect = ExWorld.CreateSystemManaged<SGCheckApplyEffect>();
            sgCheckApplyEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SCheckApplicationRequiredTags>());
            sgCheckApplyEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SCheckImmunityTags>());
            sgCheckApplyEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SCheckApplyEnd>());
            sgCheckApplyEffect.SortSystems();

            var sgApplyEffect = ExWorld.CreateSystemManaged<SGApplyEffect>();
            // 所有系统一次性添加，最后统一排序
            sgApplyEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SPlayCueOnApply>());
            sgApplyEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SRemoveEffectWithTags>());
            sgApplyEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SApplyEnd>());
            sgApplyEffect.SortSystems();

            var sgCheckActivateEffect = ExWorld.CreateSystemManaged<SGCheckActivateEffect>();
            sgCheckActivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SCheckEffectActive>());
            sgCheckActivateEffect.SortSystems();

            var sgActivateEffect = ExWorld.CreateSystemManaged<SGActivateEffect>();
            sgActivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SActivateEnd>());
            sgActivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SAddGrantedAbility>());
            sgActivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SAddModifiers>());
            sgActivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SEffectAddGrantedTags>());
            sgActivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SPlayCueOnActivate>());
            sgActivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SSetEffectActive>());
            sgActivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SPlayCueOnTick>());
            sgActivateEffect.SortSystems();

            var sgDeactivateEffect = ExWorld.CreateSystemManaged<SGDeactivateEffect>();
            sgDeactivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SDeactivateEnd>());
            sgDeactivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SRemoveGrantedAbility>());
            sgDeactivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SRemoveModifiers>());
            sgDeactivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SEffectRemoveGrantedTags>());
            sgDeactivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SPlayCueOnDeactivate>());
            sgDeactivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SSetEffectDeactive>());
            sgDeactivateEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SStopCueOnTick>());
            sgDeactivateEffect.SortSystems();

            var sgRemoveEffect = ExWorld.CreateSystemManaged<SGRemoveEffect>();
            sgRemoveEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SEffectRemoveEnd>());
            sgRemoveEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SPlayCueOnRemove>());
            sgRemoveEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SRemoveEffectFromAscBuffList>());
            sgRemoveEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SRemoveGrantedAbilityOnRemove>());
            sgRemoveEffect.SortSystems();

            sgEffectOperation.AddSystemToUpdateList(sgCheckApplyEffect);
            sgEffectOperation.AddSystemToUpdateList(sgApplyEffect);
            sgEffectOperation.AddSystemToUpdateList(sgCheckActivateEffect);
            sgEffectOperation.AddSystemToUpdateList(sgActivateEffect);
            sgEffectOperation.AddSystemToUpdateList(sgDeactivateEffect);
            sgEffectOperation.AddSystemToUpdateList(sgRemoveEffect);
            sgEffectOperation.SortSystems();

            sgEffectDestroy.AddSystemToUpdateList(ExWorld.CreateSystem<SDestroyEffects>());
            sgEffectDestroy.SortSystems();

            var sgRunningEffect = ExWorld.CreateSystemManaged<SGRunningEffect>();
            sgRunningEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SEffectDurationTick>());
            sgRunningEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SEffectPeriodTick>());
            sgRunningEffect.AddSystemToUpdateList(ExWorld.CreateSystem<SEffectStackingTick>());
            sgRunningEffect.SortSystems();
            sgEffectTick.AddSystemToUpdateList(sgRunningEffect);
            sgEffectTick.SortSystems();

            var sgInstantEffectApply = ExWorld.CreateSystemManaged<SGInstantEffect>();
            sgInstantEffectApply.AddSystemToUpdateList(ExWorld.CreateSystem<SExecuteInstantEffectModifiers>());
            sgInstantEffectApply.AddSystemToUpdateList(ExWorld.CreateSystem<SExecuteInstantEffectEnd>());
            sgInstantEffectApply.SortSystems();

            var sgDurationalEffectApply = ExWorld.CreateSystemManaged<SGDurationalEffect>();
            sgDurationalEffectApply.AddSystemToUpdateList(ExWorld.CreateSystem<SAddEffectToAscBuffList>());
            sgDurationalEffectApply.AddSystemToUpdateList(ExWorld.CreateSystem<SCheckEffectStacking>());
            sgDurationalEffectApply.AddSystemToUpdateList(ExWorld.CreateSystem<SPlayCueOnAdd>());
            sgDurationalEffectApply.SortSystems();

            sgApplyEffect.AddSystemToUpdateList(sgInstantEffectApply);
            sgApplyEffect.AddSystemToUpdateList(sgDurationalEffectApply);
            sgApplyEffect.SortSystems();

            var sgDisplay = ExWorld.CreateSystemManaged<SysGrpDisplay>();
            sgDisplay.AddSystemToUpdateList(ExWorld.CreateSystem<SCueStart>());
            sgDisplay.AddSystemToUpdateList(ExWorld.CreateSystem<SCueTick>());
            sgDisplay.AddSystemToUpdateList(ExWorld.CreateSystem<SCueEnd>());
            sgDisplay.AddSystemToUpdateList(ExWorld.CreateSystem<SCueDestroy>());
            sgDisplay.AddSystemToUpdateList(ExWorld.CreateSystem<SEventForwarder>());
            sgDisplay.SortSystems();

            sgSimulation.AddSystemToUpdateList(sgDisplay);
            sgSimulation.SortSystems();
        }

        protected override void OnDeinit()
        {
            if (ExWorld is { IsCreated: true })
            {
                // 回收 SingletonGameplayTagMap 的 NativeContainer
                var q = ExWorld.EntityManager.CreateEntityQuery(
                    Unity.Entities.ComponentType.ReadOnly<ECS.SingletonGameplayTagMap>());
                var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach (var e in entities)
                {
                    if (ExWorld.EntityManager.HasComponent<ECS.SingletonGameplayTagMap>(e))
                    {
                        var map = ExWorld.EntityManager.GetComponentData<ECS.SingletonGameplayTagMap>(e);
                        if (map.Map.IsCreated)
                        {
                            // 回收每个 ComGameplayTag 的子 NativeArray
                            var values = map.Map.GetValueArray(Unity.Collections.Allocator.Temp);
                            foreach (var v in values)
                            {
                                if (v.Parents.IsCreated) v.Parents.Dispose();
                                if (v.Children.IsCreated) v.Children.Dispose();
                            }
                            values.Dispose();
                            map.Map.Dispose();
                        }
                    }
                }
                entities.Dispose();
                q.Dispose();

                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(ExWorld);
                ExWorld.Dispose();
                ExWorld = null;
                EntityManager = default;
            }
        }
    }
}
