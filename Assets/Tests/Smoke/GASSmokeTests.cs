using NexusFramework;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using NexusFramework.GAS.Services;
using NUnit.Framework;
using Unity.Entities;

namespace NexusFramework.GAS.Tests.Smoke
{
    [TestFixture]
    public class GASSmokeTests
    {
        private TestArchitecture _arch;

        [SetUp]
        public void SetUp()
        {
            _arch = new TestArchitecture();
            _arch.Initialize();
            _arch.GetCarrierManager().RegisterType("TestUnit");
        }

        [TearDown]
        public void TearDown()
        {
            _arch?.Dispose();
            _arch = null;
        }

        // ── 架构初始化 ──────────────────────────────

        [Test]
        public void Architecture_Initializes_WithoutError()
        {
            Assert.That(_arch, Is.Not.Null);
            Assert.That(_arch.State, Is.EqualTo(ArchitectureState.Initialized));
        }

        [Test]
        public void ArchitectureType_Is_GAS()
        {
            Assert.That(_arch.ArchitectureType, Is.EqualTo("GAS"));
        }

        // ── ECS World ───────────────────────────────

        [Test]
        public void World_IsCreated_AfterInit()
        {
            var ws = _arch.GetService<WorldService>();
            Assert.That(ws.IsInitialized, Is.True);
            Assert.That(ws.ExWorld.IsCreated, Is.True);
        }

        // ── 服务可访问性 ────────────────────────────

        [Test]
        public void All_Services_Are_Accessible()
        {
            Assert.That(_arch.GetService<WorldService>(), Is.Not.Null);
            Assert.That(_arch.GetService<TagService>(), Is.Not.Null);
            Assert.That(_arch.GetService<EffectService>(), Is.Not.Null);
            Assert.That(_arch.GetService<AbilityService>(), Is.Not.Null);
            Assert.That(_arch.GetService<CueService>(), Is.Not.Null);
            Assert.That(_arch.GetService<EventBridgeService>(), Is.Not.Null);
            Assert.That(_arch.GetService<TimerService>(), Is.Not.Null);
        }

        [Test]
        public void EntityMapModel_Is_Accessible()
        {
            Assert.That(_arch.GetModel<GASEntityMapModel>(), Is.Not.Null);
        }

        // ── Carrier 与 Entity 绑定 ───────────────────

        [Test]
        public void CreateCarrier_Creates_And_Maps_Entity()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var model = _arch.GetModel<GASEntityMapModel>();
            var em = _arch.GetService<WorldService>().EntityManager;

            Assert.That(carrierId.IsValid, Is.True);
            Assert.That(model.ContainsCarrier(carrierId), Is.True);

            var entity = model.GetGASEntity(carrierId);
            Assert.That(entity, Is.Not.EqualTo(Entity.Null));
            Assert.That(em.Exists(entity), Is.True);
        }

        [Test]
        public void CreateCarrier_Adds_Required_GAS_Buffers()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrierId);
            var em = _arch.GetService<WorldService>().EntityManager;

            Assert.That(em.HasBuffer<BEAttrSet>(entity), Is.True);
            Assert.That(em.HasBuffer<BGameplayEffect>(entity), Is.True);
            Assert.That(em.HasBuffer<BAbility>(entity), Is.True);
            Assert.That(em.HasBuffer<BFixedTag>(entity), Is.True);
            Assert.That(em.HasBuffer<BTemporaryTag>(entity), Is.True);
            Assert.That(em.HasComponent<CAscBasicData>(entity), Is.True);
        }

        [Test]
        public void DestroyCarrier_CleansUp_Entity_And_Map()
        {
            var carrierId = _arch.CreateGASCarrier("TestUnit");
            var model = _arch.GetModel<GASEntityMapModel>();
            var entity = model.GetGASEntity(carrierId);
            var em = _arch.GetService<WorldService>().EntityManager;

            _arch.DestroyGASCarrier(carrierId);

            Assert.That(model.ContainsCarrier(carrierId), Is.False);
            Assert.That(em.Exists(entity), Is.False);
        }

        // ── 生命周期 ─────────────────────────────────

        [Test]
        public void Dispose_ShutsDown_World()
        {
            var ws = _arch.GetService<WorldService>();
            _arch.CreateGASCarrier("TestUnit");

            _arch.Dispose();
            _arch = null;

            Assert.That(ws.IsInitialized, Is.False);
        }

        [Test]
        public void Multiple_Carriers_Dont_Conflict()
        {
            var c1 = _arch.CreateGASCarrier("TestUnit");
            var c2 = _arch.CreateGASCarrier("TestUnit");
            var model = _arch.GetModel<GASEntityMapModel>();

            Assert.That(model.GetGASEntity(c1), Is.Not.EqualTo(Entity.Null));
            Assert.That(model.GetGASEntity(c2), Is.Not.EqualTo(Entity.Null));
            Assert.That(model.GetGASEntity(c1), Is.Not.EqualTo(model.GetGASEntity(c2)));
        }
    }
}
