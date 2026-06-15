using System;
using System.Collections.Generic;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public static class AbilityLogicFactory
    {
        private static readonly Dictionary<string, Type> _logicTypes = new();
        private static readonly Dictionary<string, Type> _logicParamTypes = new();
        private static readonly Dictionary<string, Type> _taskParamTypes = new();
        internal static EntityManager _entityManager;

        public static void SetEntityManager(EntityManager em) => _entityManager = em;

        public static void Register(string typeName, Type logicType)
        {
            _logicTypes[typeName] = logicType;
        }

        public static void RegisterAbilityLogicParam(string typeName, Type paramType)
        {
            _logicParamTypes[typeName] = paramType;
        }

        public static Type GetAbilityLogicParamType(string typeName)
        {
            _logicParamTypes.TryGetValue(typeName, out var type);
            return type;
        }

        public static void RegisterAbilityTaskParam(string typeName, Type paramType)
        {
            _taskParamTypes[typeName] = paramType;
        }

        public static Type GetAbilityTaskParamType(string typeName)
        {
            _taskParamTypes.TryGetValue(typeName, out var type);
            return type;
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
