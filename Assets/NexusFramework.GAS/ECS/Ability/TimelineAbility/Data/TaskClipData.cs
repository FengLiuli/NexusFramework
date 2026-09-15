using System;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    ///     时间轴上的一个片段：在 [StartTime, EndTime] 帧区间内驱动一个 <see cref="AbilityTaskBase"/>。
    /// </summary>
    public class TaskClipData
    {
        public string Name;

        public int StartTime;

        public int EndTime;

        /// <summary>Task 的运行时类型名（如 "TaskApplyEffects"），用于工厂创建。</summary>
        public string TaskType;

        public XParam Parameter;

        public int Duration => EndTime - StartTime;

        public void SetParameter(XParam parameter)
        {
            Parameter = parameter;
        }

        public AbilityTaskBase InstantiateTask(AbilityLogicBase logic)
        {
            var task = AbilityLogicFactory.TryCreateAbilityTask(TaskType, logic);
            if (task == null)
            {
                UnityEngine.Debug.LogError(
                    $"[NF.GAS] 时间轴片段 '{Name}' 的任务类型 '{TaskType}' 未注册或创建失败");
                return null;
            }

            task.InitParameters(Parameter);
            return task;
        }
    }
}
