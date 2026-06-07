using System.Collections.Generic;

namespace NexusFramework.GAS.ECS
{
    public class XParamNone:XParam
    {
        public XParamNone()
        {
        }

#if UNITY_EDITOR
        public void DecodeExcelData(List<object> paramData)
        {
        }

        public List<object> EncodeExcelData()
        {
            return new List<object>();
        } 
#endif
    }
}