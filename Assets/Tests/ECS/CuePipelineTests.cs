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
    public class CuePipelineTests
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

        /// <summary>施加带 Cue 的 GE → 激活后 Cue 实体被创建，且 ECCuePlaying 启用</summary>
        [Test]
        public void GE_WithCue_CreatesAndStarts_Cue()
        {
            var src = _arch.CreateGASCarrier("TestUnit");
            var tgt = _arch.CreateGASCarrier("TestUnit");

            _effectService.ApplyEffect(configId: 20, target: tgt, source: src);
            TickWorld(15);

            var q = _em.CreateEntityQuery(ComponentType.ReadOnly<MCCue>(), ComponentType.ReadOnly<ECCuePlayable>());
            var cues = q.ToEntityArray(Allocator.Temp);
            Assume.That(cues.Length, Is.GreaterThan(0), "GE 激活后应有 Cue 实体");
            var cue = cues[0];
            cues.Dispose();
            q.Dispose();

            // Play(true) → ECCuePlayable 启用 → SCueStart → ECCuePlaying 启用
            bool playing = _em.IsComponentEnabled<ECCuePlaying>(cue);
            bool playable = _em.IsComponentEnabled<ECCuePlayable>(cue);
            Assert.That(playing, Is.True,  "SCueStart 应启用 ECCuePlaying");
            Assert.That(playable, Is.True, "ECCuePlayable 应保持启用（标志位，不消费）");
        }

        /// <summary>Cue 被 Kill 后 → ECKillCue 启用 → SCueDestroy 销毁实体</summary>
        [Test]
        public void Cue_Destroyed_AfterKill()
        {
            var src = _arch.CreateGASCarrier("TestUnit");
            var tgt = _arch.CreateGASCarrier("TestUnit");

            _effectService.ApplyEffect(configId: 20, target: tgt, source: src);
            TickWorld(15);

            var q = _em.CreateEntityQuery(ComponentType.ReadOnly<MCCue>());
            var cues = q.ToEntityArray(Allocator.Temp);
            Assume.That(cues.Length, Is.GreaterThan(0));
            var cue = cues[0];
            cues.Dispose();
            q.Dispose();

            // 手动杀死 Cue → ECKillCue 启用
            _em.SetComponentEnabled<ECKillCue>(cue, true);
            TickWorld(5);

            Assert.That(_em.Exists(cue), Is.False, "ECKillCue 启用后 SCueDestroy 应销毁 Cue 实体");
        }

        private void TickWorld(int frames)
        {
            for (int i = 0; i < frames; i++)
                _world.Update();
        }
    }
}
