using NexusFramework.DataCarrier;

namespace NexusFramework
{
    /// <summary>
    ///     Carrier 新增 Tag 后发布的事件。
    /// </summary>
    public struct CarrierTagAddedEvent
    {
        public CarrierId Carrier;
        public string Key;
    }

    /// <summary>
    ///     Carrier 移除 Tag 后发布的事件。
    /// </summary>
    public struct CarrierTagRemovedEvent
    {
        public CarrierId Carrier;
        public string Key;
    }
}
