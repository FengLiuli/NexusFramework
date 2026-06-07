using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    [BurstCompile]
    public static class AttributeDataExtensions
    {
        public static int IndexOfAttrCode(this NativeArray<CAttributeData> attrs, int attrCode)
        {
            for (var i = 0; i < attrs.Length; i++)
                if (attrs[i].Code == attrCode)
                    return i;
            return -1;
        }
    }
}
