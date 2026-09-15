using System.Collections.Generic;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    ///     时间轴的一条轨道，承载若干按时间排布的片段。
    /// </summary>
    public class Track
    {
        public string Name { get; set; }

        public List<TaskClipData> TaskClips { get; set; } = new();
    }
}
