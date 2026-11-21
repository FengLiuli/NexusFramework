using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using UnityDebug = UnityEngine.Debug;

namespace NexusFramework.Core.Tests.Performance
{
    // ═══════════════════════════════════════════════════════════════════
    //  通用 ID 句柄：裸 int 包装 vs 泛型强类型包装 的性能评估
    //
    //  背景：通用 ID 方案需要决定运行时句柄对调用方暴露的形态。
    //    A. BareHandle          —— readonly struct { int Value }
    //    B. TypedHandle<TTag>   —— 同类结构，但带泛型标记防混用
    //
    //  本基准只测「同一份数据、同一种操作」在两种形态下的差异，
    //  不测域注册/字符串 intern（那部分与句柄形态无关）。
    //
    //  覆盖场景：
    //    ① 顺序遍历读取
    //    ② 相等比较（含 operator==）
    //    ③ GetHashCode
    //    ④ Dictionary 查找（最真实的 ID 使用路径）
    //    ⑤ 泛型约束方法（where T : struct, IEquatable<T>）
    //    ⑥ NativeArray 托管（ECS 场景）
    //    ⑦ 未实现 IEquatable 的泛型对照（暴露默认 ValueType.Equals 的代价）
    //
    //  ⚠ 数据来源是 Editor + Mono，不是 IL2CPP Player。泛型值类型在
    //    Mono 与 IL2CPP 下的特化策略不同，结论需在 Player 侧复测。
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>候选 A：无泛型标记的裸句柄。</summary>
    internal readonly struct BareHandle : IEquatable<BareHandle>
    {
        public readonly int Value;

        public BareHandle(int value)
        {
            Value = value;
        }

        public bool Equals(BareHandle other) => Value == other.Value;
        public override bool Equals(object obj) => obj is BareHandle other && Equals(other);
        public override int GetHashCode() => Value;

        public static bool operator ==(BareHandle left, BareHandle right) => left.Value == right.Value;
        public static bool operator !=(BareHandle left, BareHandle right) => left.Value != right.Value;
    }

