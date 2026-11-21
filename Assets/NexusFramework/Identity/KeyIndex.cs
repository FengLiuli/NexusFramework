using System;
using System.Collections.Generic;

namespace NexusFramework.Identity
{
    /// <summary>
    ///     key ↔ id 双向索引。服务 L2 中有名字的数据：物品定义、Carrier 类型、
    ///     GAS 效果/属性/标签、本地化条目等。
    ///     <para>
    ///     key 的来源可以是 L1 资产键（如 <c>core:item.potion</c>）或 L2 注册名
    ///     （如 <c>core:player</c>）；索引器不区分两者，只负责映射。
    ///     </para>
    ///     <para>非线程安全；需由使用方在单线程或外部同步下调用。</para>
    /// </summary>
    public sealed class KeyIndex
    {
        private readonly string mName;
        private readonly KeyIndexOptions mOptions;
        private readonly IIdNamespaceAuthority mAuthority;
        private readonly Dictionary<string, int> mIdByKey;
        private readonly List<string> mKeyById; // 下标 = id；0 占位
        private readonly IdAllocator mAllocator;

        private int mInvalidKeyCount;
        private int mNamespaceRejectedCount;
        private int mAuthorityUnavailableCount;
        private int mCapacityRejectedCount;

        public KeyIndex(string name, KeyIndexOptions options = default, IIdNamespaceAuthority authority = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("KeyIndex name cannot be null or empty.", nameof(name));

            mName = name;
            mOptions = options;
            mAuthority = authority;
            mIdByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            mKeyById = new List<string> { null };
            mAllocator = new IdAllocator();
        }

        public string Name => mName;

        /// <summary>创建时使用的选项。</summary>
        public KeyIndexOptions Options => mOptions;

        /// <summary>已有 key 数量。</summary>
        public int Count => mIdByKey.Count;

        /// <summary>底层分配器已发过的最大槽位，或已显式注册的最大 id。</summary>
        public int Capacity => Math.Max(mAllocator.Capacity, mKeyById.Count - 1);

        /// <summary>key → id；查不到则注册并分配。失败返回 0。</summary>
        public int Intern(string key)
        {
            return TryIntern(key, out var id, out _, out _) ? id : 0;
        }

        /// <summary>带诊断的 Intern。</summary>
        public bool TryIntern(string key, out int id, out KeyIndexError error, out string diagnostic)
        {
            id = 0;
            if (!TryNormalize(key, out var normalized, out error, out diagnostic))
            {
                mInvalidKeyCount++;
                return false;
            }

            if (mIdByKey.TryGetValue(normalized, out id))
                return true;

            var newId = AllocateUnusedId();
            if (newId == 0)
            {
                error = KeyIndexError.CapacityExceeded;
                diagnostic = $"KeyIndex '{mName}' exhausted.";
                mCapacityRejectedCount++;
                return false;
            }

            if (mOptions.MaxValue > 0 && newId > mOptions.MaxValue)
            {
                mAllocator.Free(newId);
                error = KeyIndexError.IdOutOfRange;
                diagnostic = $"KeyIndex '{mName}' id {newId} exceeds MaxValue {mOptions.MaxValue}.";
                mCapacityRejectedCount++;
                return false;
            }

            mIdByKey[normalized] = newId;
            EnsureKeyCapacity(newId);
            mKeyById[newId] = normalized;
            id = newId;
            return true;
        }

        /// <summary>
        ///     把 key 显式绑定到指定 id，供配置驱动的稳定 id 使用（如 GAS 的 Tag Code）。
        ///     key 已绑定同一 id 时幂等成功；key 或 id 已被其它绑定占用时返回
        ///     <see cref="KeyIndexError.IdConflict" />，不覆盖。
        /// </summary>
        public bool TryRegister(string key, int id, out KeyIndexError error, out string diagnostic)
        {
            error = KeyIndexError.None;
            diagnostic = null;

            if (!TryNormalize(key, out var normalized, out error, out diagnostic))
            {
                mInvalidKeyCount++;
                return false;
            }

            if (id <= 0 || (mOptions.MaxValue > 0 && id > mOptions.MaxValue))
            {
                error = KeyIndexError.IdOutOfRange;
                diagnostic = $"KeyIndex '{mName}' id {id} is out of range.";
                mCapacityRejectedCount++;
                return false;
            }

            if (mIdByKey.TryGetValue(normalized, out var existingId))
            {
                if (existingId == id)
                    return true;

                error = KeyIndexError.IdConflict;
                diagnostic =
                    $"KeyIndex '{mName}' already maps '{normalized}' to {existingId}, cannot rebind to {id}.";
                return false;
            }

            if (id < mKeyById.Count && mKeyById[id] != null)
            {
                error = KeyIndexError.IdConflict;
                diagnostic = $"KeyIndex '{mName}' id {id} is already bound to '{mKeyById[id]}'.";
                return false;
            }

            mIdByKey[normalized] = id;
            EnsureKeyCapacity(id);
            mKeyById[id] = normalized;
            return true;
        }

