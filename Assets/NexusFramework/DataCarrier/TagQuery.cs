using System;
using System.Collections.Generic;

namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     Tag 组合查询条件。
    /// </summary>
    public sealed class TagQuery
    {
        public TagQuery(
            IEnumerable<string> all = null,
            IEnumerable<string> any = null,
            IEnumerable<string> none = null)
        {
            All = ToArray(all);
            Any = ToArray(any);
            None = ToArray(none);
        }

        public IReadOnlyList<string> All { get; }
        public IReadOnlyList<string> Any { get; }
        public IReadOnlyList<string> None { get; }

        public bool IsEmpty => All.Count == 0 && Any.Count == 0 && None.Count == 0;

        private static string[] ToArray(IEnumerable<string> keys)
        {
            if (keys == null)
                return Array.Empty<string>();

            var result = new List<string>();
            foreach (var key in keys)
                result.Add(TagKey.Normalize(key));
            return result.ToArray();
        }
    }
}
