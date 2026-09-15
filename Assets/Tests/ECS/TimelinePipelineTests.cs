using System.Collections.Generic;
using NexusFramework.DataCarrier;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using NexusFramework.GAS.Services;
using NUnit.Framework;
using Unity.Entities;

namespace NexusFramework.GAS.Tests.ECS
{
    /// <summary>
    ///     时间轴技能的帧驱动行为。用手工构造的 <see cref="XParamTimeline"/> 驱动，
    ///     不依赖 Luban 表——这样测试只锁定"帧调度"这一个契约。
    /// </summary>
    [TestFixture]
    public class TimelinePipelineTests
    {
        private TestArchitecture _arch;
        private AbilityService _abilityService;
        private ConfigModel _configModel;
        private EntityManager _em;
        private World _world;

        private const int TimelineId = 9001;

        [SetUp]
        public void SetUp()
        {
            _arch = new TestArchitecture();
            _arch.Initialize();
            _arch.GetCarrierManager().RegisterType("TestUnit");
            _abilityService = _arch.GetService<AbilityService>();
            _configModel = _arch.GetModel<ConfigModel>();
            var ws = _arch.GetService<WorldService>();
            _em = ws.EntityManager;
            _world = ws.ExWorld;

            // ScanAndRegisterAll 只扫描 Architecture 所在程序集（GAS），
            // 测试程序集里的探针需手工登记。
            AbilityLogicFactory.Register(nameof(ProbeAbilityTask), typeof(ProbeAbilityTask));
            AbilityLogicFactory.RegisterAbilityTask(nameof(ProbeAbilityTask), typeof(ProbeAbilityTask));
            AbilityLogicFactory.RegisterAbilityTaskParam(nameof(ProbeAbilityTask), typeof(XParamNone));

            ProbeAbilityTask.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ProbeAbilityTask.Clear();
            _arch?.Dispose();
            _arch = null;
        }

        /// <summary>构造一条只含探针片段的时间轴并注册。</summary>
        private void RegisterProbeTimeline(int lifeTime, bool manualEnd, params (int start, int end)[] clips)
        {
            var track = new Track { Name = "Probe" };
            foreach (var (start, end) in clips)
            {
                track.TaskClips.Add(new TaskClipData
                {
                    Name = $"probe-{start}-{end}",
                    StartTime = start,
                    EndTime = end,
                    TaskType = nameof(ProbeAbilityTask),
                    Parameter = new XParamNone()
                });
            }

            _configModel.RegisterTimelineAbility(TimelineId, new XParamTimeline(
                TimelineId, "probe", lifeTime, manualEnd, new List<Track> { track }));
        }

        /// <summary>构造一条挂 ALTimeline 逻辑的能力并授予给载体。</summary>
        private CarrierId GrantTimelineAbility()
        {
            _configModel.RegisterAbility(900, new AbilityComponentConfig[]
            {
                new TestAbilityLogicConfig
                {
                    AbilityLogicType = nameof(ALTimeline),
                    Param = new XParamALTimelineID(TimelineId)
                }
            });

            var carrier = _arch.CreateGASCarrier("TestUnit");
            _abilityService.GrantAbility(carrier, 900, _arch);
            return carrier;
        }

        private void TickWorld(int frames)
        {
            for (var i = 0; i < frames; i++)
                _world.Update();
        }

        [Test]
        public void Timeline_Activates_And_Plays()
        {
            RegisterProbeTimeline(60, false, (0, 0));
            var carrier = GrantTimelineAbility();

            _abilityService.TryActivate(carrier, 900);
            TickWorld(3);

            Assert.That(ProbeAbilityTask.Log, Does.Contain("begin:0"),
                "时间轴激活后第 0 帧的片段应触发 Begin");
        }

        [Test]
        public void TaskClip_Begins_At_StartTime()
        {
            RegisterProbeTimeline(60, false, (5, 8));
            var carrier = GrantTimelineAbility();

            _abilityService.TryActivate(carrier, 900);

            // 激活本身消耗一次世界更新（SGlobalTimer 先自增，SAbilityTick 随后调用），
            // 故推进 5 次后时间轴位于第 4 帧。
            TickWorld(5);
            Assert.That(ProbeAbilityTask.Log, Does.Not.Contain("begin:5"),
                "第 5 帧之前不应触发 Begin");

            TickWorld(1);
            Assert.That(ProbeAbilityTask.Log, Does.Contain("begin:5"),
                "到达 StartTime 时应触发 Begin");
        }

        [Test]
        public void TaskClip_Finishes_At_EndTime()
        {
            RegisterProbeTimeline(60, false, (2, 6));
            var carrier = GrantTimelineAbility();

            _abilityService.TryActivate(carrier, 900);
            TickWorld(7); // +1 抵消激活消耗的那次世界更新

            Assert.That(ProbeAbilityTask.Log, Does.Contain("finish:6"),
                "到达 EndTime 时应触发 Finish");
        }

        [Test]
        public void TaskClip_Ticks_Within_Range_Only()
        {
            RegisterProbeTimeline(60, false, (2, 5));
            var carrier = GrantTimelineAbility();

            _abilityService.TryActivate(carrier, 900);
            TickWorld(9);

            var ticks = ProbeAbilityTask.Log.FindAll(x => x.StartsWith("tick:"));
            // 区间 [2,5] 共 4 帧
            Assert.That(ticks, Is.EqualTo(new List<string> { "tick:2", "tick:3", "tick:4", "tick:5" }),
                "Tick 应恰好覆盖 [StartTime, EndTime] 且不越界");
        }

        [Test]
        public void Timeline_AutoEnds_When_NotManual()
        {
            RegisterProbeTimeline(5, false, (0, 0));
            var carrier = GrantTimelineAbility();

            _abilityService.TryActivate(carrier, 900);
            TickWorld(10);

            // 自动结束会走 TryEndSelf → CAbilityInTryEnd → STryEndAbility 链路
            Assert.That(_abilityService.IsActive(carrier, 900), Is.False,
                "LifeTime 到达且 ManualEndAbility=false 时应自动结束");
        }

        [Test]
        public void Timeline_DoesNotAutoEnd_When_Manual()
        {
            RegisterProbeTimeline(5, true, (0, 0));
            var carrier = GrantTimelineAbility();

            _abilityService.TryActivate(carrier, 900);
            TickWorld(10);

            Assert.That(_abilityService.IsActive(carrier, 900), Is.True,
                "ManualEndAbility=true 时不应自动结束");
        }

        [Test]
        public void Timeline_MultipleClips_AllScheduled()
        {
            RegisterProbeTimeline(60, false, (0, 0), (3, 5));
            var carrier = GrantTimelineAbility();

            _abilityService.TryActivate(carrier, 900);
            TickWorld(8);

            Assert.That(ProbeAbilityTask.Log, Does.Contain("begin:0"));
            Assert.That(ProbeAbilityTask.Log, Does.Contain("begin:3"));
            Assert.That(ProbeAbilityTask.Log, Does.Contain("finish:5"));
        }
    }
}
