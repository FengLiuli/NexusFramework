using Unity.Collections;
using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct TagRequirementData
    {
        public NativeArray<int> all;
        public NativeArray<int> any;
        public NativeArray<int> none;
    }
}
