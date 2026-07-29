using System;
using System.Collections.Generic;
using System.Linq;

namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     TagKey 规范化与格式校验。
    /// </summary>
    public static class TagKey
    {
        public static string Normalize(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Tag key cannot be null, empty, or whitespace.", nameof(key));

            var segments = key.Split('.');
            for (var i = 0; i < segments.Length; i++)
            {
                segments[i] = segments[i].Trim();
                if (segments[i].Length == 0)
                    throw new ArgumentException("Tag key cannot contain empty path segments.", nameof(key));
            }

            return string.Join(".", segments);
        }

        public static IReadOnlyList<string> GetSegments(string key)
        {
            return Normalize(key).Split('.').ToArray();
        }
    }
}
