namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     CarrierManager 对外发送事件的回调接口。
    ///     由 Architecture 在初始化时注入，避免 Manager 直接依赖事件系统。
    /// </summary>
    public interface ICarrierEventCallback
    {
        void OnTagAdded(CarrierId carrier, string key);

        void OnTagRemoved(CarrierId carrier, string key);

        /// <summary>
        ///     载体已被销毁且索引已清理。由 CarrierManager.DestroyCarrier 在锁外触发，
        ///     接收方负责清理自身持有的该载体相关状态。
        /// </summary>
        void OnCarrierDestroyed(CarrierId carrier);
    }
}
