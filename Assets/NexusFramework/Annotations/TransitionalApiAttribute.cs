using System;
using System.Diagnostics;

namespace NexusFramework
{
    /// <summary>
    /// 过渡 API 元数据标记（D009 §5.4）。
    /// 必须与 <see cref="ObsoleteAttribute"/> 同时使用，并提供替代 API 与移除版本。
    /// 该特性仅在 Editor 期生效，运行时不可读取；运行时逻辑不得依赖它。
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum |
        AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field |
        AttributeTargets.Event | AttributeTargets.Interface,
        Inherited = false)]
    [Conditional("UNITY_EDITOR")]
    public sealed class TransitionalApiAttribute : Attribute
    {
        /// <summary>替代 API 的源码写法，例如 <c>Damage</c>。</summary>
        public string Replacement { get; }

        /// <summary>移除该过渡 API 的 MAJOR 版本，例如 <c>3.0.0</c>。</summary>
        public string RemoveVersion { get; }

        /// <summary>一句话迁移说明，可为空。</summary>
        public string Migration { get; }

        public TransitionalApiAttribute(string replacement, string removeVersion, string migration = null)
        {
            Replacement = replacement;
            RemoveVersion = removeVersion;
            Migration = migration;
        }
    }
}
