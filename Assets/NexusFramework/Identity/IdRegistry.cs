using System;
using System.Collections.Generic;
using UnityEngine;

namespace NexusFramework.Identity
{
    /// <summary>
    ///     每架构一份的原语注册表。只做持有与生命周期，不含发号或映射逻辑。
    ///     <para>
    ///     每个域只注册一种原语；同名域以不同原语创建时返回 null 并记录错误。
    ///     </para>
    ///     <para>非线程安全；应由架构在单线程内创建与清理。</para>
    /// </summary>
    public sealed class IdRegistry
    {
        private readonly Dictionary<string, KeyIndex> mIndexes =
            new Dictionary<string, KeyIndex>(StringComparer.Ordinal);

        private readonly Dictionary<string, IdAllocator> mAllocators =
            new Dictionary<string, IdAllocator>(StringComparer.Ordinal);

        private readonly Dictionary<string, TypedAllocator> mTypedAllocators =
            new Dictionary<string, TypedAllocator>(StringComparer.Ordinal);

        private readonly IIdNamespaceAuthority mAuthority;

        public IdRegistry(IIdNamespaceAuthority authority = null)
        {
            mAuthority = authority;
        }

        public KeyIndex GetOrCreateIndex(string name, KeyIndexOptions options = default)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Domain name cannot be null or empty.", nameof(name));

            if (mIndexes.TryGetValue(name, out var existing))
            {
                // 同名域已创建且选项不同：沿用既有实例，但必须让人看见，
                // 否则调用方会误以为自己拿到了解析规则/上限不同的索引。
                if (existing.Options.RequireNamespace != options.RequireNamespace ||
                    existing.Options.ParseNamespace != options.ParseNamespace ||
                    existing.Options.MaxValue != options.MaxValue)
                {
                    Debug.LogWarning(
                        $"[IdRegistry] Domain '{name}' already exists with different KeyIndexOptions; " +
                        "the existing instance is returned unchanged.");
                }

                return existing;
            }

            if (mAllocators.ContainsKey(name) || mTypedAllocators.ContainsKey(name))
            {
                Debug.LogError($"[IdRegistry] Domain '{name}' is already registered as a different primitive.");
                return null;
            }

            var index = new KeyIndex(name, options, mAuthority);
            mIndexes[name] = index;
            return index;
        }

        public IdAllocator GetOrCreateAllocator(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Domain name cannot be null or empty.", nameof(name));

            if (mAllocators.TryGetValue(name, out var existing))
                return existing;

            if (mIndexes.ContainsKey(name) || mTypedAllocators.ContainsKey(name))
            {
                Debug.LogError($"[IdRegistry] Domain '{name}' is already registered as a different primitive.");
                return null;
            }

            var allocator = new IdAllocator();
            mAllocators[name] = allocator;
            return allocator;
        }

        public TypedAllocator GetOrCreateTypedAllocator(string name, int initialCapacity = 0)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Domain name cannot be null or empty.", nameof(name));

            if (mTypedAllocators.TryGetValue(name, out var existing))
                return existing;

            if (mIndexes.ContainsKey(name) || mAllocators.ContainsKey(name))
            {
                Debug.LogError($"[IdRegistry] Domain '{name}' is already registered as a different primitive.");
                return null;
            }

            var allocator = new TypedAllocator(name, initialCapacity);
            mTypedAllocators[name] = allocator;
            return allocator;
        }

        public bool TryGetIndex(string name, out KeyIndex index)
        {
            return mIndexes.TryGetValue(name, out index);
        }

        public bool TryGetAllocator(string name, out IdAllocator allocator)
        {
            return mAllocators.TryGetValue(name, out allocator);
        }

        public bool TryGetTypedAllocator(string name, out TypedAllocator allocator)
        {
            return mTypedAllocators.TryGetValue(name, out allocator);
        }

        public IReadOnlyCollection<string> Names
        {
            get
            {
                var names = new List<string>(mIndexes.Count + mAllocators.Count + mTypedAllocators.Count);
                names.AddRange(mIndexes.Keys);
                names.AddRange(mAllocators.Keys);
                names.AddRange(mTypedAllocators.Keys);
                return names;
            }
        }

        /// <summary>移除域并丢弃其状态。返回是否确实移除。</summary>
        public bool Remove(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            var removed = mIndexes.Remove(name);
            removed |= mAllocators.Remove(name);
            removed |= mTypedAllocators.Remove(name);
            return removed;
        }

        public void Clear()
        {
            mIndexes.Clear();
            mAllocators.Clear();
            mTypedAllocators.Clear();
        }
    }
}
