using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct CAbilityCost : IComponentData
    {
        public Entity ProtoGameplayEffectCost;
    }
    
    public sealed class ConfAbilityCost:AbilityComponentConfig
    {
        public GameplayEffectComponentConfig[] CostComponentConfigs;
        
        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.AddComponent<CAbilityCost>(ability);
            _entityManager.SetComponentData(ability, new CAbilityCost
            {
                ProtoGameplayEffectCost = GEConfigHelper.CreateGameplayEffectEntity(_entityManager, CostComponentConfigs)
            });
        }
    }
}