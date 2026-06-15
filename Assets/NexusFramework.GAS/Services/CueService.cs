using System;
using System.Linq;
using System.Reflection;
using NexusFramework;
using NexusFramework.GAS.Config;
using NexusFramework.GAS.ECS;

namespace NexusFramework.GAS.Services
{
    public class CueService : AbstractService
    {
        protected override void OnInit()
        {
            ScanAndRegisterAll();
        }

        protected override void OnDeinit() { }

        /// <summary>手动注册单个 Cue 类型</summary>
        public void RegisterCueType(string typeName, Type logicType, Type paramType)
        {
            CueHelper.RegisterCue(typeName, logicType, paramType);
        }

        /// <summary>手动注册单个 Cue 类型（泛型版本）</summary>
        public void RegisterCueType<T>(string typeName, Type paramType) where T : GameplayCueBase
        {
            CueHelper.RegisterCue<T>(typeName, paramType);
        }

        /// <summary>自动扫描 Architecture 所在程序集中所有 GameplayCueBase / ModMagnitudeCalculationBase 子类并注册</summary>
        public void ScanAndRegisterAll()
        {
            var assembly = Architecture.GetType().Assembly;
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract) continue;

                if (typeof(GameplayCueBase).IsAssignableFrom(type))
                {
                    var paramType = InferParamType(type, typeof(GameplayCueBase<>));
                    if (paramType != null) RegisterCueType(type.Name, type, paramType);
                }

                if (typeof(ModMagnitudeCalculationBase).IsAssignableFrom(type))
                {
                    GeneralGasChoiceHelper.RegisterMmcType(type);
                    var paramType = InferParamType(type, typeof(ModMagnitudeCalculationBase<>));
                    if (paramType != null)
                        GasMmcHelper.RegisterMmc(type.Name, type, paramType);
                }
            }
        }

        private static Type InferParamType(Type subType, Type genericBaseDef)
        {
            var baseType = subType.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == genericBaseDef)
                    return baseType.GetGenericArguments()[0];
                baseType = baseType.BaseType;
            }
            return null;
        }
    }
}
