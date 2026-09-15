using System;
using System.Collections.Generic;

namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     当前 CarrierManager 私有的 TagKey 与运行时槽位映射。
    /// </summary>
    internal sealed class TagRuntimeIndex
    {
        private sealed class Entry
        {
            public string Key;
            public RuntimeTagSlot Slot;
            public RuntimeTagSlot[] Ancestors;
        }

        private readonly Dictionary<string, Entry> entriesByKey = new();
        private readonly Dictionary<RuntimeTagSlot, Entry> entriesBySlot = new();
        private int nextSlotValue = 1;

        public RuntimeTagSlot Intern(string key)
        {
            var normalizedKey = TagKey.Normalize(key);
            if (entriesByKey.TryGetValue(normalizedKey, out var existingEntry))
                return existingEntry.Slot;

            var segments = normalizedKey.Split('.');
            var ancestors = new RuntimeTagSlot[segments.Length - 1];
            for (var i = 1; i < segments.Length; i++)
                ancestors[i - 1] = Intern(string.Join(".", segments, 0, i));

            var entry = new Entry
            {
                Key = normalizedKey,
                Slot = new RuntimeTagSlot(nextSlotValue++),
                Ancestors = ancestors
            };

            entriesByKey.Add(normalizedKey, entry);
            entriesBySlot.Add(entry.Slot, entry);
            return entry.Slot;
        }

        public bool TryGetSlot(string key, out RuntimeTagSlot slot)
        {
            var normalizedKey = TagKey.Normalize(key);
            if (entriesByKey.TryGetValue(normalizedKey, out var entry))
            {
                slot = entry.Slot;
                return true;
            }

            slot = default;
            return false;
        }

        /// <summary>
        ///     按槽位值反查槽位，用于跨程序集传入的 int 槽位（如 ITagService 的公开 API）。
        /// </summary>
        public bool TryGetSlot(int slotValue, out RuntimeTagSlot slot)
        {
            slot = new RuntimeTagSlot(slotValue);
            return entriesBySlot.ContainsKey(slot);
        }

        public string GetKey(RuntimeTagSlot slot)
        {
            if (!entriesBySlot.TryGetValue(slot, out var entry))
                throw new ArgumentException("Runtime tag slot does not belong to this carrier manager.", nameof(slot));

            return entry.Key;
        }

        public bool MatchesOrIsAncestor(RuntimeTagSlot actual, RuntimeTagSlot query)
        {
            if (actual == query)
                return true;

            if (!entriesBySlot.TryGetValue(actual, out var entry))
                return false;

            foreach (var ancestor in entry.Ancestors)
                if (ancestor == query)
                    return true;

            return false;
        }

        public void Clear()
        {
            entriesByKey.Clear();
            entriesBySlot.Clear();
            nextSlotValue = 1;
        }
    }
}
