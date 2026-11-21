using System;

namespace NexusFramework.Identity
{
    /// <summary>
    ///     稳定键：&lt;namespace&gt;:&lt;path&gt;。
    ///     规范化后作为相等性判定的唯一依据。
    ///     <para>
    ///     只做校验与解析，不参与发号。冒号为唯一分隔符；<c>requireNamespace = false</c>
    ///     时无冒号的键视为 legacy：<see cref="Namespace" /> 为 null、<see cref="Path" /> 为整个键。
    ///     </para>
    /// </summary>
    public readonly struct IdKey : IEquatable<IdKey>
    {
        private IdKey(string value, string namespaceId, string path, bool isLegacy)
        {
            Value = value;
            Namespace = namespaceId;
            Path = path;
            IsLegacy = isLegacy;
        }

        /// <summary>规范化后的完整键。</summary>
        public string Value { get; }

        /// <summary>冒号前的命名空间；legacy 键为 null。</summary>
        public string Namespace { get; }

        /// <summary>冒号后的路径；legacy 键为整个键。</summary>
        public string Path { get; }

        /// <summary>是否为无命名空间的 legacy 键。</summary>
        public bool IsLegacy { get; }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        /// <summary>解析并规范化；非法取值抛 <see cref="ArgumentException" />。</summary>
        public static IdKey Parse(string value)
        {
            if (!TryParse(value, false, out var key))
                throw new ArgumentException($"Invalid id key: '{value}'.", nameof(value));
            return key;
        }

        /// <summary>
        ///     解析并规范化。空值、空段、多个冒号均失败。
        ///     <paramref name="requireNamespace" /> 为 true 时，无冒号的键被拒绝。
        /// </summary>
        public static bool TryParse(string value, bool requireNamespace, out IdKey key)
        {
            key = default;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            var colon = trimmed.IndexOf(':');
            if (colon < 0)
            {
                if (requireNamespace)
                    return false;

                key = new IdKey(trimmed, null, trimmed, true);
                return true;
            }

            // 冒号为唯一分隔符：出现第二个冒号即非法。
            if (trimmed.IndexOf(':', colon + 1) >= 0)
                return false;

            var namespaceId = trimmed.Substring(0, colon).Trim();
            var path = trimmed.Substring(colon + 1).Trim();
            if (namespaceId.Length == 0 || path.Length == 0)
                return false;

            key = new IdKey($"{namespaceId}:{path}", namespaceId, path, false);
            return true;
        }

        public bool Equals(IdKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is IdKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public static bool operator ==(IdKey left, IdKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(IdKey left, IdKey right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return Value ?? "<invalid>";
        }
    }
}
