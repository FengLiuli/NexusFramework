namespace NexusFramework.Identity
{
    /// <summary>
    ///     命名空间归属校验的可注入来源。
    ///     内容包系统（R008）未建时可传 null；此时 <see cref="KeyIndex" /> 放行所有命名空间。
    /// </summary>
    public interface IIdNamespaceAuthority
    {
        bool IsRegistered(string namespaceId);
    }
}
