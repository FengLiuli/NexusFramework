using NexusFramework;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using NexusFramework.GAS.Services;
using NUnit.Framework;
using Unity.Entities;

namespace NexusFramework.GAS.Tests.Services
{
    [TestFixture]
    public class AbilityServiceTests
    {
        private TestArchitecture _arch;
        private AbilityService _abilityService;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _arch = new TestArchitecture();
            _arch.Initialize();
            _arch.GetCarrierManager().RegisterType("TestUnit");
            _abilityService = _arch.GetService<AbilityService>();
            _em = _arch.GetService<WorldService>().EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            _arch?.Dispose();
            _arch = null;
        }

        // ── 容错 ────────────────────────────────────

        [Test]
        public void GrantAbility_WithNonExistentConfig_DoesNotThrow()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            Assert.DoesNotThrow(() =>
            {
                _abilityService.GrantAbility(carrier, abilityCode: 99999, _arch);
            });
        }

        [Test]
        public void TryActivate_WithInvalidCarrier_ReturnsFalse()
        {
            Assert.That(
                _abilityService.TryActivate(new CarrierId(), abilityCode: 1),
                Is.False);
        }

        [Test]
        public void IsActive_WithInvalidCarrier_ReturnsFalse()
        {
            Assert.That(
                _abilityService.IsActive(new CarrierId(), abilityCode: 1),
                Is.False);
        }

        // ── 手动创建的 Ability Entity ──────────────

        [Test]
        public void TryActivate_OnManualAbility_MarksEntity()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var ownerEntity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);

            var abilityEntity = CreateMinimalAbilityEntity(ownerEntity, abilityCode: 100);
            _em.GetBuffer<BAbility>(ownerEntity).Add(new BAbility { Ability = abilityEntity });

            var result = _abilityService.TryActivate(carrier, abilityCode: 100);
            Assert.That(result, Is.True);
            Assert.That(_em.HasComponent<CAbilityInTryActivate>(abilityEntity), Is.True);
        }

        [Test]
        public void IsActive_AfterManualActivation_ReflectsState()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var ownerEntity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);

            var abilityEntity = CreateMinimalAbilityEntity(ownerEntity, abilityCode: 200);
            _em.GetBuffer<BAbility>(ownerEntity).Add(new BAbility { Ability = abilityEntity });

            _abilityService.TryActivate(carrier, abilityCode: 200);
            Assert.That(_abilityService.IsActive(carrier, abilityCode: 200), Is.False);

            _em.AddComponent<CAbilityActive>(abilityEntity);
            Assert.That(_abilityService.IsActive(carrier, abilityCode: 200), Is.True);
        }

        [Test]
        public void TryEnd_Marks_Ability_For_End()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var ownerEntity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);

            var abilityEntity = CreateMinimalAbilityEntity(ownerEntity, abilityCode: 300);
            _em.GetBuffer<BAbility>(ownerEntity).Add(new BAbility { Ability = abilityEntity });

            _abilityService.TryEnd(carrier, abilityCode: 300);
            Assert.That(_em.HasComponent<CAbilityInTryEnd>(abilityEntity), Is.True);
        }

        [Test]
        public void TryCancel_Marks_Ability_For_Cancel()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var ownerEntity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);

            var abilityEntity = CreateMinimalAbilityEntity(ownerEntity, abilityCode: 400);
            _em.GetBuffer<BAbility>(ownerEntity).Add(new BAbility { Ability = abilityEntity });

            _abilityService.TryCancel(carrier, abilityCode: 400);
            Assert.That(_em.HasComponent<CAbilityInTryCancel>(abilityEntity), Is.True);
        }

        [Test]
        public void RemoveAbility_Destroys_Entity()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var ownerEntity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);

            var abilityEntity = CreateMinimalAbilityEntity(ownerEntity, abilityCode: 500);
            _em.GetBuffer<BAbility>(ownerEntity).Add(new BAbility { Ability = abilityEntity });

            _abilityService.RemoveAbility(carrier, abilityCode: 500);
            Assert.That(_em.Exists(abilityEntity), Is.False);
        }

        // ── 辅助方法 ────────────────────────────────

        private Entity CreateMinimalAbilityEntity(Entity owner, int abilityCode)
        {
            var entity = _em.CreateEntity();
            _em.AddComponent<CAbilityBaseInfo>(entity);
            _em.SetComponentData(entity, new CAbilityBaseInfo
            {
                Code = abilityCode,
                Owner = owner,
                Level = 1
            });
            return entity;
        }
    }
}
