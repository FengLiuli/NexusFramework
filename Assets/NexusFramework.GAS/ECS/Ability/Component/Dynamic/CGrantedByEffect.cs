using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CGrantedByEffect : IComponentData  
    {  
        public Entity SourceEffect;                              // 来源GE Entity  
        public GrantedAbilityActivationPolicy ActivationPolicy;  
        public GrantedAbilityDeactivationPolicy DeactivationPolicy;  
        public GrantedAbilityRemovePolicy RemovePolicy;  
    }
}