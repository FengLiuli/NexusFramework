using System;
using System.Collections.Generic;
using NexusFramework.Identity;

namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     TagKey 与运行时槽位的映射，以及点分层级的祖先链。
    ///     <para>
    ///     key→slot 映射由 <see cref="KeyIndex" /> 提供；本类型只维护 Tag 特有的祖先链。
    ///     </para>
    /// </summary>
    internal sealed class TagRuntimeIndex
    {
        private readonly KeyIndex mIndex;
        private readonly Dictionary<int, int[]> mAncestorsBySlot = new Dictionary<int, int[]>();

        public TagRuntimeIndex()
            : this(new KeyIndex("core.tag", new KeyIndexOptions(parseNamespace: false)))
        {
        }

        public TagRuntimeIndex(KeyIndex index)
        {
            mIndex = index ?? throw new ArgumentNullException(nameof(index));
        }

        /// <summary>
        ///     键转槽位；已存在则复用。注册失败（KeyIndex 拒绝或容量耗尽）时返回
        ///     <see cref="RuntimeTagSlot" /> 的默认值（Value = 0），调用方必须用
        ///     <see cref="TryIntern" /> 判定成功，不要把 0 当作有效槽位。
        /// </summary>
        public RuntimeTagSlot Intern(string key)
        {
            return TryIntern(key, out var slot) ? slot : default;
        }

        /// <summary>键转槽位；失败返回 false，不产生半成品挂载。</summary>
        public bool TryIntern(string key, out RuntimeTagSlot slot)
        {
            slot = default;

            var normalizedKey = TagKey.Normalize(key);
            var id = mIndex.Intern(normalizedKey);
            if (id <= 0)
                return false;

            EnsureAncestors(id, normalizedKey);
            slot = new RuntimeTagSlot(id);
            return true;
        }

        public bool TryGetSlot(string key, out RuntimeTagSlot slot)
        {
            slot = default;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var normalizedKey = TagKey.Normalize(key);
            if (!mIndex.TryGet(normalizedKey, out var id))
                return false;

            slot = new RuntimeTagSlot(id);
            return true;
        }

        /// <summary>
        ///     按槽位值反查槽位，用于跨程序集传入的 int 槽位（如 ITagService 的公开 API）。
        /// </summary>
        public bool TryGetSlot(int slotValue, out RuntimeTagSlot slot)
        {
            slot = new RuntimeTagSlot(slotValue);
            return mIndex.IsAlive(slotValue);
        }

        public string GetKey(RuntimeTagSlot slot)
        {
            if (!mIndex.TryGetKey(slot.Value, out var key))
                throw new ArgumentException("Runtime tag slot does not belong to this carrier manager.", nameof(slot));

            return key;
        }

        public bool MatchesOrIsAncestor(RuntimeTagSlot actual, RuntimeTagSlot query)
        {
            if (actual == query)
                return true;

            if (!mAncestorsBySlot.TryGetValue(actual.Value, out var ancestors))
                return false;

            foreach (var ancestor in ancestors)
                if (ancestor == query.Value)
                    return true;

            return false;
        }

        /// <summary>只清本类型维护的祖先链，不动外部注入的 key→slot 索引。</summary>
        public void ClearAncestors()
        {
            mAncestorsBySlot.Clear();
        }

        /// <summary>清空索引与祖先链。仅当本类型拥有该索引时使用。</summary>
        public void Clear()
        {
            mIndex.Clear();
            mAncestorsBySlot.Clear();
        }

        private void EnsureAncestors(int id, string key)
        {
            if (mAncestorsBySlot.ContainsKey(id))
                return;

            var segments = key.Split('.');
            var ancestors = new List<int>(segments.Length - 1);
            for (var i = 1; i < segments.Length; i++)
            {
                var ancestorId = mIndex.Intern(string.Join(".", segments, 0, i));
                // 祖先注册失败（容量耗尽）时跳过，不要把 0 写进祖先链——
                // 否则 MatchesOrIsAncestor 会把无效槽位当成命中。
                if (ancestorId > 0)
                    ancestors.Add(ancestorId);
            }

            mAncestorsBySlot[id] = ancestors.ToArray();
        }
    }
}
