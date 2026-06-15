using System;
using System.Collections.Generic;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public static class GasMmcHelper
    {
        private static readonly Dictionary<string, Type> _mmcTypes = new();
        private static readonly Dictionary<string, Type> _mmcParamTypes = new();

        public static void RegisterMmc(string sType, Type mmcType, Type paramType)
        {
            _mmcTypes[sType] = mmcType;
            _mmcParamTypes[sType] = paramType;
        }

        public static Type GetMmcType(string sType)
        {
            _mmcTypes.TryGetValue(sType, out var type);
            return type;
        }

        public static Type GetMmcParamType(string sType)
        {
            _mmcParamTypes.TryGetValue(sType, out var type);
            return type;
        }

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
