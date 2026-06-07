using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public abstract class GameplayEffectComponentConfig
    {
        protected static EntityManager _entityManager;
        
        public static void SetEntityManager(EntityManager em)
        {
            _entityManager = em;
        }
        
        public abstract void LoadToGameplayEffectEntity(Entity ge);
    }
}
