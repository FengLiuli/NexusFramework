using System;
using System.Collections.Generic;
using System.Linq;
using NexusFramework.Identity;
using UnityEngine;

namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     <see cref="ITagService"/> 的纯托管实现。
    ///     自带 Tag 运行时索引（正排）与反向索引，不依赖任何 ECS 或事件系统。
    /// </summary>
    public sealed class CarrierTagService : ITagService
    {
        private readonly ICarrierManager carrierManager;
        private readonly object lockObject = new();

        /// <summary>Tag 键与运行时槽位的双向映射，含祖先链推导。</summary>
        private readonly TagRuntimeIndex tagIndex;

        /// <summary>
        ///     索引是否由本服务自建。注入的索引归 <see cref="IdRegistry" /> 所有，
        ///     本服务销毁时只能清祖先链，不能清整个索引。
        /// </summary>
        private readonly bool ownsTagIndex;

        /// <summary>正向索引：Carrier 持有的槽位集合。</summary>
        private readonly Dictionary<int, HashSet<RuntimeTagSlot>> tagsByCarrier = new();

        /// <summary>反向索引：槽位被哪些 Carrier 持有。</summary>
        private readonly Dictionary<RuntimeTagSlot, HashSet<int>> carriersBySlot = new();

        /// <summary>已知的 Carrier 集合，用于空条件反查。</summary>
        private readonly HashSet<int> knownCarriers = new();

        private ICarrierEventCallback eventCallback;

        /// <param name="carrierManager">
        ///     仅用于校验 Carrier 是否有效与枚举已知载体，不参与 Tag 状态存储。
        /// </param>
        public CarrierTagService(ICarrierManager carrierManager)
            : this(carrierManager, null)
        {
        }

        /// <param name="carrierManager">载体管理器。</param>
        /// <param name="tagIndex">
        ///     Tag 的 key→slot 索引；为 null 时本服务自建一个。
        ///     由架构注入时，索引归 <see cref="IdRegistry" /> 管理。
        /// </param>
        public CarrierTagService(ICarrierManager carrierManager, KeyIndex tagIndex)
        {
            this.carrierManager = carrierManager ?? throw new ArgumentNullException(nameof(carrierManager));
            ownsTagIndex = tagIndex == null;
            this.tagIndex = ownsTagIndex ? new TagRuntimeIndex() : new TagRuntimeIndex(tagIndex);
        }

        #region 生命周期

        public bool Initialized { get; set; }

        public void Init()
        {
            lock (lockObject)
            {
                ClearTagIndex();
                tagsByCarrier.Clear();
                carriersBySlot.Clear();

                // 从载体管理器重建，而不是留空：
                // 空集合会让 FindCarriersWithTags 返回空集，而 FindCarriersWithTag
                // 仍能查到——同一份数据两个 API 结论不一致。
                // 重建后 knownCarriers 不再是独立账本，而是 CarrierManager 的投影。
                knownCarriers.Clear();
                if (carrierManager == null) return;

                // CarrierManager 与本服务同在 InitAll 中初始化，遍历顺序不保证。
                // 若对方尚未 Init，其内部字段为空引用——此时跳过重建，
                // 待载体创建时经 AddTag 自然登记。
                if (!carrierManager.Initialized) return;

                foreach (var id in carrierManager.GetAllCarriers())
                    knownCarriers.Add(id);
            }
        }

        public void Deinit()
        {
            lock (lockObject)
            {
                ClearTagIndex();
                tagsByCarrier.Clear();
                carriersBySlot.Clear();
                knownCarriers.Clear();
            }
        }

        public void Dispose()
        {
            lock (lockObject)
            {
                ClearTagIndex();
                tagsByCarrier.Clear();
                carriersBySlot.Clear();
                knownCarriers.Clear();
                eventCallback = null;
            }
        }

        /// <summary>
        ///     注入索引时只清本服务维护的祖先链；自建索引才整体清空。
        ///     由架构注入的 <c>core.tag</c> 归 <see cref="IdRegistry" />，生命周期由架构统一管理。
        /// </summary>
        private void ClearTagIndex()
        {
            if (ownsTagIndex)
                tagIndex.Clear();
            else
                tagIndex.ClearAncestors();
        }

        public void SetEventCallback(ICarrierEventCallback callback)
        {
            eventCallback = callback;
        }

        public bool IsCarrierKnown(int carrierId)
        {
            return carrierManager != null && carrierManager.IsCarrierValid(carrierId);
        }

        public void OnCarrierDestroyed(int carrierId)
        {
            lock (lockObject)
            {
                if (!tagsByCarrier.TryGetValue(carrierId, out var slots))
                {
                    knownCarriers.Remove(carrierId);
                    return;
                }

                foreach (var slot in slots)
                    RemoveReverseEntry(carrierId, slot);

                tagsByCarrier.Remove(carrierId);
                knownCarriers.Remove(carrierId);
            }
        }

        #endregion

        #region ID 空间

        public int InternTag(string key)
        {
            if (!TryNormalize(key, out var normalized))
                return 0;

            lock (lockObject)
            {
                return tagIndex.Intern(normalized).Value;
            }
        }

        public string GetTagKey(int tagId)
        {
            if (tagId <= 0)
                return null;

            lock (lockObject)
            {
                return tagIndex.TryGetSlot(tagId, out var slot) ? tagIndex.GetKey(slot) : null;
            }
        }

        public bool TryGetTagId(string key, out int tagId)
        {
            tagId = 0;
            if (!TryNormalize(key, out var normalized))
                return false;

            lock (lockObject)
            {
                if (!tagIndex.TryGetSlot(normalized, out var slot))
                    return false;

                tagId = slot.Value;
                return true;
            }
        }

        public bool MatchesOrIsAncestor(int actualTagId, int queryTagId)
        {
            if (actualTagId <= 0 || queryTagId <= 0)
                return false;

            if (actualTagId == queryTagId)
                return true;

            lock (lockObject)
            {
                if (!tagIndex.TryGetSlot(actualTagId, out var actualSlot) ||
                    !tagIndex.TryGetSlot(queryTagId, out var querySlot))
                    return false;

                return tagIndex.MatchesOrIsAncestor(actualSlot, querySlot);
            }
        }

        #endregion

        #region 写入

        public bool AddTag(int carrierId, string key)
        {
            var outcome = AddTagInternal(carrierId, key);
            if (outcome != AddOutcome.Added)
                return false;

            eventCallback?.OnTagAdded(carrierId, TagKey.Normalize(key));
            return true;
        }

        public bool RemoveTag(int carrierId, string key)
        {
            if (!TryNormalize(key, out var normalized))
                return false;

            lock (lockObject)
            {
                if (!tagsByCarrier.TryGetValue(carrierId, out var slots))
                    return false;

                if (!tagIndex.TryGetSlot(normalized, out var slot))
                    return false;

                if (!slots.Remove(slot))
                    return false;

                RemoveReverseEntry(carrierId, slot);
            }

            eventCallback?.OnTagRemoved(carrierId, normalized);
            return true;
        }

        public int AddTags(int carrierId, IEnumerable<string> keys)
        {
            if (keys == null)
                return 0;

            var added = new List<string>();
            foreach (var key in keys)
            {
                if (AddTagInternal(carrierId, key) != AddOutcome.Added)
                    continue;

                added.Add(TagKey.Normalize(key));
            }

            foreach (var key in added)
                eventCallback?.OnTagAdded(carrierId, key);

            return added.Count;
        }

        public int RemoveTags(int carrierId, IEnumerable<string> keys)
        {
            if (keys == null)
                return 0;

            var removed = new List<string>();
            foreach (var key in keys)
            {
                if (!RemoveTagInternal(carrierId, key, out var normalized))
                    continue;

                removed.Add(normalized);
            }

            foreach (var key in removed)
                eventCallback?.OnTagRemoved(carrierId, key);

            return removed.Count;
        }

        public int RestoreTags(int carrierId, IEnumerable<string> keys)
        {
            if (keys == null)
                return 0;

            var restored = 0;
            foreach (var key in keys)
                if (AddTagInternal(carrierId, key) != AddOutcome.Invalid)
                    restored++;

            return restored;
        }

        #endregion

        #region 查询

        public bool HasExactTag(int carrierId, string key)
        {
            if (!TryNormalize(key, out var normalized))
                return false;

            lock (lockObject)
            {
                if (!tagsByCarrier.TryGetValue(carrierId, out var slots))
                    return false;

                return tagIndex.TryGetSlot(normalized, out var slot) && slots.Contains(slot);
            }
        }

        public bool HasTag(int carrierId, string key)
        {
            if (!TryNormalize(key, out var normalized))
                return false;

            lock (lockObject)
            {
                if (!tagsByCarrier.TryGetValue(carrierId, out var slots))
                    return false;

                if (!tagIndex.TryGetSlot(normalized, out var querySlot))
                    return false;

                foreach (var actualSlot in slots)
                    if (tagIndex.MatchesOrIsAncestor(actualSlot, querySlot))
                        return true;

                return false;
            }
        }

        public bool HasAllTags(int carrierId, IEnumerable<string> keys)
        {
            if (keys == null)
                return false;

            lock (lockObject)
            {
                if (!tagsByCarrier.TryGetValue(carrierId, out var slots))
                    return false;

                foreach (var key in keys)
                    if (!HasTagUnsafe(slots, key))
                        return false;

                return true;
            }
        }

        public bool HasAnyTag(int carrierId, IEnumerable<string> keys)
        {
            if (keys == null)
                return false;

            lock (lockObject)
            {
                if (!tagsByCarrier.TryGetValue(carrierId, out var slots))
                    return false;

                foreach (var key in keys)
                    if (HasTagUnsafe(slots, key))
                        return true;

                return false;
            }
        }

        public bool HasNoneTags(int carrierId, IEnumerable<string> keys)
        {
            return !HasAnyTag(carrierId, keys);
        }

        public IEnumerable<string> GetTags(int carrierId)
        {
            lock (lockObject)
            {
                if (!tagsByCarrier.TryGetValue(carrierId, out var slots))
                    return Enumerable.Empty<string>();

                return slots.Select(slot => tagIndex.GetKey(slot)).ToList();
            }
        }

        public int GetTagCount(int carrierId)
        {
            lock (lockObject)
            {
                return tagsByCarrier.TryGetValue(carrierId, out var slots) ? slots.Count : 0;
            }
        }

        #endregion

        #region 反查

        public IEnumerable<int> FindCarriersWithTag(string key)
        {
            if (!TryNormalize(key, out var normalized))
                return Enumerable.Empty<int>();

            lock (lockObject)
            {
                if (!tagIndex.TryGetSlot(normalized, out var querySlot))
                    return Enumerable.Empty<int>();

                var result = new HashSet<int>();
                foreach (var kvp in carriersBySlot)
                    if (tagIndex.MatchesOrIsAncestor(kvp.Key, querySlot))
                        result.UnionWith(kvp.Value);

                return result.ToList();
            }
        }

        public IEnumerable<int> FindCarriersWithTags(TagQuery query)
        {
            if (query == null || query.IsEmpty)
                return Enumerable.Empty<int>();

            lock (lockObject)
            {
                HashSet<int> result = null;

                if (query.All.Count > 0)
                {
                    result = new HashSet<int>(FindCarriersWithTagUnsafe(query.All.First()));
                    foreach (var key in query.All.Skip(1))
                        result.IntersectWith(FindCarriersWithTagUnsafe(key));
                }

                if (query.Any.Count > 0)
                {
                    var anyMatches = new HashSet<int>();
                    foreach (var key in query.Any)
                        anyMatches.UnionWith(FindCarriersWithTagUnsafe(key));

                    result = result == null ? anyMatches : Intersect(result, anyMatches);
                }

                result ??= new HashSet<int>(knownCarriers);
                result.IntersectWith(knownCarriers);

                if (query.None.Count > 0)
                {
                    var excluded = new HashSet<int>();
                    foreach (var key in query.None)
                        excluded.UnionWith(FindCarriersWithTagUnsafe(key));

                    result.ExceptWith(excluded);
                }

                return result.ToList();
            }
        }

        #endregion

        #region 私有辅助

        private enum AddOutcome
        {
            /// <summary>键无效或 Carrier 不存在。</summary>
            Invalid,

            /// <summary>该 Carrier 已持有此 Tag。</summary>
            Duplicate,

            /// <summary>本次新增成功。</summary>
            Added
        }

        /// <summary>归一化失败时返回 false。空键与含空段的键视为无效。</summary>
        private static bool TryNormalize(string key, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            try
            {
                normalized = TagKey.Normalize(key);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private AddOutcome AddTagInternal(int carrierId, string key)
        {
            if (!IsCarrierKnown(carrierId))
                return AddOutcome.Invalid;

            if (!TryNormalize(key, out var normalized))
                return AddOutcome.Invalid;

            lock (lockObject)
            {
                if (!tagIndex.TryIntern(normalized, out var slot))
                    return AddOutcome.Invalid;

                if (!tagsByCarrier.TryGetValue(carrierId, out var slots))
                {
                    slots = new HashSet<RuntimeTagSlot>();
                    tagsByCarrier[carrierId] = slots;
                    knownCarriers.Add(carrierId);
                }

                if (!slots.Add(slot))
                    return AddOutcome.Duplicate;

                AddReverseEntry(carrierId, slot);
                return AddOutcome.Added;
            }
        }

        private bool RemoveTagInternal(int carrierId, string key, out string normalized)
        {
            normalized = null;
            if (!TryNormalize(key, out var value))
                return false;

            lock (lockObject)
            {
                if (!tagsByCarrier.TryGetValue(carrierId, out var slots))
                    return false;

                if (!tagIndex.TryGetSlot(value, out var slot) || !slots.Remove(slot))
                    return false;

                RemoveReverseEntry(carrierId, slot);
                normalized = value;
                return true;
            }
        }

        private void AddReverseEntry(int carrierId, RuntimeTagSlot slot)
        {
            if (!carriersBySlot.TryGetValue(slot, out var carriers))
            {
                carriers = new HashSet<int>();
                carriersBySlot[slot] = carriers;
            }

            carriers.Add(carrierId);
        }

        private void RemoveReverseEntry(int carrierId, RuntimeTagSlot slot)
        {
            if (!carriersBySlot.TryGetValue(slot, out var carriers))
                return;

            carriers.Remove(carrierId);
            if (carriers.Count == 0)
                carriersBySlot.Remove(slot);
        }

        private bool HasTagUnsafe(HashSet<RuntimeTagSlot> slots, string key)
        {
            if (!tagIndex.TryGetSlot(key, out var querySlot))
                return false;

            foreach (var actualSlot in slots)
                if (tagIndex.MatchesOrIsAncestor(actualSlot, querySlot))
                    return true;

            return false;
        }

        /// <summary>调用方必须已持有 <see cref="lockObject"/>。</summary>
        private HashSet<int> FindCarriersWithTagUnsafe(string key)
        {
            if (!tagIndex.TryGetSlot(key, out var querySlot))
                return new HashSet<int>();

            var result = new HashSet<int>();
            foreach (var kvp in carriersBySlot)
                if (tagIndex.MatchesOrIsAncestor(kvp.Key, querySlot))
                    result.UnionWith(kvp.Value);

            return result;
        }

        /// <summary>
        ///     交集结果同样受已知载体集合约束：Tag 索引里可能残留已销毁载体的槽位，
        ///     若不收敛，交集会把这些幽灵载体带进结果。
        /// </summary>
        private static HashSet<int> Intersect(HashSet<int> left, HashSet<int> right)
        {
            var result = new HashSet<int>(left);
            result.IntersectWith(right);
            return result;
        }

        #endregion
    }
}
