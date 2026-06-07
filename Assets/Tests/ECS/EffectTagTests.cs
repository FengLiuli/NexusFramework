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
    public class EffectTagTests
    {
        private TestArchitecture _arch;
        private EffectService _effectService;
        private EntityManager _em;
        private World _world;
        private SingletonGameplayTagMap _tagMap;

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
            _tagMap = SetupTagMap();
        }

        [TearDown]
        public void TearDown()
        {
            _arch?.Dispose();
            _arch = null;
        }

        // ── 需求标签检查 ────────────────────────────

        /// <summary>GE 要求标签 10，目标已有标签 10 → 施加成功，GE 创建</summary>
        [Test]
        public void RequiredTag_Present_GrantPermits()
        {
            var target = _arch.CreateGASCarrier("TestUnit");
            var source = _arch.CreateGASCarrier("TestUnit");
            var targetEntity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(target);
            AddFixedTag(targetEntity, tagCode: 10);

            _effectService.ApplyEffect(configId: 10, target: target, source: source);
            TickWorld(frames: 10);

            var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<CEffectInUsage>(),
                ComponentType.ReadOnly<CApplicationRequiredTags>());
            Assert.That(query.CalculateEntityCount(), Is.GreaterThan(0),
                "目标有需求标签时 GE 应成功创建");
        }

        /// <summary>GE 要求标签 10，目标无该标签 → GE 被销毁</summary>
        [Test]
        public void RequiredTag_Missing_EffectDestroyed()
        {
            var target = _arch.CreateGASCarrier("TestUnit");
            var source = _arch.CreateGASCarrier("TestUnit");
            // 不添加标签 10

            _effectService.ApplyEffect(configId: 10, target: target, source: source);
            TickWorld(frames: 10);

            var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<CApplicationRequiredTags>());
            Assert.That(query.CalculateEntityCount(), Is.EqualTo(0),
                "目标缺需求标签时 GE 应被管线拒绝并销毁");
        }

        // ── 免疫标签检查 ────────────────────────────

        /// <summary>GE 检查免疫标签 20，目标有标签 20 → GE 被销毁</summary>
        [Test]
        public void ImmunityTag_Present_EffectDestroyed()
        {
            var target = _arch.CreateGASCarrier("TestUnit");
            var source = _arch.CreateGASCarrier("TestUnit");
            var targetEntity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(target);
            AddFixedTag(targetEntity, tagCode: 20);

            _effectService.ApplyEffect(configId: 11, target: target, source: source);
            TickWorld(frames: 10);

            var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<CEffectImmunityTags>());
            Assert.That(query.CalculateEntityCount(), Is.EqualTo(0),
                "目标有免疫标签时 GE 应被销毁");
        }

        /// <summary>GE 检查免疫标签 20，目标无该标签 → GE 正常存活</summary>
        [Test]
        public void ImmunityTag_Absent_GEPersists()
        {
            var target = _arch.CreateGASCarrier("TestUnit");
            var source = _arch.CreateGASCarrier("TestUnit");

            _effectService.ApplyEffect(configId: 11, target: target, source: source);
            TickWorld(frames: 10);

            var query = _em.CreateEntityQuery(
                ComponentType.ReadOnly<CEffectInUsage>());
            Assert.That(query.CalculateEntityCount(), Is.GreaterThan(0),
                "目标无免疫标签时 GE 应正常保留");
        }

        // ── 授予标签 ────────────────────────────────

        /// <summary>GE 激活后授予标签 10 → 目标 BTemporaryTag 应含该标签</summary>
        [Test]
        public void GrantedTag_Added_AfterActivation()
        {
            var target = _arch.CreateGASCarrier("TestUnit");
            var source = _arch.CreateGASCarrier("TestUnit");
            var targetEntity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(target);

            _effectService.ApplyEffect(configId: 12, target: target, source: source);
            TickWorld(frames: 15);

            var tempTags = _em.GetBuffer<BTemporaryTag>(targetEntity);
            var hasTag = false;
            for (var i = 0; i < tempTags.Length; i++)
            {
                if (tempTags[i].tag == 10)
                { hasTag = true; break; }
            }
            Assert.That(hasTag, Is.True,
                "GE 激活后应授予临时标签 10 到目标");
        }

        // ── 辅助 ────────────────────────────────────

        private void TickWorld(int frames)
        {
            for (var i = 0; i < frames; i++)
                _world.Update();
        }

        private void AddFixedTag(Entity gasEntity, int tagCode)
        {
            var buffer = _em.GetBuffer<BFixedTag>(gasEntity);
            // 避免重复
            for (var i = 0; i < buffer.Length; i++)
                if (buffer[i].tag == tagCode) return;
            buffer.Add(new BFixedTag { tag = tagCode });
        }

        /// <summary>创建 SingletonGameplayTagMap 并填充测试标签层级</summary>
        private SingletonGameplayTagMap SetupTagMap()
        {
            // 销毁已有的 SingletonGameplayTagMap 实体（如有）
            var existingQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<SingletonGameplayTagMap>());
            var existing = existingQuery.ToEntityArray(Allocator.Temp);
            foreach (var e in existing) _em.DestroyEntity(e);
            existing.Dispose();
            existingQuery.Dispose();

            var entity = _em.CreateEntity();
            _em.AddComponent<SingletonGameplayTagMap>(entity);
            var map = new NativeHashMap<int, ComGameplayTag>(8, Allocator.Persistent);

            // Tag 0 = Root
            map.Add(0, new ComGameplayTag
            {
                Code = 0,
                Parents = new NativeArray<int>(0, Allocator.Persistent),
                Children = new NativeArray<int>(new[] { 10, 20 }, Allocator.Persistent)
            });

            // Tag 10 = Buffed (child of 0)
            map.Add(10, new ComGameplayTag
            {
                Code = 10,
                Parents = new NativeArray<int>(new[] { 0 }, Allocator.Persistent),
                Children = new NativeArray<int>(0, Allocator.Persistent)
            });

            // Tag 20 = ImmuneToPoison (child of 0)
            map.Add(20, new ComGameplayTag
            {
                Code = 20,
                Parents = new NativeArray<int>(new[] { 0 }, Allocator.Persistent),
                Children = new NativeArray<int>(0, Allocator.Persistent)
            });

            var tagMap = new SingletonGameplayTagMap { Map = map };
            _em.SetComponentData(entity, tagMap);
            return tagMap;
        }
    }
}
