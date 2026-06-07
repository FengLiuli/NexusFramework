using NexusFramework.GAS.ECS;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public class MCGrantedAbility:IComponentData
    {
        public GrantedAbility[] GrantedAbilities;
        
        public MCGrantedAbility(GrantedAbility[] grantedAbilities)
        {
            GrantedAbilities = grantedAbilities;
        }
        
        public MCGrantedAbility()
        {
        }
    }
    
    public struct GrantedAbility
    {
        public AbilityConfig AbilityConfig;
        public int Level;
        public GrantedAbilityActivationPolicy ActivationPolicy;
        public GrantedAbilityDeactivationPolicy DeactivationPolicy;
        public GrantedAbilityRemovePolicy RemovePolicy;
    }
    
    public sealed class MCConfGrantedAbility : GameplayEffectComponentConfig
    {
        public GrantedAbility[] GrantedAbilities;
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            _entityManager.AddComponent<MCGrantedAbility>(ge);
            _entityManager.SetComponentData(ge, new MCGrantedAbility
            {
                GrantedAbilities = GrantedAbilities
            });
        }
    }
}