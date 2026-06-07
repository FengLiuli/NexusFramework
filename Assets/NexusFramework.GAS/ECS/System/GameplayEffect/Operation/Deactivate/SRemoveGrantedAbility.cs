// 文件: Assets/GAS/Runtime/System/GameplayEffect/Operation/Deactivate/SRemoveGrantedAbility.cs  
using Unity.Burst;  
using Unity.Entities;  
  
namespace NexusFramework.GAS.ECS  
{  
    [UpdateInGroup(typeof(SGDeactivateEffect))]  
    [UpdateBefore(typeof(SDeactivateEnd))]  
    [DisableAutoCreation]
    public partial struct SRemoveGrantedAbility : ISystem  
    {  
        [BurstCompile]  
        public void OnCreate(ref SystemState state)  
        {  
            state.RequireForUpdate<WipDeactivateEffect>();  
            state.RequireForUpdate<CEffectInstance>();  
            state.RequireForUpdate<MCGrantedAbility>();  
            state.RequireForUpdate<CEffectInUsage>();  
        }  
  
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            foreach (var (_, _, grantedAbilityComp, inUsage, ge) in
                     SystemAPI.Query<
                         RefRO<CEffectInstance>,
                         RefRO<WipDeactivateEffect>,
                         MCGrantedAbility,
                         RefRO<CEffectInUsage>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasComponent<MCGrantedAbilityRuntime>(ge)) continue;

                var runtime = state.EntityManager.GetComponentData<MCGrantedAbilityRuntime>(ge);
                if (runtime.GrantedAbilityEntities == null) continue;

                var grantedAbilities = grantedAbilityComp.GrantedAbilities;

                for (int i = 0; i < grantedAbilities.Length; i++)
                {
                    if (grantedAbilities[i].DeactivationPolicy != GrantedAbilityDeactivationPolicy.SyncWithEffect)
                        continue;

                    var abilityEntity = runtime.GrantedAbilityEntities[i];
                    if (abilityEntity == Entity.Null) continue;
                    if (!state.EntityManager.HasComponent<CAbilityActive>(abilityEntity)) continue;

                    ecb.AddComponent<CAbilityInTryCancel>(abilityEntity);
                }
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
  
        [BurstCompile]  
        public void OnDestroy(ref SystemState state)  
        {  
        }  
    }  
}
