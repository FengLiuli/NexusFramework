using NexusFramework;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using NexusFramework.GAS.Services;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace NexusFramework.GAS.Tests
{
    [TestFixture]
    public class EntityGameObjectBindingTests
    {
        private TestArchitecture _arch;
        private GASEntityMapModel _model;
        private GameObject _testGo;

        [SetUp]
        public void SetUp()
        {
            _arch = new TestArchitecture();
            _arch.Initialize();
            _arch.GetCarrierManager().RegisterType("TestUnit");
            _model = _arch.GetModel<GASEntityMapModel>();
            _testGo = new GameObject("TestBindingGO");
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGo != null)
                Object.DestroyImmediate(_testGo);
            _arch?.Dispose();
            _arch = null;
        }

        // ── IGASEntityResolver 接口可用 ────────────────

        [Test]
        public void Model_Implements_IGASEntityResolver()
        {
            Assert.That(_model, Is.InstanceOf<IGASEntityResolver>());
        }

        // ── BindGameObject 与 GetGameObject ─────────────

        [Test]
        public void BindGameObject_Then_GetGameObject_Returns_GO()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var entity = _model.GetGASEntity(carrierId);

            _model.BindGameObject(entity, _testGo);

            var result = _model.GetGameObject(entity);
            Assert.That(result, Is.EqualTo(_testGo));
        }

        [Test]
        public void GetGameObject_Without_Binding_Returns_Null()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var entity = _model.GetGASEntity(carrierId);

            Assert.That(_model.GetGameObject(entity), Is.Null);
        }

        [Test]
        public void GetGameObject_With_EntityNull_Returns_Null()
        {
            Assert.That(_model.GetGameObject(Entity.Null), Is.Null);
        }

        // ── GetEntity 反向查询 ──────────────────────────

        [Test]
        public void GetEntity_Returns_Bound_Entity()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var entity = _model.GetGASEntity(carrierId);
            _model.BindGameObject(entity, _testGo);

            var result = _model.GetEntity(_testGo);
            Assert.That(result, Is.EqualTo(entity));
        }

        [Test]
        public void GetEntity_Without_Binding_Returns_Null()
        {
            var go = new GameObject("UnboundGO");
            try
            {
                Assert.That(_model.GetEntity(go), Is.EqualTo(Entity.Null));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GetEntity_With_Null_Returns_Null()
        {
            Assert.That(_model.GetEntity(null), Is.EqualTo(Entity.Null));
        }

        // ── IsBound 检查 ────────────────────────────────

        [Test]
        public void IsEntityBound_True_After_Bind()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var entity = _model.GetGASEntity(carrierId);
            _model.BindGameObject(entity, _testGo);

            Assert.That(_model.IsEntityBound(entity), Is.True);
        }

        [Test]
        public void IsEntityBound_False_Before_Bind()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var entity = _model.GetGASEntity(carrierId);

            Assert.That(_model.IsEntityBound(entity), Is.False);
        }

        [Test]
        public void IsGameObjectBound_True_After_Bind()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var entity = _model.GetGASEntity(carrierId);
            _model.BindGameObject(entity, _testGo);

            Assert.That(_model.IsGameObjectBound(_testGo), Is.True);
        }

        // ── UnbindGameObject ─────────────────────────────

        [Test]
        public void UnbindGameObject_Removes_Binding()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var entity = _model.GetGASEntity(carrierId);
            _model.BindGameObject(entity, _testGo);

            _model.UnbindGameObject(entity);

            Assert.That(_model.GetGameObject(entity), Is.Null);
            Assert.That(_model.GetEntity(_testGo), Is.EqualTo(Entity.Null));
            Assert.That(_model.IsEntityBound(entity), Is.False);
        }

        [Test]
        public void UnbindGameObject_NotBound_DoesNotThrow()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var entity = _model.GetGASEntity(carrierId);

            Assert.DoesNotThrow(() => _model.UnbindGameObject(entity));
        }

        // ── 重复绑定（覆盖） ───────────────────────────

        [Test]
        public void Rebind_SameEntity_Replaces_OldBinding()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var entity = _model.GetGASEntity(carrierId);
            var go1 = new GameObject("Go1");
            var go2 = new GameObject("Go2");
            try
            {
                _model.BindGameObject(entity, go1);
                _model.BindGameObject(entity, go2);

                Assert.That(_model.GetGameObject(entity), Is.EqualTo(go2));
                Assert.That(_model.GetEntity(go1), Is.EqualTo(Entity.Null));
                Assert.That(_model.GetEntity(go2), Is.EqualTo(entity));
            }
            finally
            {
                Object.DestroyImmediate(go1);
                Object.DestroyImmediate(go2);
            }
        }

        // ── 同一 GO 绑定不同 Entity（拒绝） ────────────

        [Test]
        public void Bind_DifferentEntity_To_SameGO_IsRejected()
        {
            var c1 = _arch.CreateGASCarrier("TestUnit");
            var c2 = _arch.CreateGASCarrier("TestUnit");
            var e1 = _model.GetGASEntity(c1);
            var e2 = _model.GetGASEntity(c2);

            _model.BindGameObject(e1, _testGo);
            _model.BindGameObject(e2, _testGo); // 应被拒绝

            Assert.That(_model.GetEntity(_testGo), Is.EqualTo(e1),
                "应保留第一次绑定，拒绝第二次");
        }

        // ── 空值防御 ───────────────────────────────────

        [Test]
        public void BindGameObject_With_NullEntity_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _model.BindGameObject(Entity.Null, _testGo));
        }

        [Test]
        public void BindGameObject_With_NullGO_DoesNotThrow()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var entity = _model.GetGASEntity(carrierId);
            Assert.DoesNotThrow(() => _model.BindGameObject(entity, null));
        }

        // ── GASEntityRef 自动附加 ──────────────────────

        [Test]
        public void BindGameObject_AutoAdds_GASEntityRef()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var entity = _model.GetGASEntity(carrierId);

            _model.BindGameObject(entity, _testGo);

            var refComp = _testGo.GetComponent<GASEntityRef>();
            Assert.That(refComp, Is.Not.Null);
            Assert.That(refComp.Entity, Is.EqualTo(entity));
            Assert.That(refComp.Resolver, Is.EqualTo(_model));
        }

        [Test]
        public void UnbindGameObject_Marks_GASEntityRef_Unbound()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var entity = _model.GetGASEntity(carrierId);

            _model.BindGameObject(entity, _testGo);
            var refComp = _testGo.GetComponent<GASEntityRef>();

            _model.UnbindGameObject(entity);

            Assert.That(refComp.Entity, Is.EqualTo(Entity.Null));
            Assert.That(refComp.Resolver, Is.Null);
        }

        // ── GameObject 过期检测 ─────────────────────────

        [Test]
        public void GetGameObject_ReturnsNull_WhenGO_Destroyed()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var entity = _model.GetGASEntity(carrierId);
            var tempGo = new GameObject("TempBindingGO");
            _model.BindGameObject(entity, tempGo);

            Object.DestroyImmediate(tempGo);

            var result = _model.GetGameObject(entity);
            Assert.That(result, Is.Null, "已销毁的 GameObject 应返回 null");
        }

        // ── DestroyGASCarrier 自动解绑 ──────────────────

        [Test]
        public void DestroyGASCarrier_Auto_Unbinds_GameObject()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit", _testGo);
            var entity = _model.GetGASEntity(carrierId);

            Assert.That(_model.IsEntityBound(entity), Is.True);

            _arch.DestroyGASCarrier(carrierId);

            Assert.That(_model.IsEntityBound(entity), Is.False);
            Assert.That(_model.GetGameObject(entity), Is.Null);
        }

        // ── BindGameObjectForCarrier 便捷方法 ───────────

        [Test]
        public void BindGameObjectForCarrier_Works()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            _arch.BindGameObjectForCarrier(carrierId, _testGo);

            var entity = _model.GetGASEntity(carrierId);
            Assert.That(_model.GetGameObject(entity), Is.EqualTo(_testGo));
        }

        [Test]
        public void CreateGASCarrier_With_GameObject_AutomaticallyBinds()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit", _testGo);
            var entity = _model.GetGASEntity(carrierId);

            Assert.That(_model.GetGameObject(entity), Is.EqualTo(_testGo));
        }

        // ── 多架构隔离 ──────────────────────────────────

        [Test]
        public void Multiple_Architectures_Have_IndependentBindings()
        {
            var c1 = _arch.CreateGASCarrier("TestUnit", _testGo);
            var e1 = _model.GetGASEntity(c1);

            // 第二个架构
            var arch2 = new TestArchitecture();
            arch2.Initialize();
            arch2.GetCarrierManager().RegisterType("TestUnit2");
            var model2 = arch2.GetModel<GASEntityMapModel>();
            var go2 = new GameObject("Arch2GO");
            try
            {
                var c2 = arch2.CreateGASCarrier("TestUnit2", go2);
                var e2 = model2.GetGASEntity(c2);

                // 两个架构绑定独立
                Assert.That(_model.GetGameObject(e1), Is.EqualTo(_testGo));
                Assert.That(model2.GetGameObject(e2), Is.EqualTo(go2));

                // 架构 A 的 Model 查不到架构 B 的 Entity
                Assert.That(_model.IsEntityBound(e2), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go2);
                arch2.Dispose();
            }
        }

        // ── GASEntityRef OnDestroy 兜底解绑 ─────────────

        [Test]
        public void GASEntityRef_OnDestroy_AutoUnbinds()
        {
            var tempGo = new GameObject("TempForOnDestroy");
            var carrierId = _arch.CreateGASCarrier("TestUnit", tempGo);
            var entity = _model.GetGASEntity(carrierId);

            Assert.That(_model.IsEntityBound(entity), Is.True);

            Object.DestroyImmediate(tempGo); // 触发 GASEntityRef.OnDestroy

            Assert.That(_model.IsEntityBound(entity), Is.False);
        }
    }
}
