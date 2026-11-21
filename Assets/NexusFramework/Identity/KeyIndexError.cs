namespace NexusFramework.Identity
{
    /// <summary>Key-Id 索引器的失败原因。</summary>
    public enum KeyIndexError
    {
        None,
        InvalidKey,
        MissingNamespace,
        NamespaceNotRegistered,
        CapacityExceeded,
        IdOutOfRange,
        /// <summary>显式注册时 key 或 id 已被其它绑定占用。</summary>
        IdConflict
    }
}