    /// <summary>候选 B：带泛型标记的强类型句柄。</summary>
    internal readonly struct TypedHandle<TTag> : IEquatable<TypedHandle<TTag>>
        where TTag : struct
    {
        public readonly int Value;

        public TypedHandle(int value)
        {
            Value = value;
        }

        public bool Equals(TypedHandle<TTag> other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TypedHandle<TTag> other && Equals(other);
        public override int GetHashCode() => Value;

        public static bool operator ==(TypedHandle<TTag> left, TypedHandle<TTag> right) => left.Value == right.Value;
        public static bool operator !=(TypedHandle<TTag> left, TypedHandle<TTag> right) => left.Value != right.Value;
    }

    /// <summary>对照：泛型标记但未实现 IEquatable，用于暴露默认 ValueType.Equals 的代价。</summary>
    internal readonly struct TypedHandleNoEq<TTag>
        where TTag : struct
    {
        public readonly int Value;

        public TypedHandleNoEq(int value)
        {
            Value = value;
        }
    }

    // 强类型标记（真实模块会各自声明自己的标记）
    internal struct TagItem
    {
    }

    internal struct TagEffect
    {
    }

    internal struct TagSkill
    {
    }

    [TestFixture]
    [Category("Performance")]
    public class IdHandleBenchmarkTests
    {
        private const int SimpleOps = 10_000_000;
        private const int EqualityOps = 5_000_000;
        private const int HashOps = 10_000_000;
        private const int DictScale = 100_000;
        private const int DictOps = 3_000_000;
        private const int NativeScale = 1_000_000;

        // 防止被测结果被 JIT 视为死代码。
        private static long s_sink;

        private static string ResultFilePath =>
            Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", "TestResults", "id-handle-perf.txt"));

        [OneTimeSetUp]
        public void ResetResults()
        {
            Perf.Lines.Clear();
            var dir = Path.GetDirectoryName(ResultFilePath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(ResultFilePath,
                $"# ID handle perf (EditMode, Mono/Editor) {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        }

        [OneTimeTearDown]
        public void DumpResults()
        {
            File.WriteAllLines(ResultFilePath, Perf.Lines);
            UnityDebug.Log($"[PERF] results written to {ResultFilePath}");
        }

        #region 数据构造

        private static BareHandle[] CreateBare(int n)
        {
            var ids = new BareHandle[n];
            for (var i = 0; i < n; i++) ids[i] = new BareHandle(i + 1);
            return ids;
        }

        private static TypedHandle<TagItem>[] CreateTyped(int n)
        {
            var ids = new TypedHandle<TagItem>[n];
            for (var i = 0; i < n; i++) ids[i] = new TypedHandle<TagItem>(i + 1);
            return ids;
        }

        private static TypedHandleNoEq<TagItem>[] CreateTypedNoEq(int n)
        {
            var ids = new TypedHandleNoEq<TagItem>[n];
            for (var i = 0; i < n; i++) ids[i] = new TypedHandleNoEq<TagItem>(i + 1);
            return ids;
        }

        #endregion

        #region 1. 顺序遍历

        [Test]
        public void SequentialSum_Bare()
        {
            var ids = CreateBare(SimpleOps);
            Perf.Run("SequentialSum Bare", SimpleOps, () =>
            {
                long sum = 0;
                for (var i = 0; i < ids.Length; i++) sum += ids[i].Value;
                s_sink += sum;
            });
        }

        [Test]
        public void SequentialSum_Typed()
        {
            var ids = CreateTyped(SimpleOps);
            Perf.Run("SequentialSum Typed", SimpleOps, () =>
            {
                long sum = 0;
                for (var i = 0; i < ids.Length; i++) sum += ids[i].Value;
                s_sink += sum;
            });
        }

        #endregion

        #region 2. 相等比较

        [Test]
        public void Equality_Bare()
        {
            var ids = CreateBare(EqualityOps);
            Perf.Run("Equality Bare", EqualityOps, () =>
            {
                long count = 0;
                for (var i = 0; i < ids.Length - 1; i++)
                    if (ids[i] == ids[i + 1]) count++;
                s_sink += count;
            });
        }

        [Test]
        public void Equality_Typed()
        {
            var ids = CreateTyped(EqualityOps);
            Perf.Run("Equality Typed", EqualityOps, () =>
            {
                long count = 0;
                for (var i = 0; i < ids.Length - 1; i++)
                    if (ids[i] == ids[i + 1]) count++;
                s_sink += count;
            });
        }

        [Test]
        public void Equality_TypedNoEq()
        {
            var ids = CreateTypedNoEq(EqualityOps);
            Perf.Run("Equality TypedNoEq (ValueType.Equals)", EqualityOps, () =>
            {
                long count = 0;
                for (var i = 0; i < ids.Length - 1; i++)
                    if (ids[i].Equals(ids[i + 1])) count++;
                s_sink += count;
            });
        }

        #endregion

        #region 3. GetHashCode

        [Test]
        public void HashCode_Bare()
        {
            var ids = CreateBare(HashOps);
            Perf.Run("GetHashCode Bare", HashOps, () =>
            {
                long sum = 0;
                for (var i = 0; i < ids.Length; i++) sum += ids[i].GetHashCode();
                s_sink += sum;
            });
        }

        [Test]
        public void HashCode_Typed()
        {
            var ids = CreateTyped(HashOps);
            Perf.Run("GetHashCode Typed", HashOps, () =>
            {
                long sum = 0;
                for (var i = 0; i < ids.Length; i++) sum += ids[i].GetHashCode();
                s_sink += sum;
            });
        }

        [Test]
        public void HashCode_TypedNoEq()
        {
            var ids = CreateTypedNoEq(HashOps);
            Perf.Run("GetHashCode TypedNoEq (ValueType.GetHashCode)", HashOps, () =>
            {
                long sum = 0;
                for (var i = 0; i < ids.Length; i++) sum += ids[i].GetHashCode();
                s_sink += sum;
            });
        }

        #endregion

        #region 4. Dictionary 查找

        private static Dictionary<TKey, int> BuildDict<TKey>(Func<int, TKey> factory, int scale)
        {
            var dict = new Dictionary<TKey, int>(scale);
            for (var i = 0; i < scale; i++) dict[factory(i)] = i;
            return dict;
        }

        private static TKey[] BuildRandomKeys<TKey>(Func<int, TKey> factory, int count, int modulus)
        {
            var keys = new TKey[count];
            var rng = new Random(12345);
            for (var i = 0; i < count; i++) keys[i] = factory(rng.Next(modulus));
            return keys;
        }

        [Test]
        public void Dictionary_Bare()
        {
            var dict = BuildDict(i => new BareHandle(i), DictScale);
            var keys = BuildRandomKeys(i => new BareHandle(i), DictOps, DictScale);

            Perf.Run("DictionaryLookup Bare", DictOps, () =>
            {
                long sum = 0;
                for (var i = 0; i < keys.Length; i++)
                    if (dict.TryGetValue(keys[i], out var value)) sum += value;
                s_sink += sum;
            });
        }

        [Test]
        public void Dictionary_Typed()
        {
            var dict = BuildDict(i => new TypedHandle<TagItem>(i), DictScale);
            var keys = BuildRandomKeys(i => new TypedHandle<TagItem>(i), DictOps, DictScale);

            Perf.Run("DictionaryLookup Typed", DictOps, () =>
            {
                long sum = 0;
                for (var i = 0; i < keys.Length; i++)
                    if (dict.TryGetValue(keys[i], out var value)) sum += value;
                s_sink += sum;
            });
        }

        [Test]
        public void Dictionary_TypedNoEq()
        {
            var dict = BuildDict(i => new TypedHandleNoEq<TagItem>(i), DictScale);
            var keys = BuildRandomKeys(i => new TypedHandleNoEq<TagItem>(i), DictOps, DictScale);

            Perf.Run("DictionaryLookup TypedNoEq", DictOps, () =>
            {
                long sum = 0;
                for (var i = 0; i < keys.Length; i++)
                    if (dict.TryGetValue(keys[i], out var value)) sum += value;
                s_sink += sum;
            });
        }

        #endregion

        #region 5. 泛型约束方法

        /// <summary>模拟通用层内部按 IEquatable 约束处理任意句柄的代码路径。</summary>
        private static long CountAdjacentEqual<T>(T[] ids) where T : struct, IEquatable<T>
        {
            long count = 0;
            for (var i = 0; i < ids.Length - 1; i++)
                if (ids[i].Equals(ids[i + 1])) count++;
            return count;
        }

        [Test]
        public void GenericConstraint_Bare()
        {
            var ids = CreateBare(EqualityOps);
            Perf.Run("GenericConstraint Bare", EqualityOps, () => s_sink += CountAdjacentEqual(ids));
        }

        [Test]
        public void GenericConstraint_Typed()
        {
            var ids = CreateTyped(EqualityOps);
            Perf.Run("GenericConstraint Typed", EqualityOps, () => s_sink += CountAdjacentEqual(ids));
        }

        #endregion

        #region 6. NativeArray（ECS 托管）

        [Test]
        public void NativeArray_Bare()
        {
            var array = new NativeArray<BareHandle>(NativeScale, Allocator.Temp);
            for (var i = 0; i < array.Length; i++) array[i] = new BareHandle(i + 1);

            Perf.Run("NativeArray Bare", NativeScale, () =>
            {
                long sum = 0;
                for (var i = 0; i < array.Length; i++) sum += array[i].Value;
                s_sink += sum;
            });

            array.Dispose();
        }

        [Test]
        public void NativeArray_Typed()
        {
            var array = new NativeArray<TypedHandle<TagItem>>(NativeScale, Allocator.Temp);
            for (var i = 0; i < array.Length; i++) array[i] = new TypedHandle<TagItem>(i + 1);

            Perf.Run("NativeArray Typed", NativeScale, () =>
            {
                long sum = 0;
                for (var i = 0; i < array.Length; i++) sum += array[i].Value;
                s_sink += sum;
            });

            array.Dispose();
        }

        #endregion
    }
}
