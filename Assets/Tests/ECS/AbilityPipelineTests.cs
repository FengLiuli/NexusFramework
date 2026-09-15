using NexusFramework;
using Unity.Collections;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using NexusFramework.GAS.Services;
using NUnit.Framework;
using Unity.Entities;

namespace NexusFramework.GAS.Tests.ECS
{
    [TestFixture]
    public class AbilityPipelineTests
    {
        private TestArchitecture _arch;
        private AbilityService _abilityService;
        private EntityManager _em;
        private World _world;

        [SetUp]
        public void SetUp()
        {
            _arch = new TestArchitecture();
            _arch.Initialize();
            _arch.GetCarrierManager().RegisterType("TestUnit");
            _abilityService = _arch.GetService<AbilityService>();
            var ws = _arch.GetService<WorldService>();
            _em = ws.EntityManager;
            _world = ws.ExWorld;

            // 标签层级由 EcsTagService.RebuildTagMap 在架构初始化时烘焙，
            // 此处不再需要替换空映射。
        }

        [TearDown]
        public void TearDown()
        {
            _arch?.Dispose();
            _arch = null;
        }

        /// <summary>授予能力 → TryActivate → ECS 管线处理后 CAbilityActive 出现</summary>
        [Test]
        public void GrantAndActivate_AbilityBecomesActive()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);

            _abilityService.GrantAbility(carrier, abilityCode: 1, _arch);
            _abilityService.TryActivate(carrier, abilityCode: 1);
            TickWorld(frames: 10);

            Assert.That(_abilityService.IsActive(carrier, abilityCode: 1), Is.True,
                "ECS 管线处理后能力应变为 Active 状态");
        }

        [Test]
        public void GrantAndEnd_MarksForEnd()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            _abilityService.GrantAbility(carrier, abilityCode: 1, _arch);
            _abilityService.TryEnd(carrier, abilityCode: 1);

            var ownerEntity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            var buffer = _em.GetBuffer<BAbility>(ownerEntity);
            Assert.That(buffer.Length, Is.GreaterThan(0));
            Assert.That(_em.HasComponent<CAbilityInTryEnd>(buffer[0].Ability), Is.True);
        }

        [Test]
        public void GrantAndCancel_MarksForCancel()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            _abilityService.GrantAbility(carrier, abilityCode: 1, _arch);
            _abilityService.TryCancel(carrier, abilityCode: 1);

            var ownerEntity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            var buffer = _em.GetBuffer<BAbility>(ownerEntity);
            Assert.That(buffer.Length, Is.GreaterThan(0));
            Assert.That(_em.HasComponent<CAbilityInTryCancel>(buffer[0].Ability), Is.True);
        }

        private void TickWorld(int frames)
        {
            for (var i = 0; i < frames; i++)
                _world.Update();
        }
    }
}
