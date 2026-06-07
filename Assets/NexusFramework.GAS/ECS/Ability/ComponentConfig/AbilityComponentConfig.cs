using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public abstract class AbilityComponentConfig
    {
        protected static EntityManager _entityManager;
        
        public static void SetEntityManager(EntityManager em)
        {
            _entityManager = em;
        }
        
        public abstract void LoadToGameplayAbilityEntity(Entity ability);
    }
}
