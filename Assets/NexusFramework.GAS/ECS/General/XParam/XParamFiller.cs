using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NexusFramework.GAS.ECS
{
    /// <summary>
    ///     把 Luban 生成的 <c>cfg.*</c> 参数对象填充到运行时的 <see cref="XParam"/>。
    ///     <para>
    ///         映射规则不在此处硬编码，而是从 <see cref="BeanFieldAttribute"/> /
    ///         <see cref="BeanPolymorphicFieldAttribute"/> 读取——这两个特性本就是为此设计的，
    ///         只是此前只有 Excel 路径（<c>DecodeExcelData</c>）在消费它们。
    ///     </para>
    ///     <para>
    ///         填充失败一律 <c>LogError</c>，不静默跳过：配置参数丢失的行为是
    ///         "技能能放但没效果"，从表现上极难定位。
    ///     </para>
    /// </summary>
    public static class XParamFiller
    {
        private const BindingFlags CfgFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

        /// <summary>
        ///     将 <paramref name="cfgSource"/> 的字段值按特性声明填入 <paramref name="target"/>。
        /// </summary>
        /// <returns>全部字段均填充成功时返回 true。</returns>
        public static bool Fill(XParam target, object cfgSource)
        {
            if (target == null)
            {
                Debug.LogError("[XParamFiller] target 为 null");
                return false;
            }

            if (cfgSource == null)
            {
                Debug.LogError($"[XParamFiller] {target.GetType().Name} 的配置源为 null");
                return false;
            }

            return FillMembers(target, cfgSource, $"{target.GetType().Name} ← {cfgSource.GetType().Name}");
        }

        private static bool FillMembers(object target, object source, string path)
        {
            var ok = true;

            foreach (var member in EnumerateConfiguredMembers(target.GetType()))
            {
                if (member.Polymorphic != null)
                    ok &= FillPolymorphic(target, member, source, path);
                else
                    ok &= FillScalar(target, member, source, path);
            }

            return ok;
        }

        /// <summary>先处理普通字段，再处理多态字段，保证参数类型解析时所需的字段已就位。</summary>
        private static IEnumerable<ConfiguredMember> EnumerateConfiguredMembers(Type type)
        {
            var scalars = new List<ConfiguredMember>();
            var polymorphics = new List<ConfiguredMember>();

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var poly = field.GetCustomAttribute<BeanPolymorphicFieldAttribute>();
                if (poly != null)
                {
                    polymorphics.Add(new ConfiguredMember(field, null, poly));
                    continue;
                }

                var bean = field.GetCustomAttribute<BeanFieldAttribute>();
                if (bean != null)
                    scalars.Add(new ConfiguredMember(field, bean, null));
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var poly = property.GetCustomAttribute<BeanPolymorphicFieldAttribute>();
                if (poly != null)
                {
                    polymorphics.Add(new ConfiguredMember(property, null, poly));
                    continue;
                }

                var bean = property.GetCustomAttribute<BeanFieldAttribute>();
                if (bean != null)
                    scalars.Add(new ConfiguredMember(property, bean, null));
            }

            scalars.Sort((a, b) => a.Bean.Order.CompareTo(b.Bean.Order));
            scalars.AddRange(polymorphics);
            return scalars;
        }

        private static bool FillScalar(object target, ConfiguredMember member, object source, string path)
        {
            var cfgFieldName = member.Bean.Name ?? member.Name;
            var cfgField = FindField(source.GetType(), cfgFieldName);

            if (cfgField == null)
            {
                Debug.LogError($"[XParamFiller] {path}: 配置类型缺少字段 '{cfgFieldName}'");
                return false;
            }

            var raw = cfgField.GetValue(source);

            // 目标类型优先取 setter 的参数类型，而非字段类型。
            // 二者可能不同：如 XParamCatchAreaBox3D.layer 是 LayerMask，
            // 但 SetLayer(int) 接受 int——按字段类型转换会失败。
            var targetType = ResolveSetterParamType(target, member.Bean.Setter) ?? member.MemberType;

            if (!TryConvert(raw, targetType, out var converted, out var reason))
            {
                Debug.LogError(
                    $"[XParamFiller] {path}.{member.Name}: 无法从 {raw?.GetType().Name ?? "null"} 转为 {targetType.Name}（{reason}）");
                return false;
            }

            return InvokeSetter(target, member, converted, path);
        }

        private static bool FillPolymorphic(object target, ConfiguredMember member, object source, string path)
        {
            var poly = member.Polymorphic;
            var cfgField = FindField(source.GetType(), poly.BeanFieldName);

            if (cfgField == null)
            {
                Debug.LogError($"[XParamFiller] {path}: 配置类型缺少多态字段 '{poly.BeanFieldName}'");
                return false;
            }

            var subCfg = cfgField.GetValue(source);
            if (subCfg == null)
            {
                // 多态字段可选：未配置时保持默认
                return true;
            }

            var subTypeName = subCfg.GetType().Name;

            // 1) 写类型判别符
            if (!TryInvokeMethod(target, poly.TypeSetter, subTypeName, out var typeReason))
            {
                Debug.LogError($"[XParamFiller] {path}.{member.Name}: 调用 {poly.TypeSetter} 失败（{typeReason}）");
                return false;
            }

            // 2) 解析出参数类型
            var paramType = ResolveParamType(poly.ParamTypeResolver, subTypeName, target, out var resolveReason);
            if (paramType == null)
            {
                Debug.LogError($"[XParamFiller] {path}.{member.Name}: 解析 '{subTypeName}' 的参数类型失败（{resolveReason}）");
                return false;
            }

            // 3) 实例化
            if (Activator.CreateInstance(paramType) is not XParam subParam)
            {
                Debug.LogError($"[XParamFiller] {path}.{member.Name}: {paramType.Name} 不是 XParam");
                return false;
            }

            // 4) 下钻到 catcher 的 Param 字段再填充。
            //    cfg 侧结构是 <Catcher>{ Param: <XParamXxx> }——参数字段挂在 Param 对象上，
            //    而不是 catcher 自身。把 catcher 直接当填充源会去找不存在的字段。
            var paramSource = FindField(subCfg.GetType(), "Param")?.GetValue(subCfg) ?? subCfg;

            if (!FillMembers(subParam, paramSource, $"{path}.{member.Name}"))
                return false;

            // 5) 挂上参数
            if (!TryInvokeMethod(target, poly.ParamSetter, subParam, out var paramReason))
            {
                Debug.LogError($"[XParamFiller] {path}.{member.Name}: 调用 {poly.ParamSetter} 失败（{paramReason}）");
                return false;
            }

            return true;
        }

        /// <summary>
        ///     解析 <c>"TargetCatcherHelper.GetCatcherParamType"</c> 形式的方法全路径。
        /// </summary>
        private static Type ResolveParamType(string resolverPath, string subTypeName, object target, out string reason)
        {
            reason = null;

            var dot = resolverPath.LastIndexOf('.');
            if (dot <= 0 || dot == resolverPath.Length - 1)
            {
                reason = $"ParamTypeResolver 格式非法: '{resolverPath}'";
                return null;
            }

            var helperName = resolverPath.Substring(0, dot);
            var methodName = resolverPath.Substring(dot + 1);

            var helperType = target.GetType().Assembly.GetType($"{target.GetType().Namespace}.{helperName}")
                             ?? Type.GetType(helperName)
                             ?? FindTypeInAssemblies(helperName);

            if (helperType == null)
            {
                reason = $"找不到 helper 类型 '{helperName}'";
                return null;
            }

            var method = helperType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                reason = $"{helperName} 中找不到静态方法 '{methodName}'";
                return null;
            }

            try
            {
                var result = method.Invoke(null, new object[] { subTypeName }) as Type;
                if (result == null) reason = $"{helperName}.{methodName}('{subTypeName}') 返回 null";
                return result;
            }
            catch (TargetInvocationException e)
            {
                reason = e.InnerException?.Message ?? e.Message;
                return null;
            }
        }

        private static Type FindTypeInAssemblies(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(name);
                if (t != null) return t;
            }

            return null;
        }

        private static FieldInfo FindField(Type cfgType, string name)
        {
            for (var t = cfgType; t != null && t != typeof(object); t = t.BaseType)
            {
                var f = t.GetField(name, CfgFlags);
                if (f != null) return f;
            }

            return null;
        }

        private static bool TryConvert(object raw, Type targetType, out object converted, out string reason)
        {
            reason = null;

            if (raw == null)
            {
                converted = targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
                return !targetType.IsValueType;
            }

            if (targetType.IsInstanceOfType(raw))
            {
                converted = raw;
                return true;
            }

            // Vector 家族：cfg.vectorN 与 UnityEngine.VectorN 字段名一致
            if (TryConvertVector(raw, targetType, out converted))
                return true;

            // 数组 / List
            if (raw is IEnumerable enumerable && !(raw is string))
            {
                if (TryConvertEnumerable(enumerable, targetType, out converted, out reason))
                    return true;
            }

            // 枚举
            if (targetType.IsEnum && raw is string enumName)
            {
                try
                {
                    converted = Enum.Parse(targetType, enumName);
                    return true;
                }
                catch (ArgumentException)
                {
                    reason = $"'{enumName}' 不是 {targetType.Name} 的合法值";
                    return false;
                }
            }

            // 基础类型直接转换（fg 侧 int/float/bool 与运行时一致，但可能装箱类型不同）
            try
            {
                converted = Convert.ChangeType(raw, targetType);
                return true;
            }
            catch (Exception e)
            {
                reason = e.Message;
                converted = null;
                return false;
            }
        }

        private static bool TryConvertVector(object raw, Type targetType, out object converted)
        {
            converted = null;
            if (targetType != typeof(Vector2) && targetType != typeof(Vector3) && targetType != typeof(Vector4))
                return false;

            var srcType = raw.GetType();
            var x = ReadFloat(srcType, raw, "x");
            var y = ReadFloat(srcType, raw, "y");
            if (x == null || y == null) return false;

            if (targetType == typeof(Vector2))
            {
                converted = new Vector2(x.Value, y.Value);
                return true;
            }

            var z = ReadFloat(srcType, raw, "z");
            if (z == null) return false;

            if (targetType == typeof(Vector3))
            {
                converted = new Vector3(x.Value, y.Value, z.Value);
                return true;
            }

            var w = ReadFloat(srcType, raw, "w");
            if (w == null) return false;

            converted = new Vector4(x.Value, y.Value, z.Value, w.Value);
            return true;
        }

        private static float? ReadFloat(Type type, object instance, string fieldName)
        {
            var f = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (f == null) return null;

            var v = f.GetValue(instance);
            return v == null ? null : Convert.ToSingle(v);
        }

        private static bool TryConvertEnumerable(IEnumerable source, Type targetType,
            out object converted, out string reason)
        {
            reason = null;
            converted = null;

            var elementType = targetType.IsArray
                ? targetType.GetElementType()
                : targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>)
                    ? targetType.GetGenericArguments()[0]
                    : null;

            if (elementType == null)
            {
                reason = $"不支持的目标集合类型 {targetType.Name}";
                return false;
            }

            var items = new List<object>();
            foreach (var item in source)
            {
                if (!TryConvert(item, elementType, out var convertedItem, out var itemReason))
                {
                    reason = $"元素转换失败: {itemReason}";
                    return false;
                }

                items.Add(convertedItem);
            }

            if (targetType.IsArray)
            {
                var array = Array.CreateInstance(elementType, items.Count);
                for (var i = 0; i < items.Count; i++)
                    array.SetValue(items[i], i);
                converted = array;
                return true;
            }

            var list = (IList)Activator.CreateInstance(targetType);
            foreach (var item in items)
                list.Add(item);
            converted = list;
            return true;
        }

        private static bool InvokeSetter(object target, ConfiguredMember member, object value, string path)
        {
            if (!string.IsNullOrEmpty(member.Bean.Setter))
            {
                if (!TryInvokeMethod(target, member.Bean.Setter, value, out var reason))
                {
                    Debug.LogError($"[XParamFiller] {path}.{member.Name}: 调用 {member.Bean.Setter} 失败（{reason}）");
                    return false;
                }

                return true;
            }

            // 无 setter 声明时直接写字段
            try
            {
                member.SetValue(target, value);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[XParamFiller] {path}.{member.Name}: 直接赋值失败（{e.Message}）");
                return false;
            }
        }

        /// <summary>
        ///     取 setter 方法的首个参数类型。取不到时返回 null，调用方回退到字段类型。
        /// </summary>
        private static Type ResolveSetterParamType(object target, string setterName)
        {
            if (string.IsNullOrEmpty(setterName))
                return null;

            var method = target.GetType().GetMethod(setterName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            var parameters = method?.GetParameters();
            return parameters is { Length: 1 } ? parameters[0].ParameterType : null;
        }

        private static bool TryInvokeMethod(object target, string methodName, object arg, out string reason)
        {
            reason = null;

            if (string.IsNullOrEmpty(methodName))
            {
                reason = "方法名为空";
                return false;
            }

            var method = target.GetType().GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null)
            {
                reason = $"找不到方法 {methodName}";
                return false;
            }

            try
            {
                method.Invoke(target, new[] { arg });
                return true;
            }
            catch (TargetInvocationException e)
            {
                reason = e.InnerException?.Message ?? e.Message;
                return false;
            }
            catch (ArgumentException e)
            {
                reason = e.Message;
                return false;
            }
        }

        private readonly struct ConfiguredMember
        {
            public readonly MemberInfo Member;
            public readonly BeanFieldAttribute Bean;
            public readonly BeanPolymorphicFieldAttribute Polymorphic;

            public ConfiguredMember(MemberInfo member, BeanFieldAttribute bean,
                BeanPolymorphicFieldAttribute polymorphic)
            {
                Member = member;
                Bean = bean;
                Polymorphic = polymorphic;
            }

            public string Name => Member.Name;

            public Type MemberType => Member is FieldInfo f ? f.FieldType : ((PropertyInfo)Member).PropertyType;

            public void SetValue(object target, object value)
            {
                if (Member is FieldInfo f)
                    f.SetValue(target, value);
                else
                    ((PropertyInfo)Member).SetValue(target, value);
            }
        }
    }
}
