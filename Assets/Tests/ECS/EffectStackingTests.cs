using NexusFramework;
using Unity.Collections;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Services;
using NUnit.Framework;
using Unity.Entities;

namespace NexusFramework.GAS.Tests.ECS
{
    [TestFixture]
    public class EffectStackingTests
    {
        private TestArchitecture _arch;
        private EffectService _effectService;
        private EntityManager _em;
        private World _world;

        [SetUp]
        public void SetUp()
        {
            _arch = new TestArchitecture();
            _arch.Initialize();
            _arch.GetCarrierManager().RegisterType("TestUnit");
            _effectService = _arch.GetService<EffectService>();
            var ws = _arch.GetService<WorldService>();
            _em = ws.EntityManager;
            _world = ws.ExWorld;
        }

        [TearDown]
        public void TearDown()
        {
            _arch?.Dispose();
            _arch = null;
        }

        // ── 基础堆叠 ────────────────────────────────

        /// <summary>同 Code GE 施加两次 → 堆叠层数=2，而不是创建两个实体</summary>
        [Test]
        public void ApplySameGE_Twice_StacksCount_To_Two()
        {
            var target = _arch.CreateGASCarrier("TestUnit");
            var source = _arch.CreateGASCarrier("TestUnit");

            // 施加两次 configId=3（可堆叠 GE）
            _effectService.ApplyEffect(configId: 3, target: target, source: source);
            _effectService.ApplyEffect(configId: 3, target: target, source: source);
            TickWorld(frames: 15);

            // 应该只有 1 个 GE 实体，堆叠层数=2
            var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<CStacking>(),
                ComponentType.ReadOnly<CEffectInUsage>());
            var entities = query.ToEntityArray(Allocator.Temp);
            Assert.That(entities.Length, Is.EqualTo(1),
                "同 Code GE 应堆叠到同一实体上，不应创建多个");

            var stacking = _em.GetComponentData<CStacking>(entities[0]);
            Assert.That(stacking.StackCount, Is.EqualTo(2),
                "施加两次后堆叠层数应为 2");

            entities.Dispose();
            query.Dispose();
        }

        /// <summary>同 Code GE 施加三次（达到 LimitCount）→ StackCount=3</summary>
        [Test]
        public void ApplySameGE_ThreeTimes_Reaches_Limit()
        {
            var target = _arch.CreateGASCarrier("TestUnit");
            var source = _arch.CreateGASCarrier("TestUnit");

            _effectService.ApplyEffect(configId: 3, target: target, source: source);
            _effectService.ApplyEffect(configId: 3, target: target, source: source);
            _effectService.ApplyEffect(configId: 3, target: target, source: source);
            TickWorld(frames: 15);

            var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<CStacking>(),
                ComponentType.ReadOnly<CEffectInUsage>());
            var entities = query.ToEntityArray(Allocator.Temp);
            Assert.That(entities.Length, Is.EqualTo(1));
            Assert.That(_em.GetComponentData<CStacking>(entities[0]).StackCount, Is.EqualTo(3));
            entities.Dispose();
            query.Dispose();
        }

        /// <summary>不同 StackingCode 的 GE 不应堆叠在一起</summary>
        [Test]
        public void DifferentStackingCodes_DoNotStack()
        {
            var target = _arch.CreateGASCarrier("TestUnit");
            var source = _arch.CreateGASCarrier("TestUnit");

            // configId=2 无 Stacking，configId=3 有 StackingCode=100
            // 施加 configId=2 与 configId=3，应各为一个实体（不合并堆叠）
            _effectService.ApplyEffect(configId: 2, target: target, source: source);
            _effectService.ApplyEffect(configId: 3, target: target, source: source);
            TickWorld(frames: 15);

            var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<CEffectInUsage>());
            var count = query.CalculateEntityCount();
            Assert.That(count, Is.EqualTo(2),
                "不同 Code 的 GE 不应共享堆叠");
        }

        // ── 堆叠溢出 ────────────────────────────────

        /// <summary>超出 LimitCount 的施加应被拒绝（denyOverflow=true）</summary>
        [Test]
        public void ExceedLimit_Denies_ExtraApplication()
        {
            var target = _arch.CreateGASCarrier("TestUnit");
            var source = _arch.CreateGASCarrier("TestUnit");

            _effectService.ApplyEffect(configId: 3, target: target, source: source);
            _effectService.ApplyEffect(configId: 3, target: target, source: source);
            _effectService.ApplyEffect(configId: 3, target: target, source: source);
            // 第 4 次超过 LimitCount=3
            _effectService.ApplyEffect(configId: 3, target: target, source: source);
            TickWorld(frames: 15);

            var query = _em.CreateEntityQuery(ComponentType.ReadOnly<CStacking>());
            var entities = query.ToEntityArray(Allocator.Temp);
            Assume.That(entities.Length, Is.EqualTo(1));
            var stacking = _em.GetComponentData<CStacking>(entities[0]);
            Assert.That(stacking.StackCount, Is.EqualTo(3),
                "超出 LimitCount 的施加不应增加堆叠层数");
            entities.Dispose();
            query.Dispose();
        }

        // ── 辅助 ────────────────────────────────────

        private void TickWorld(int frames)
        {
            for (var i = 0; i < frames; i++)
                _world.Update();
        }
    }
}
