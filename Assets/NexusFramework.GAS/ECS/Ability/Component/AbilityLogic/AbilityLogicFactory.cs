using System;
using System.Collections.Generic;
using Unity.Entities;
using NexusFramework;

namespace NexusFramework.GAS.ECS
{
    public static class AbilityLogicFactory
    {
        private static readonly Dictionary<string, Type> _logicTypes = new();
        private static readonly Dictionary<string, Type> _logicParamTypes = new();
        private static readonly Dictionary<string, Type> _taskParamTypes = new();
        internal static EntityManager _entityManager;
        internal static IArchitecture _architecture;

        public static void SetEntityManager(EntityManager em) => _entityManager = em;
        public static void SetArchitecture(IArchitecture architecture) => _architecture = architecture;

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
                // 优先使用 (Entity, IArchitecture) 构造函数 —— 可以正确设置 Architecture 属性
                // 使得 ICanGetService / ICanGetModel 扩展方法正常工作
                if (_architecture != null)
                {
                    var archCtor = type.GetConstructor(new[] { typeof(Entity), typeof(IArchitecture) });
                    if (archCtor != null)
                        return (AbilityLogicBase)Activator.CreateInstance(type, ability, _architecture);
                }

                // 回退到 (Entity, EntityManager) 构造函数（兼容 ECS 路径）
                return (AbilityLogicBase)Activator.CreateInstance(type, ability, _entityManager);
            }
            return null;
        }
    }
}
