using System.Collections.Generic;
using NexusFramework.GAS.Config;
using NexusFramework.GAS.Models;
using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    ///     在片段起点捕获目标并对每个目标施加一组 GameplayEffect。
    /// </summary>
    public class TaskApplyEffects : AbilityTaskBase<XParamApplyEffects>
    {
        private List<Entity> _catchResults = new();
        private TargetCatcherBase _catcher;

        public TaskApplyEffects(AbilityLogicBase logic) : base(logic)
        {
        }

        public override void InitParameters(XParam parameter)
        {
            base.InitParameters(parameter);
            if (Parameter == null || string.IsNullOrEmpty(Parameter.CatcherType))
                return;

            // 注入解析器使 CatchAreaBox3D 之类的空间捕获可用
            var resolver = Logic.Architecture.GetModel<GASEntityMapModel>();
            _catcher = TargetCatcherHelper.TryCreateTargetCatcher(Parameter.CatcherType, resolver);

            if (_catcher != null && Parameter.Param != null)
                _catcher.InitParameters(Parameter.Param);
        }

        protected override void OnBegin(int startFrame)
        {
            if (_catcher == null)
            {
                Debug.LogError($"[NF.GAS] TaskApplyEffects 未能创建目标捕获器: '{Parameter?.CatcherType}'");
                return;
            }

            if (Parameter?.IDs == null || Parameter.IDs.Length == 0)
                return;

            var owner = OwnerEntity;
            if (owner == Entity.Null)
            {
                Debug.LogError("[NF.GAS] TaskApplyEffects 无法解析所属 ASC");
                return;
            }

            _catcher.Init(owner);
            _catcher.CatchTargetsNonAllocSafe(owner, ref _catchResults);

            var configModel = Logic.Architecture.GetModel<ConfigModel>();

            foreach (var target in _catchResults)
            foreach (var effectId in Parameter.IDs)
            {
                var configs = configModel.GetGameplayEffectConfig(effectId);
                if (configs == null || configs.Length == 0)
                {
                    Debug.LogError($"[NF.GAS] TaskApplyEffects 找不到效果配置: ID={effectId}");
                    continue;
                }

                // 复用技能逻辑的受保护辅助方法，避免 Task 直接操作 ECS
                var ge = Logic.CreateGameplayEffectEntity(configs);
                Logic.ApplyGameplayEffectTo(ge, target, owner);
            }
        }

        public override void Dispose()
        {
            _catchResults.Clear();
            _catcher = null;
        }
    }
}
