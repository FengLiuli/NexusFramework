using System.Collections.Generic;

namespace NexusFramework.GAS.ECS
{
    public class XParamMMCScalable : XParam
    {
        public XParamMMCScalable(float k, float b)
        {
            K = k;
            B = b;
        }

        public XParamMMCScalable()
        {
            K = 0;
            B = 0;
        }

        [BeanField(nameof(SetK))]
        public float K { get; private set; }

        [BeanField(nameof(SetB))]
        public float B { get; private set; }

        public void SetK(float value)
        {
            K = value;
        }

        public void SetB(float value)
        {
            B = value;
        }

#if UNITY_EDITOR
        public void DecodeExcelData(List<object> paramData)
        {
            if (paramData == null || paramData.Count == 0)
            {
                K = 0;
                B = 0;
                return;
            }

            K = paramData[0] as float? ?? 0;

            if (paramData.Count > 1)
                B = paramData[1] as float? ?? 0;
        }

        public List<object> EncodeExcelData()
        {
            var result = new List<object> { K, B };
            return result;
        }
#endif
    }
}