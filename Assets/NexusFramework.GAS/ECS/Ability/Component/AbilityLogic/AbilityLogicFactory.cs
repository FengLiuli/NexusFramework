using System;
using System.Collections.Generic;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public static class AbilityLogicFactory
    {
        private static readonly Dictionary<string, Type> _logicTypes = new();
        internal static EntityManager _entityManager;

        public static void SetEntityManager(EntityManager em) => _entityManager = em;

        public static void Register(string typeName, Type logicType)
        {
            _logicTypes[typeName] = logicType;
        }

        public static AbilityLogicBase TryCreateAbilityLogic(string typeName, Entity ability)
        {
            if (_logicTypes.TryGetValue(typeName, out var type))
            {
                return (AbilityLogicBase)Activator.CreateInstance(type, ability, _entityManager);
            }
            return null;
        }
    }
}
