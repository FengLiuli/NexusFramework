using System.Collections.Generic;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    ///     时间轴技能的运行时数据。
    ///     由 <c>LubanConfigLoader</c> 从 <c>exgas.timelineAbility</c> 表构建，
    ///     不在运行时反序列化——因此不需要 Luban 的多态支持。
    /// </summary>
    public class XParamTimeline : XParam
    {
        public int ID { get; private set; }

        public string Name { get; private set; }

        /// <summary>技能总时长（帧）。到达后若 <see cref="ManualEndAbility"/> 为 false 则自动结束。</summary>
        public int LifeTime { get; private set; }

        /// <summary>true 表示由技能逻辑自行决定何时结束，不随时间轴走完而结束。</summary>
        public bool ManualEndAbility { get; private set; }

        public List<Track> Tracks { get; private set; } = new();

        public XParamTimeline()
        {
            Name = string.Empty;
            LifeTime = 1;
        }

        public XParamTimeline(int id, string name, int lifeTime, bool manualEndAbility, List<Track> tracks)
        {
            SetValue(id, name, lifeTime, manualEndAbility, tracks);
        }

        public void SetValue(int id, string name, int lifeTime, bool manualEndAbility, List<Track> tracks)
        {
            ID = id;
            Name = name ?? string.Empty;
            LifeTime = lifeTime;
            ManualEndAbility = manualEndAbility;
            Tracks = tracks ?? new List<Track>();
        }

#if UNITY_EDITOR
        public void DecodeExcelData(List<object> paramData)
        {
            // 时间轴数据来自独立的表，不通过 XParam 流式配置
        }

        public List<object> EncodeExcelData()
        {
            return new List<object>();
        }
#endif
    }
}
