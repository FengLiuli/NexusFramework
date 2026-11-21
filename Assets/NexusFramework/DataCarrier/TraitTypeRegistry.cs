using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace NexusFramework.DataCarrier
{
    /// <summary>
    ///     数据特征类型名解析注册表。
    ///
    ///     <para>
    ///     <see cref="Type.GetType(string)" /> 只查找 mscorlib、调用方程序集与已加载的具名程序集，
    ///     业务程序集中的 Trait 短名无法解析（core backlog A1）。本注册表维护「短名 / 全名 → Type」
    ///     映射，并在首次解析失败时惰性扫描已加载程序集，补齐业务类型。
    ///     </para>
    /// </summary>
    public static class TraitTypeRegistry
    {
        private static readonly Dictionary<string, Type> ShortNames = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Type> FullNames = new(StringComparer.Ordinal);
        private static bool mScanned;

        /// <summary>已登记的 Trait 类型数量（诊断用）。</summary>
        public static int RegisteredCount => ShortNames.Count;

        /// <summary>
        ///     显式注册一个 Trait 类型。
        ///     同名不同类型时保留首个并告警；抽象类型与接口被忽略。
        /// </summary>
        public static void Register(Type traitType)
        {
            if (traitType == null || traitType.IsAbstract || traitType.IsInterface) return;
            if (!typeof(IDataTrait).IsAssignableFrom(traitType)) return;
            AddType(traitType);
        }

        /// <summary>显式注册一个 Trait 类型。</summary>
        public static void Register<T>() where T : class, IDataTrait => Register(typeof(T));

        /// <summary>
        ///     解析 Trait 类型名。支持三种形态：
        ///     ① <see cref="Type.AssemblyQualifiedName" />（含逗号）→ 直接交给运行时；
        ///     ② 全名（Namespace.Type）→ 查表；
        ///     ③ 短名（Type）→ 查表，必要时先扫描已加载程序集。
        /// </summary>
        public static Type Resolve(string traitTypeName)
        {
            if (string.IsNullOrEmpty(traitTypeName)) return null;

            if (traitTypeName.IndexOf(',') >= 0)
            {
                var qualified = Type.GetType(traitTypeName, false);
                if (qualified != null) return qualified;
            }

            if (ShortNames.TryGetValue(traitTypeName, out var found)) return found;
            if (FullNames.TryGetValue(traitTypeName, out found)) return found;

            if (!mScanned)
            {
                EnsureScanned();
                if (ShortNames.TryGetValue(traitTypeName, out found)) return found;
                if (FullNames.TryGetValue(traitTypeName, out found)) return found;
            }

            return null;
        }

        /// <summary>清空注册表并重置扫描标记（测试隔离用）。</summary>
        public static void Clear()
        {
            ShortNames.Clear();
            FullNames.Clear();
            mScanned = false;
        }

        private static void EnsureScanned()
        {
            mScanned = true;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }
                catch (Exception)
                {
                    // 个别程序集（如依赖缺失的第三方包）无法枚举类型，跳过即可
                    continue;
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract || type.IsInterface) continue;
                    if (!typeof(IDataTrait).IsAssignableFrom(type)) continue;
                    AddType(type);
                }
            }
        }

        private static void AddType(Type type)
        {
            var shortName = type.Name;
            if (ShortNames.TryGetValue(shortName, out var existing))
            {
                if (existing != type)
                {
                    Debug.LogWarning(
                        $"[TraitTypeRegistry] Trait 短名冲突: '{shortName}' 同时对应 " +
                        $"'{existing.FullName}' 与 '{type.FullName}'，保留前者。" +
                        "请改用全名或显式注册消歧。");
                }
            }
            else
            {
                ShortNames[shortName] = type;
            }

            var fullName = type.FullName;
            if (!string.IsNullOrEmpty(fullName)) FullNames[fullName] = type;
        }
    }
}
