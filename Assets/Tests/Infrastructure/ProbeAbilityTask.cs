using System.Collections.Generic;
using NexusFramework.GAS.ECS;

namespace NexusFramework.GAS.Tests
{
    /// <summary>
    ///     记录 Begin/Tick/Finish 调用帧的探针任务，用于验证时间轴的帧调度精度。
    /// </summary>
    public class ProbeAbilityTask : AbilityTaskBase<XParamNone>
    {
        public static readonly List<string> Log = new();

        public ProbeAbilityTask(AbilityLogicBase logic) : base(logic)
        {
        }

        protected override void OnBegin(int startFrame)
        {
            Log.Add($"begin:{startFrame}");
        }

        protected override void OnTick(int frameIndex)
        {
            Log.Add($"tick:{frameIndex}");
        }

        protected override void OnFinish(int endFrame)
        {
            Log.Add($"finish:{endFrame}");
        }

        public static void Clear()
        {
            Log.Clear();
        }
    }
}
