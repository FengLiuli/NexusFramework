namespace NexusFramework.GAS.ECS
{
    /// <summary>占位任务，不做任何事。用于时间轴上留白或调试。</summary>
    public class TaskDoNothing : AbilityTaskBase<XParamNone>
    {
        public TaskDoNothing(AbilityLogicBase logic) : base(logic)
        {
        }
    }
}
