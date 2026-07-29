using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     聚合保存单个 Carrier 所有 Tag 的内置特征。
    /// </summary>
    [Serializable]
    public sealed class TagTrait : DataTrait
    {
        private static readonly string sTypeName = typeof(TagTrait).AssemblyQualifiedName;

        [NonSerialized] private CarrierManager manager;
        [NonSerialized] private readonly HashSet<RuntimeTagSlot> slots = new();

        public override string TraitTypeName => sTypeName;

        internal IEnumerable<RuntimeTagSlot> Slots => slots;

        internal void Bind(CarrierManager carrierManager)
        {
            manager = carrierManager ?? throw new ArgumentNullException(nameof(carrierManager));
        }

        internal bool Add(RuntimeTagSlot slot)
        {
            return slots.Add(slot);
        }

        internal bool Remove(RuntimeTagSlot slot)
        {
            return slots.Remove(slot);
        }

        internal bool Contains(RuntimeTagSlot slot)
        {
            return slots.Contains(slot);
        }

        public override IDataTrait Clone()
        {
            var clone = new TagTrait();
            foreach (var slot in slots)
                clone.slots.Add(slot);

            return clone;
        }

        public override string ToJson()
        {
            EnsureBound();
            var keys = slots
                .Select(manager.GetTagKey)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
            return JsonUtility.ToJson(new TagTraitSerializationData { keys = keys });
        }

        public override void FromJson(string json)
        {
            EnsureBound();
            var data = JsonUtility.FromJson<TagTraitSerializationData>(json);
            slots.Clear();

            if (data?.keys == null)
                return;

            foreach (var key in data.keys)
                slots.Add(manager.InternTag(key));
        }

        private void EnsureBound()
        {
            if (manager == null)
                throw new InvalidOperationException("TagTrait must be attached to a CarrierManager before serialization or deserialization.");
        }

        [Serializable]
        private class TagTraitSerializationData
        {
            public string[] keys;
        }
    }
}
