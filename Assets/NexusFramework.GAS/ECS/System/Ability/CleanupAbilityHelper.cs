using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    internal static class CleanupAbilityHelper
    {
        public static void DisposeAllAbilityNativeArrays(EntityManager em, Entity abilityEntity)
        {
            if (!em.Exists(abilityEntity)) return;

            DisposeIfExists<CAbilityCooldown>(em, abilityEntity, c => c.CooldownTags);
            DisposeIfExists<CAbilityAssetTags>(em, abilityEntity, c => c.tags);
            DisposeIfExists<CBlockAbilityWithTags>(em, abilityEntity, c => c.tags);
            DisposeIfExists<CCancelAbilityWithTags>(em, abilityEntity, c => c.tags);
            DisposeIfExists<CAbilityActivationOwnedTags>(em, abilityEntity, c => c.tags);

            DisposeTagRequirementIfExists<CAbilityActivationRequiredTags>(em, abilityEntity, c => c.requirement);
            DisposeTagRequirementIfExists<CAbilityActivationBlockedTags>(em, abilityEntity, c => c.requirement);
        }

        private static void DisposeIfExists<T>(EntityManager em, Entity e, System.Func<T, NativeArray<int>> selector)
            where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(e))
            {
                var arr = selector(em.GetComponentData<T>(e));
                if (arr.IsCreated) arr.Dispose();
            }
        }

        private static void DisposeTagRequirementIfExists<T>(EntityManager em, Entity e,
            System.Func<T, TagRequirementData> selector) where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(e))
            {
                var req = selector(em.GetComponentData<T>(e));
                if (req.all.IsCreated) req.all.Dispose();
                if (req.any.IsCreated) req.any.Dispose();
                if (req.none.IsCreated) req.none.Dispose();
            }
        }
    }
}
