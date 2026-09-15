using System.Collections.Generic;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    ///     时间轴技能的参数：指向 <c>exgas.timelineAbility</c> 表中的一条时间轴定义。
    /// </summary>
    public class XParamALTimelineID : XParam
    {
        [BeanField(nameof(SetID), Comment = "时间轴 ID")]
        public int ID { get; private set; }

        public XParamALTimelineID()
        {
        }

        public XParamALTimelineID(int id)
        {
            ID = id;
        }

        public void SetID(int id)
        {
            ID = id;
        }

#if UNITY_EDITOR
        public void DecodeExcelData(List<object> paramData)
        {
            ID = 0;
            if (paramData == null || paramData.Count == 0) return;

            var strData = paramData[0]?.ToString();
            if (!string.IsNullOrEmpty(strData) && int.TryParse(strData, out var id))
                ID = id;
        }

        public List<object> EncodeExcelData()
        {
            return new List<object> { ID.ToString() };
        }
#endif
    }
}
