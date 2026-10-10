namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     Carrier 索引表契约。
    ///     索引表以「表名」标识业务语义（而非 C# 类型），由 <see cref="ICarrierManager" /> 托管生命周期：
    ///     载体销毁时，管理器会回调 <see cref="OnCarrierRemoved" />，
    ///     清除该载体在索引中的**全部角色**（作为成员、key、或被引用的 value）。
    /// </summary>
    public interface ICarrierIndex
    {
        /// <summary>
        ///     表名。同一 Architecture 内唯一，标识索引的业务语义。
        /// </summary>
        string Name { get; }

        /// <summary>
        ///     移除该载体在本索引中的所有条目（销毁清理入口）。
        /// </summary>
        void OnCarrierRemoved(CarrierId carrier);

        /// <summary>
        ///     本索引当前承载的条目数（诊断用）。
        /// </summary>
        int EntryCount { get; }
    }

    /// <summary>
    ///     可选能力：索引在接受 <see cref="ICarrierManager.RegisterIndex" /> 注册时被注入 owner，
    ///     以便在写入前校验载体有效性（避免绑定到已销毁载体而留下永不被清理的孤儿条目）。
    ///     不实现本接口的索引不做校验，需业务自行保证「先建载体再绑定」。
    /// </summary>
    internal interface ICarrierIndexOwner
    {
        void SetOwner(ICarrierManager owner);
    }
}
