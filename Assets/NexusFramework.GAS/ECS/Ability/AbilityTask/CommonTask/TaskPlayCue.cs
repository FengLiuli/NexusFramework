using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    ///     在片段起点对所属 ASC 播放一个 GameplayCue。
    ///     任务结束时停止并销毁 Cue 实体（与片段区间对齐）。
    /// </summary>
    public class TaskPlayCue : AbilityTaskBase<XParamCue>
    {
        private Entity _cueEntity = Entity.Null;

        public TaskPlayCue(AbilityLogicBase logic) : base(logic)
        {
        }

        protected override void OnBegin(int startFrame)
        {
            if (Parameter == null)
            {
                Debug.LogError("[NF.GAS] TaskPlayCue 参数为空");
                return;
            }

            var owner = OwnerEntity;
            if (owner == Entity.Null)
            {
                Debug.LogError("[NF.GAS] TaskPlayCue 无法解析所属 ASC");
                return;
            }

            var em = EntityManager;
            var config = Parameter.GetCueConfig();
            if (config?.CueType == null)
            {
                Debug.LogError("[NF.GAS] TaskPlayCue 未能解析 Cue 配置");
                return;
            }

            _cueEntity = CueHelper.CreateCueEntity(em, config, owner);
            if (_cueEntity == Entity.Null)
                return;

            CueHelper.PlayCue(em, _cueEntity);
        }

        protected override void OnFinish(int endFrame)
        {
            if (_cueEntity == Entity.Null) return;

            var em = EntityManager;
            if (em.Exists(_cueEntity))
            {
                CueHelper.StopCue(em, _cueEntity);
                em.AddComponent<ECKillCue>(_cueEntity);
            }

            _cueEntity = Entity.Null;
        }

        public override void Dispose()
        {
            OnFinish(0);
        }
    }
}
