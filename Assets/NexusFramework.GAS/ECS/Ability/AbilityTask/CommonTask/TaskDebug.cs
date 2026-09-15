using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    /// <summary>在片段起点打一条日志。用于验证时间轴的帧调度。</summary>
    public class TaskDebug : AbilityTaskBase<XParamString>
    {
        public TaskDebug(AbilityLogicBase logic) : base(logic)
        {
        }

        protected override void OnBegin(int startFrame)
        {
            Debug.Log($"[NF.GAS][Timeline] 帧 {startFrame}: {Parameter?.Value}");
        }
    }
}