        /// <summary>只查，不分配。</summary>
        public bool TryGet(string key, out int id)
        {
            id = 0;
            return TryNormalize(key, out var normalized, out _, out _)
                   && mIdByKey.TryGetValue(normalized, out id);
        }

        /// <summary>id → key，O(1) 数组直索引。</summary>
        public bool TryGetKey(int id, out string key)
        {
            key = null;
            if (id <= 0 || id >= mKeyById.Count)
                return false;

            key = mKeyById[id];
            return key != null;
        }

        /// <summary>删除映射并回收 id。未找到返回 false。</summary>
        public bool Remove(string key)
        {
            if (!TryNormalize(key, out var normalized, out _, out _))
                return false;
            if (!mIdByKey.TryGetValue(normalized, out var id))
                return false;

            mIdByKey.Remove(normalized);
            mKeyById[id] = null;
            mAllocator.Free(id);
            return true;
        }

        public bool IsAlive(int id)
        {
            // 以映射表为唯一真相：显式注册的 id 不经过底层分配器。
            return id > 0
                   && id < mKeyById.Count
                   && mKeyById[id] != null;
        }

        public void Clear()
        {
            mIdByKey.Clear();
            mKeyById.Clear();
            mKeyById.Add(null);
            mAllocator.Clear();
            mInvalidKeyCount = 0;
            mNamespaceRejectedCount = 0;
            mAuthorityUnavailableCount = 0;
            mCapacityRejectedCount = 0;
        }

        public KeyIndexDiagnostics GetDiagnostics()
        {
            return new KeyIndexDiagnostics(mName, Count, Capacity, mInvalidKeyCount,
                mNamespaceRejectedCount, mAuthorityUnavailableCount, mCapacityRejectedCount);
        }

        private bool TryNormalize(string key, out string normalized, out KeyIndexError error,
            out string diagnostic)
        {
            normalized = null;
            error = KeyIndexError.None;
            diagnostic = null;

            if (string.IsNullOrWhiteSpace(key))
            {
                error = KeyIndexError.InvalidKey;
                diagnostic = $"KeyIndex '{mName}' rejected empty key.";
                return false;
            }

            // 不解析命名空间时，冒号只作普通字符，键只做首尾裁剪。
            if (mOptions.DisableNamespaceParsing)
            {
                normalized = key.Trim();
                return true;
            }

            if (mOptions.RequireNamespace && key.IndexOf(':') < 0)
            {
                error = KeyIndexError.MissingNamespace;
                diagnostic = $"KeyIndex '{mName}' requires namespace: '{key}'.";
                return false;
            }

            if (!IdKey.TryParse(key, mOptions.RequireNamespace, out var idKey))
            {
                error = KeyIndexError.InvalidKey;
                diagnostic = $"KeyIndex '{mName}' rejected key '{key}'.";
                return false;
            }

            if (mOptions.RequireNamespace)
            {
                if (mAuthority == null)
                {
                    // RID13：authority 缺失时放行，但必须计数，便于发现"未真正校验"。
                    mAuthorityUnavailableCount++;
                }
                else if (!mAuthority.IsRegistered(idKey.Namespace))
                {
                    error = KeyIndexError.NamespaceNotRegistered;
                    diagnostic = $"KeyIndex '{mName}' rejects unregistered namespace '{idKey.Namespace}'.";
                    mNamespaceRejectedCount++;
                    return false;
                }
            }

            normalized = idKey.Value;
            return true;
        }

        private void EnsureKeyCapacity(int id)
        {
            while (mKeyById.Count <= id)
                mKeyById.Add(null);
        }

        /// <summary>
        ///     取一个空闲槽位；跳过已被显式注册占用的 id。
        ///     跳过时不 Free——把槽位留在分配器的 alive 表中，避免它被反复弹出。
        /// </summary>
        private int AllocateUnusedId()
        {
            while (true)
            {
                var id = mAllocator.Allocate();
                if (id == 0)
                    return 0;

                if (id >= mKeyById.Count || mKeyById[id] == null)
                    return id;
            }
        }
    }
}
