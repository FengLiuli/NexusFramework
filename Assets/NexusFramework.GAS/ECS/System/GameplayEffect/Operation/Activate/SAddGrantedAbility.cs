using System;
using Unity.Burst;  
using Unity.Entities;
using Unity.Collections;

namespace NexusFramework.GAS.ECS
{
    [UpdateInGroup(typeof(SGActivateEffect))]
    [UpdateBefore(typeof(SActivateEnd))]
    [DisableAutoCreation]
    public partial struct SAddGrantedAbility : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WipActivateEffect>();
            state.RequireForUpdate<CEffectInstance>();
            state.RequireForUpdate<MCGrantedAbility>();
            state.RequireForUpdate<CEffectInUsage>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var deferredAdds = new System.Collections.Generic.List<DeferredAbilityAdd>();
            var deferredActivates = new System.Collections.Generic.List<DeferredActivate>();

            foreach (var (_, _, grantedAbilityComp, inUsage, ge) in
                     SystemAPI.Query<
                         RefRO<CEffectInstance>,
                         RefRO<WipActivateEffect>,
                         MCGrantedAbility,
                         RefRO<CEffectInUsage>>().WithEntityAccess())
            {
                var targetAsc = inUsage.ValueRO.Target;
                var grantedAbilities = grantedAbilityComp.GrantedAbilities;
                if (grantedAbilities == null || grantedAbilities.Length == 0) continue;

                bool isReactivation = state.EntityManager.HasComponent<MCGrantedAbilityRuntime>(ge);

                if (!isReactivation)
                {
                    var abilityEntities = new Entity[grantedAbilities.Length];
                    for (int i = 0; i < grantedAbilities.Length; i++)
                    {
                        var ga = grantedAbilities[i];

                        // 记录创建操作
                        var deferred = new DeferredAbilityAdd
                        {
                            TargetAsc = targetAsc,
                            AbilityConfigs = ga.AbilityConfig.ComponentConfigs,
                            ActivateNow = ga.ActivationPolicy == GrantedAbilityActivationPolicy.WhenAdded
                                || ga.ActivationPolicy == GrantedAbilityActivationPolicy.SyncWithEffect
                        };
                        deferredAdds.Add(deferred);
                    }

                    ecb.AddComponent<MCGrantedAbilityRuntime>(ge);
                }
                else
                {
                    var runtime = state.EntityManager.GetComponentData<MCGrantedAbilityRuntime>(ge);
                    if (runtime.GrantedAbilityEntities == null) continue;

                    for (int i = 0; i < grantedAbilities.Length; i++)
                    {
                        if (grantedAbilities[i].ActivationPolicy != GrantedAbilityActivationPolicy.SyncWithEffect)
                            continue;

                        var abilityEntity = runtime.GrantedAbilityEntities[i];
                        if (abilityEntity == Entity.Null) continue;
                        if (state.EntityManager.HasComponent<CAbilityActive>(abilityEntity)) continue;

                        deferredActivates.Add(new DeferredActivate { AbilityEntity = abilityEntity });
                    }
                }
            }

            // 执行所有延迟创建操作
            foreach (var d in deferredAdds)
            {
                var abilityEntity = ecb.CreateEntity();
                ecb.SetName(abilityEntity, "GrantedAbility");
                GameplayEffectComponentConfig.SetEntityManager(state.EntityManager);
                AbilityLogicFactory.SetEntityManager(state.EntityManager);
                foreach (var config in d.AbilityConfigs)
                    config.LoadToGameplayAbilityEntity(abilityEntity);

                var baseInfo = state.EntityManager.GetComponentData<CAbilityBaseInfo>(abilityEntity);
                baseInfo.Owner = d.TargetAsc;
                ecb.SetComponent(abilityEntity, baseInfo);

                ecb.AppendToBuffer<BAbility>(d.TargetAsc, new BAbility { Ability = abilityEntity });

                if (d.ActivateNow)
                    ecb.AddComponent<CAbilityInTryActivate>(abilityEntity);
            }

            // 所有延迟激活
            foreach (var d in deferredActivates)
                ecb.AddComponent<CAbilityInTryActivate>(d.AbilityEntity);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        struct DeferredAbilityAdd
        {
            public Entity TargetAsc;
            public AbilityComponentConfig[] AbilityConfigs;
            public bool ActivateNow;
        }

        struct DeferredActivate
        {
            public Entity AbilityEntity;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
