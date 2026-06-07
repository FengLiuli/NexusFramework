using Unity.Entities;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    /// 正在应用过程中的标签
    /// Apply是个即时过程，只发生一帧。挂上这个tag component就会把ge的apply过程触发一遍
    /// apply结束会把ComInApplicationProgress移除
    /// </summary>
    public struct CInApplicationProgress : IComponentData
    {
        
    }
}