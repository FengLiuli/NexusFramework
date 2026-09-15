namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     载体被销毁后广播。索引清理先于本事件完成，订阅者不会看到已销毁的载体。
    /// </summary>
    public struct CarrierDestroyedEvent
    {
        public CarrierId Carrier;
    }
}
