using System;
using System.Collections.Generic;
using UnityEngine;

namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     按 TKey 分组的集合索引表：(TKey) → { CarrierId }。
    ///
    ///     与 <see cref="CarrierIndex{TValue}" /> 的分工：
    ///     - 一对多、多对一关系用本类型；
    ///     - 一对一（双射）关系用 <see cref="CarrierIndex{TValue}" />。
    ///
    ///     内部维护 <c>CarrierId → {TKey}</c> 反向账本，使载体销毁时能 O(#键) 精确清理其成员身份。
    /// </summary>
    public sealed class CarrierGroupIndex<TKey> : ICarrierIndex, ICarrierIndexOwner
    {
        // key → 成员集合
        private readonly Dictionary<TKey, HashSet<CarrierId>> _groups = new();

        // 反向账本：CarrierId → 其所属的所有 key（用于销毁时精确清理）
        private readonly Dictionary<CarrierId, HashSet<TKey>> _keysByCarrier = new();

        // 仅当 TKey == CarrierId 时启用「被引用分组键」清理（引用完整性）。
        private static readonly bool KeyIsCarrierId = typeof(CarrierId).IsAssignableFrom(typeof(TKey));

        private ICarrierManager _owner;

        /// <param name="name">表名。同一 Architecture 内唯一，标识业务语义。</param>
        public CarrierGroupIndex(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("CarrierGroupIndex table name must not be null or empty", nameof(name));
            Name = name;
        }

        public string Name { get; }

        public int EntryCount
        {
            get
            {
                var total = 0;
                foreach (var set in _groups.Values) total += set.Count;
                return total;
            }
        }

        void ICarrierIndexOwner.SetOwner(ICarrierManager owner) => _owner = owner;

        /// <summary>加入分组。同一 key 下重复加入同一载体是幂等的。载体无效则拒绝。</summary>
        public bool Add(TKey key, CarrierId carrier)
        {
            if (_owner != null && !_owner.IsCarrierValid(carrier))
            {
                Debug.LogWarning($"[CarrierGroupIndex:{Name}] Add rejected: carrier {carrier} is not valid");
                return false;
            }

            if (!_groups.TryGetValue(key, out var members))
            {
                members = new HashSet<CarrierId>();
                _groups[key] = members;
            }

            if (!members.Add(carrier)) return false;

            if (!_keysByCarrier.TryGetValue(carrier, out var keys))
            {
                keys = new HashSet<TKey>();
                _keysByCarrier[carrier] = keys;
            }
            keys.Add(key);
            return true;
        }

        /// <summary>从分组移除载体。</summary>
        public bool Remove(TKey key, CarrierId carrier)
        {
            if (!_groups.TryGetValue(key, out var members) || !members.Remove(carrier)) return false;

            if (members.Count == 0) _groups.Remove(key);

            if (_keysByCarrier.TryGetValue(carrier, out var keys))
            {
                keys.Remove(key);
                if (keys.Count == 0) _keysByCarrier.Remove(carrier);
            }
            return true;
        }

        /// <summary>移除某载体的全部分组归属（等价于它作为成员的所有条目）。</summary>
        public bool RemoveCarrierFromAll(CarrierId carrier)
        {
            if (!_keysByCarrier.TryGetValue(carrier, out var keys)) return false;

            foreach (var key in keys)
                if (_groups.TryGetValue(key, out var members))
                {
                    members.Remove(carrier);
                    if (members.Count == 0) _groups.Remove(key);
                }

            _keysByCarrier.Remove(carrier);
            return true;
        }

        /// <summary>清空某分组。</summary>
        public bool RemoveAll(TKey key)
        {
            if (!_groups.TryGetValue(key, out var members)) return false;

            foreach (var carrier in members)
                if (_keysByCarrier.TryGetValue(carrier, out var keys))
                {
                    keys.Remove(key);
                    if (keys.Count == 0) _keysByCarrier.Remove(carrier);
                }

            _groups.Remove(key);
            return true;
        }

        /// <summary>反查某分组下的载体（快照副本）。</summary>
        public IReadOnlyCollection<CarrierId> Get(TKey key)
            => _groups.TryGetValue(key, out var members)
                ? new List<CarrierId>(members)
                : Array.Empty<CarrierId>();

        /// <summary>某载体所属的所有 key（快照副本）。</summary>
        public IReadOnlyCollection<TKey> KeysOf(CarrierId carrier)
            => _keysByCarrier.TryGetValue(carrier, out var keys)
                ? new List<TKey>(keys)
                : Array.Empty<TKey>();

        /// <summary>分组数（诊断）。</summary>
        public int GroupCount => _groups.Count;

        public void OnCarrierRemoved(CarrierId carrier)
        {
            // ── 角色 A：载体作成员 → 从所属各分组移除 ──
            RemoveCarrierFromAll(carrier);

            // ── 角色 B：载体作分组 key（仅 TKey == CarrierId）→ 整组清除 ──
            // 该组「以本载体为键」，载体已销毁，组本身不再有意义。
            // 用 RemoveAll 而非 _groups.Remove：后者只删组、不反清成员的 _keysByCarrier，留下悬垂账本。
            if (KeyIsCarrierId)
                RemoveAll((TKey)(object)carrier);
        }
    }
}
