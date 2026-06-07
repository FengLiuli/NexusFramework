using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    public struct BTemporaryTag: IBufferElementData
    {
        public int tag;
        public Entity source;
    }
}