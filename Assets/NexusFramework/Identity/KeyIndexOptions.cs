namespace NexusFramework.Identity
{
    /// <summary>Key-Id 索引器的创建选项。</summary>
    public readonly struct KeyIndexOptions
    {
        public KeyIndexOptions(bool requireNamespace = false, int maxValue = 0, bool parseNamespace = true)
        {
            RequireNamespace = requireNamespace;
            MaxValue = maxValue;
            // 用「关闭」而非「开启」位存储：default(KeyIndexOptions) 必须等价于
            // 默认行为（解析命名空间），否则 `KeyIndex(name, options = default)`
            // 会与 `new KeyIndexOptions()` 产生不同语义。
            DisableNamespaceParsing = !(parseNamespace || requireNamespace);
        }

        /// <summary>是否要求键带命名空间（<c>namespace:path</c>）。</summary>
        public bool RequireNamespace { get; }

        /// <summary>
        ///     是否关闭 <see cref="IdKey" /> 的 <c>namespace:path</c> 解析。
        ///     关闭后只做首尾空白裁剪，冒号按普通字符处理。
        ///     供 Tag 这类自有点分层规则、键中可能含冒号的索引器使用。
        /// </summary>
        public bool DisableNamespaceParsing { get; }

        /// <summary><see cref="DisableNamespaceParsing" /> 的取反，便于阅读。</summary>
        public bool ParseNamespace => !DisableNamespaceParsing;

        /// <summary>id 上限；0 表示 <see cref="int.MaxValue" />。</summary>
        public int MaxValue { get; }
    }
}
