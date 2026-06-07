using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public static class GasMmcHelper
    {
        public static float Calculate(EntityManager entityManager, Entity ge, EffectModifier modifier, float sourceValue)
        {
            var geData = entityManager.GetComponentData<CEffectInUsage>(ge);
            var context = new MmcContext
            {
                EffectEntity = ge,
                Source = geData.Source,
                Target = geData.Target
            };
            return modifier.Apply(context, sourceValue);
        }

        public static float Calculate(EntityManager entityManager, Entity ge, EffectModifier modifier, float sourceValue, Entity sourceAsc, Entity targetAsc)
        {
            var context = new MmcContext
            {
                EffectEntity = ge,
                Source = sourceAsc,
                Target = targetAsc
            };
            return modifier.Apply(context, sourceValue);
        }
    }
}
