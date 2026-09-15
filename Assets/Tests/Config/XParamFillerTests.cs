using System.Collections.Generic;
using NexusFramework.GAS.ECS;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace NexusFramework.GAS.Tests.Config
{
    /// <summary>
    ///     XParamFiller：把配置对象的字段按 <see cref="BeanFieldAttribute"/> /
    ///     <see cref="BeanPolymorphicFieldAttribute"/> 声明搬进运行时 XParam。
    ///     <para>
    ///         这里用本地替身类型而非真实的 <c>cfg.*</c>，原因有二：
    ///         ① 测试程序集不引用 Luban，避免为测试扩大程序集依赖；
    ///         ② 填充器的契约是"按特性声明搬运"，与 JSON 反序列化无关，
    ///            替身类型能更精确地锁定这一点。
    ///     </para>
    ///     <para>
    ///         cfg 侧与运行时的真实对接由 <c>LubanConfigLoaderGeneratorTests</c> 的
    ///         生成产物断言覆盖。
    ///     </para>
    /// </summary>
    [TestFixture]
    public class XParamFillerTests
    {
        [SetUp]
        public void SetUp()
        {
            TargetCatcherHelper.RegisterTargetCatcher("CatchSelf", typeof(CatchSelf), typeof(XParamNone));
            TargetCatcherHelper.RegisterTargetCatcher("CatchAreaBox3D", typeof(CatchAreaBox3D),
                typeof(XParamCatchAreaBox3D));
        }

        #region 标量字段

        [Test]
        public void Fill_IntArray_TransfersValues()
        {
            var target = new XParamEffectIDs();
            var source = new FakeEffectIds { IDs = new[] { 1001, 1002, 1003 } };

            Assert.That(XParamFiller.Fill(target, source), Is.True);
            Assert.That(target.IDs, Is.EqualTo(new[] { 1001, 1002, 1003 }));
        }

        [Test]
        public void Fill_IntArrayEmpty_ProducesEmptyArray()
        {
            var target = new XParamEffectIDs();
            var source = new FakeEffectIds { IDs = new int[0] };

            Assert.That(XParamFiller.Fill(target, source), Is.True);
            Assert.That(target.IDs, Is.Empty);
        }

        [Test]
        public void Fill_String_TransfersValue()
        {
            var target = new XParamString();
            var source = new FakeString { Value = "hello" };

            Assert.That(XParamFiller.Fill(target, source), Is.True);
            Assert.That(target.Value, Is.EqualTo("hello"));
        }

        [Test]
        public void Fill_Float_TransfersValue()
        {
            var target = new XParamFloat();
            var source = new FakeFloat { Value = 42.5f };

            Assert.That(XParamFiller.Fill(target, source), Is.True);
            Assert.That(target.Value, Is.EqualTo(42.5f).Within(0.001f));
        }

        [Test]
        public void Fill_Int_TransfersValue()
        {
            var target = new XParamInt();
            var source = new FakeInt { Value = 7 };

            Assert.That(XParamFiller.Fill(target, source), Is.True);
            Assert.That(target.Value, Is.EqualTo(7));
        }

        [Test]
        public void Fill_Bool_TransfersValue()
        {
            var target = new XParamBool();
            var source = new FakeBool { Value = true };

            Assert.That(XParamFiller.Fill(target, source), Is.True);
            Assert.That(target.Value, Is.True);
        }

        #endregion

        #region Vector 适配

        [Test]
        public void Fill_Vector3_ConvertsFromConfigVector()
        {
            var target = new XParamVector3();
            var source = new FakeVector3 { Value = new FakeVec3(1.5f, 2.5f, 3.5f) };

            Assert.That(XParamFiller.Fill(target, source), Is.True);
            Assert.That(target.Value, Is.EqualTo(new Vector3(1.5f, 2.5f, 3.5f)));
        }

        [Test]
        public void Fill_Vector2_ConvertsFromConfigVector()
        {
            var target = new XParamVector2();
            var source = new FakeVector2 { Value = new FakeVec2(4f, 5f) };

            Assert.That(XParamFiller.Fill(target, source), Is.True);
            Assert.That(target.Value, Is.EqualTo(new Vector2(4f, 5f)));
        }

        #endregion

        #region 健壮性

        [Test]
        public void Fill_NullTarget_ReturnsFalseWithoutThrowing()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"XParamFiller"));
            Assert.That(XParamFiller.Fill(null, new FakeInt { Value = 1 }), Is.False);
        }

        [Test]
        public void Fill_NullSource_ReturnsFalseWithoutThrowing()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"XParamFiller"));
            Assert.That(XParamFiller.Fill(new XParamInt(), null), Is.False);
        }

        [Test]
        public void Fill_SourceMissingField_LogsAndReturnsFalse()
        {
            var target = new XParamEffectIDs();
            // 源对象没有 IDs 字段——应显式报错而非静默
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"XParamFiller"));
            Assert.That(XParamFiller.Fill(target, new object()), Is.False);
        }

        [Test]
        public void Fill_TargetWithNoConfiguredFields_ReturnsTrue()
        {
            // XParamNone 没有任何 [BeanField]，填充应为无操作
            Assert.That(XParamFiller.Fill(new XParamNone(), new object()), Is.True);
        }

        #endregion

        #region 多态字段

        [Test]
        public void Fill_PolymorphicField_SplitsTypeAndParam()
        {
            var target = new XParamApplyEffects();
            var source = new FakeApplyEffects
            {
                IDs = new[] { 1001 },
                TargetCatcher = new CatchSelf()
            };

            Assert.That(XParamFiller.Fill(target, source), Is.True);
            Assert.That(target.IDs, Is.EqualTo(new[] { 1001 }));
            Assert.That(target.CatcherType, Is.EqualTo(nameof(CatchSelf)));
        }

        [Test]
        public void Fill_PolymorphicField_NestedParamIsFilled()
        {
            var target = new XParamApplyEffects();
            var source = new FakeApplyEffects
            {
                IDs = new[] { 1001 },
                TargetCatcher = new CatchAreaBox3D
                {
                    Param = new FakeBoxParam
                    {
                        IsWorldSpace = true,
                        Offset = new FakeVec3(1, 2, 3),
                        Size = new FakeVec3(4, 5, 6),
                        Rotation = new FakeVec3(0, 0, 0),
                        Layer = 1
                    }
                }
            };

            Assert.That(XParamFiller.Fill(target, source), Is.True);
            Assert.That(target.CatcherType, Is.EqualTo(nameof(CatchAreaBox3D)));
            Assert.That(target.Param, Is.InstanceOf<XParamCatchAreaBox3D>());

            var box = (XParamCatchAreaBox3D)target.Param;
            Assert.That(box.isWorldSpace, Is.True);
            Assert.That(box.offset, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(box.size, Is.EqualTo(new Vector3(4, 5, 6)));
            Assert.That(box.layer.value, Is.EqualTo(1));
        }

        [Test]
        public void Fill_PolymorphicField_NullSubObject_KeepsDefault()
        {
            var target = new XParamApplyEffects();
            var source = new FakeApplyEffects { IDs = new[] { 1 }, TargetCatcher = null };

            Assert.That(XParamFiller.Fill(target, source), Is.True);
            Assert.That(target.CatcherType, Is.EqualTo(string.Empty));
            Assert.That(target.Param, Is.Null);
        }

        [Test]
        public void Fill_PolymorphicField_UnknownType_ReturnsFalse()
        {
            var target = new XParamApplyEffects();
            var source = new FakeApplyEffects
            {
                IDs = new[] { 1 },
                TargetCatcher = new NoSuchCatcher()
            };

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"XParamFiller"));
            Assert.That(XParamFiller.Fill(target, source), Is.False);
        }

        #endregion

        #region 测试替身

        private class FakeVec2
        {
            public float x;
            public float y;

            public FakeVec2(float x, float y)
            {
                this.x = x;
                this.y = y;
            }
        }

        private class FakeVec3
        {
            public float x;
            public float y;
            public float z;

            public FakeVec3(float x, float y, float z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
        }

        private class FakeInt
        {
            public int Value;
        }

        private class FakeFloat
        {
            public float Value;
        }

        private class FakeBool
        {
            public bool Value;
        }

        private class FakeString
        {
            public string Value;
        }

        private class FakeVector2
        {
            public FakeVec2 Value;
        }

        private class FakeVector3
        {
            public FakeVec3 Value;
        }

        private class FakeEffectIds
        {
            public int[] IDs;
        }

        // 真实的 cfg.TargetCatcherBase 子类，其类名即逻辑类型名
        // （XParamFiller 用 GetType().Name 取类型判别符）。
        // 替身必须复刻这一形态，因此用多个小类而非带 Kind 字段的单类。
        private class CatchSelf
        {
        }

        private class NoSuchCatcher
        {
        }

        private class CatchAreaBox3D
        {
            public FakeBoxParam Param;
        }

        private class FakeApplyEffects
        {
            public int[] IDs;
            public object TargetCatcher;
        }

        private class FakeBoxParam
        {
            public bool IsWorldSpace;
            public FakeVec3 Offset;
            public FakeVec3 Size;
            public FakeVec3 Rotation;
            public int Layer;
        }

        #endregion
    }
}
