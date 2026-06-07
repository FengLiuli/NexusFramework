using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    internal static class GEConfigHelper
    {
        public static Entity CreateGameplayEffectEntity(EntityManager em, GameplayEffectComponentConfig[] configs)
        {
            GameplayEffectComponentConfig.SetEntityManager(em);
            var entity = em.CreateEntity();
            foreach (var config in configs)
                config.LoadToGameplayEffectEntity(entity);
            return entity;
        }
    }
}
