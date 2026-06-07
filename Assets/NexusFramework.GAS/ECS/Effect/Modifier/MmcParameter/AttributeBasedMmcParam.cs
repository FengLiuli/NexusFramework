using System;  
using System.Collections.Generic;
using NexusFramework.GAS.Config;

namespace NexusFramework.GAS.ECS
{
    public class AttributeBasedMmcParam : XParam
    {
        [BeanField(nameof(SetAttrSetCode), Comment = "属性集Code", Order = 1)]
        public int AttrSetCode { get; private set; }

        [BeanField(nameof(SetAttrCode), Comment = "属性Code", Order = 2)]
        public int AttrCode { get; private set; }

        [BeanField(nameof(SetFromType), LubanType = "int", Order = 3)]  
        public AttributeFromType FromType { get; private set; }

        [BeanField(nameof(SetCaptureType), LubanType = "int", Order = 4)]  
        public AttributeCaptureType CaptureType { get; private set; }

        [BeanField(nameof(SetK), Order = 5)]  
        public float K { get; private set; } = 1f;

        [BeanField(nameof(SetB), Order = 6)]  
        public float B { get; private set; } = 0f;

        public void SetAttrSetCode(int v) => AttrSetCode = v;
        public void SetAttrCode(int v) => AttrCode = v;
        public void SetFromType(int v) => FromType = (AttributeFromType)v;
        public void SetCaptureType(int v) => CaptureType = (AttributeCaptureType)v;
        public void SetK(float v) => K = v;
        public void SetB(float v) => B = v;

        private static IAttributeValueResolver _resolver;

        public static IAttributeValueResolver GetResolver() => _resolver ??= new DefaultAttributeValueResolver();

#if UNITY_EDITOR
        public List<object> EncodeExcelData() => new List<object>
            { AttrSetCode, AttrCode, (int)FromType, (int)CaptureType, K, B };

        public void DecodeExcelData(List<object> data)
        {
            if (data == null || data.Count < 6) return;
            AttrSetCode = Convert.ToInt32(data[0]);
            AttrCode = Convert.ToInt32(data[1]);
            FromType = (AttributeFromType)Convert.ToInt32(data[2]);
            CaptureType = (AttributeCaptureType)Convert.ToInt32(data[3]);
            K = Convert.ToSingle(data[4]);
            B = Convert.ToSingle(data[5]);
        }
#endif
    }
}