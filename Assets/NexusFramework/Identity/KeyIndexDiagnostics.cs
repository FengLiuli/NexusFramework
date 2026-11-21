namespace NexusFramework.Identity
{
    /// <summary>Key-Id 索引器的诊断快照。</summary>
    public readonly struct KeyIndexDiagnostics
    {
        public KeyIndexDiagnostics(string name, int count, int capacity, int invalidKeyCount,
            int namespaceRejectedCount, int authorityUnavailableCount, int capacityRejectedCount)
        {
            Name = name;
            Count = count;
            Capacity = capacity;
            InvalidKeyCount = invalidKeyCount;
            NamespaceRejectedCount = namespaceRejectedCount;
            AuthorityUnavailableCount = authorityUnavailableCount;
            CapacityRejectedCount = capacityRejectedCount;
        }

        public string Name { get; }
        public int Count { get; }
        public int Capacity { get; }
        public int InvalidKeyCount { get; }

        /// <summary>因命名空间未注册而被拒绝的次数。</summary>
        public int NamespaceRejectedCount { get; }

        /// <summary>
        ///     <see cref="KeyIndex" /> 要求命名空间校验、但未注入 authority 而放行的次数。
        ///     大于 0 说明该索引的命名空间归属未被真正校验。
        /// </summary>
        public int AuthorityUnavailableCount { get; }

        public int CapacityRejectedCount { get; }
    }
}
