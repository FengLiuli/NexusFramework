using System;
using System.Collections.Generic;
using UnityEngine;

namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     CarrierId ↔ TValue 双射索引表。
    ///
    ///     反向映射（value → CarrierId）**始终维护**：值冲突采用「覆盖」语义，
    ///     Bind 时须按值反查旧主并驱逐旧主，故反查能力为必需。
    ///
    ///     **一张表 = 一个双射关系**。若需「多对一」（多个源指向同一目标，如多单位锁定同一目标），
    ///     请改用 <see cref="CarrierGroupIndex{TKey}" />，否则覆盖语义会互相驱逐、只剩最后一个。
    /// </summary>
    public sealed class CarrierIndex<TValue> : ICarrierIndex, ICarrierIndexOwner
    {
        private readonly Dictionary<CarrierId, TValue> _forward = new();
        private readonly Dictionary<TValue, CarrierId> _reverse = new();

        // 仅当 TValue == CarrierId 时启用「被引用值」清理（引用完整性）。
        private static readonly bool ValueIsCarrierId = typeof(CarrierId).IsAssignableFrom(typeof(TValue));

        private ICarrierManager _owner;

        /// <summary>
        ///     载体失去绑定时触发（subject = 该载体）。涵盖全部移除路径：
        ///     显式 <see cref="Unbind" />、值冲突驱逐旧主、自我重绑、销毁清理。
        ///     供宿主同步其它派生索引（避免「一个载体在两张表」的不一致）。
        /// </summary>
        public event Action<CarrierId> OnBindingRemoved;

        /// <param name="name">表名。同一 Architecture 内唯一，标识业务语义。</param>
        public CarrierIndex(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("CarrierIndex table name must not be null or empty", nameof(name));
            Name = name;
        }

        public string Name { get; }

        public int EntryCount => _forward.Count;

        void ICarrierIndexOwner.SetOwner(ICarrierManager owner) => _owner = owner;

        /// <summary>
        ///     建立映射。规则：
        ///     ① 载体无效 → 警告 + 返回 false（避免孤儿条目）；
        ///     ② 值冲突 → 驱逐旧主（后者覆盖前者），返回 true；
        ///     ③ 自我重绑 → 覆盖旧值并清理旧反向条目。
        /// </summary>
        public bool Bind(CarrierId carrier, TValue value)
        {
            if (_owner != null && !_owner.IsCarrierValid(carrier))
            {
                Debug.LogWarning($"[CarrierIndex:{Name}] Bind rejected: carrier {carrier} is not valid");
                return false;
            }

            // 规则③：同一载体重绑 → 先清旧值反向条目
            if (_forward.TryGetValue(carrier, out var oldValue) &&
                !EqualityComparer<TValue>.Default.Equals(oldValue, value))
                _reverse.Remove(oldValue);

            // 规则②：值冲突 → 驱逐旧主（后者覆盖前者）
            if (_reverse.TryGetValue(value, out var oldOwner) && oldOwner != carrier)
            {
                _forward.Remove(oldOwner);
                OnBindingRemoved?.Invoke(oldOwner);
            }

            _forward[carrier] = value;
            _reverse[value] = carrier;
            return true;
        }

        /// <summary>解除该载体映射（同时清反向条目）。</summary>
        public bool Unbind(CarrierId carrier)
        {
            if (!_forward.TryGetValue(carrier, out var value)) return false;

            _forward.Remove(carrier);
            if (_reverse.TryGetValue(value, out var owner) && owner == carrier)
                _reverse.Remove(value);

            OnBindingRemoved?.Invoke(carrier);
            return true;
        }

        public bool TryGet(CarrierId carrier, out TValue value) => _forward.TryGetValue(carrier, out value);

        public CarrierId TryGetCarrier(TValue value)
            => _reverse.TryGetValue(value, out var carrier) ? carrier : CarrierId.Invalid;

        public bool Contains(CarrierId carrier) => _forward.ContainsKey(carrier);

        /// <summary>全部载体（快照副本，用于遍历/诊断）。</summary>
        public IReadOnlyCollection<CarrierId> Carriers => new List<CarrierId>(_forward.Keys);

        public void OnCarrierRemoved(CarrierId carrier)
        {
            // ── 角色 A：载体作主语（key）──
            if (_forward.TryGetValue(carrier, out var value))
            {
                _forward.Remove(carrier);
                if (_reverse.TryGetValue(value, out var owner) && owner == carrier) // 所有权守卫
                    _reverse.Remove(value);
                OnBindingRemoved?.Invoke(carrier);
            }

            // ── 角色 B：载体作被引用值（仅 TValue == CarrierId）──
            // 有载体把本载体当作目标；它已销毁，指向它的绑定必须解除（source 本身存活）。
            if (ValueIsCarrierId &&
                _reverse.TryGetValue((TValue)(object)carrier, out var source) && source != carrier)
            {
                Unbind(source); // 内部触发 OnBindingRemoved
            }
        }
    }
}
