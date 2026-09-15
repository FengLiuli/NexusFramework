using NexusFramework.DataCarrier;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using NexusFramework.GAS.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Entities;

namespace NexusFramework.GAS.Tests.Services
{
    /// <summary>
    ///     ITagService 的 ECS 实现。测试架构使用 MockConfigLoader，
    ///     其中定义了 <c>Faction</c> / <c>Faction.Player</c> / <c>Faction.Enemy</c> 等标签。
    /// </summary>
    [TestFixture]
    public class EcsTagServiceTests
    {
        private TestArchitecture _arch;
        private ITagService _tagService;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _arch = new TestArchitecture();
            _arch.Initialize();
            _arch.GetCarrierManager().RegisterType("TestUnit");
            _tagService = _arch.GetTagService();
            _em = _arch.GetService<WorldService>().EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            _arch?.Dispose();
            _arch = null;
        }

        // ── ID 空间 ──────────────────────────────────

        [Test]
        public void TagService_IsEcsImplementation()
        {
            Assert.That(_tagService, Is.InstanceOf<EcsTagService>());
        }

        [Test]
        public void InternTag_KnownKey_ReturnsStableId()
        {
            var id1 = _tagService.InternTag("Faction.Player");
            var id2 = _tagService.InternTag("Faction.Player");

            Assert.That(id1, Is.GreaterThan(0));
            Assert.That(id1, Is.EqualTo(id2));
        }

        [Test]
        public void InternTag_UnknownKey_ReturnsZero()
        {
            // 表外键必须显式报错而非静默返回 0
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"EcsTagService"));
            Assert.That(_tagService.InternTag("NoSuch.Tag"), Is.EqualTo(0));
        }

        [Test]
        public void InternTag_InvalidKey_ReturnsZero()
        {
            Assert.That(_tagService.InternTag(null), Is.EqualTo(0));
            Assert.That(_tagService.InternTag(""), Is.EqualTo(0));
            Assert.That(_tagService.InternTag("a..b"), Is.EqualTo(0));
        }

        [Test]
        public void MatchesOrIsAncestor_ChildToParent_True()
        {
            var parent = _tagService.InternTag("Faction");
            var child = _tagService.InternTag("Faction.Player");

            Assert.That(parent, Is.GreaterThan(0));
            Assert.That(child, Is.GreaterThan(0));
            Assert.That(_tagService.MatchesOrIsAncestor(child, parent), Is.True);
            Assert.That(_tagService.MatchesOrIsAncestor(parent, child), Is.False);
        }

        [Test]
        public void MatchesOrIsAncestor_Siblings_False()
        {
            var player = _tagService.InternTag("Faction.Player");
            var enemy = _tagService.InternTag("Faction.Enemy");

            Assert.That(_tagService.MatchesOrIsAncestor(player, enemy), Is.False);
        }

        [Test]
        public void GetTagKey_RoundTrips()
        {
            var id = _tagService.InternTag("Faction.Player");
            Assert.That(_tagService.GetTagKey(id), Is.EqualTo("Faction.Player"));
        }

        // ── 写与查 ───────────────────────────────────

        [Test]
        public void AddTag_ThenHasTag_ReturnsTrue()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");

            Assert.That(_tagService.AddTag(carrier, "Faction.Player"), Is.True);
            Assert.That(_tagService.HasExactTag(carrier, "Faction.Player"), Is.True);
            // 层级：父标签应命中
            Assert.That(_tagService.HasTag(carrier, "Faction"), Is.True);
            // 兄弟标签不命中
            Assert.That(_tagService.HasTag(carrier, "Faction.Enemy"), Is.False);
        }

        [Test]
        public void AddTag_WritesToEcsBuffer()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            _tagService.AddTag(carrier, "Faction.Player");

            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            var expected = _tagService.InternTag("Faction.Player");
            var buffer = _em.GetBuffer<BFixedTag>(entity);

            var found = false;
            for (var i = 0; i < buffer.Length; i++)
                if (buffer[i].tag == expected) { found = true; break; }

            Assert.That(found, Is.True, "Tag 应写入 ECS 的 BFixedTag buffer");
        }

        [Test]
        public void AddTag_Duplicate_ReturnsFalse()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");

            Assert.That(_tagService.AddTag(carrier, "Faction.Player"), Is.True);
            Assert.That(_tagService.AddTag(carrier, "Faction.Player"), Is.False);
        }

        [Test]
        public void AddTag_UnknownKey_ReturnsFalse()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"EcsTagService"));
            Assert.That(_tagService.AddTag(carrier, "NoSuch.Tag"), Is.False);
        }

        [Test]
        public void RemoveTag_ThenHasTag_ReturnsFalse()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            _tagService.AddTag(carrier, "Faction.Player");

            Assert.That(_tagService.RemoveTag(carrier, "Faction.Player"), Is.True);
            Assert.That(_tagService.HasExactTag(carrier, "Faction.Player"), Is.False);
        }

        [Test]
        public void HasTag_WithoutTags_ReturnsFalse()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            Assert.That(_tagService.HasTag(carrier, "Faction.Player"), Is.False);
        }

        [Test]
        public void HasTag_InvalidCarrier_ReturnsFalse()
        {
            Assert.That(_tagService.HasTag(new CarrierId(), "Faction.Player"), Is.False);
        }

        [Test]
        public void HasAllTags_RequiresEveryTag()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            _tagService.AddTag(carrier, "Faction.Player");

            Assert.That(_tagService.HasAllTags(carrier, new[] { "Faction.Player" }), Is.True);
            Assert.That(_tagService.HasAllTags(carrier, new[] { "Faction.Player", "Faction.Enemy" }),
                Is.False);
        }

        [Test]
        public void HasNoneTags_TrueWhenAbsent()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            _tagService.AddTag(carrier, "Faction.Player");

            Assert.That(_tagService.HasNoneTags(carrier, new[] { "Faction.Enemy" }), Is.True);
            Assert.That(_tagService.HasNoneTags(carrier, new[] { "Faction.Player" }), Is.False);
        }

        [Test]
        public void GetTagCount_ReflectsAdds()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");

            _tagService.AddTag(carrier, "Faction.Player");
            _tagService.AddTag(carrier, "Faction.Enemy");

            Assert.That(_tagService.GetTagCount(carrier), Is.EqualTo(2));
        }

        // ── 临时标签（GE 授予）参与查询 ──────────────

        [Test]
        public void HasTag_MatchesTemporaryTags()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            var tagId = _tagService.InternTag("Faction.Enemy");

            _em.GetBuffer<BTemporaryTag>(entity).Add(new BTemporaryTag { tag = tagId, source = Entity.Null });

            Assert.That(_tagService.HasExactTag(carrier, "Faction.Enemy"), Is.True,
                "GE 授予的临时标签应参与查询");
        }

        // ── 绕过服务的写入必须可见 ──────────────────
        // ECS 是 Tag 状态的真相源，因此直接写 buffer（如 ASC 初始化、
        // GE 系统授予）的标签必须被服务读到。曾因服务持有独立托管账本而失同步。

        [Test]
        public void GetTags_SeesDirectBufferWrites()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            var tagId = _tagService.InternTag("Faction.Player");

            // 模拟 ASC 初始化路径：绕过服务直接写 ECS
            _em.GetBuffer<BFixedTag>(entity).Add(new BFixedTag { tag = tagId });

            Assert.That(_tagService.GetTagCount(carrier), Is.EqualTo(1));
            Assert.That(_tagService.GetTags(carrier), Does.Contain("Faction.Player"));
        }

        [Test]
        public void GetTagCount_CountsFixedAndTemporary()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);

            _em.GetBuffer<BFixedTag>(entity).Add(new BFixedTag { tag = _tagService.InternTag("Faction.Player") });
            _em.GetBuffer<BTemporaryTag>(entity).Add(new BTemporaryTag
            {
                tag = _tagService.InternTag("Faction.Enemy"), source = Entity.Null
            });

            Assert.That(_tagService.GetTagCount(carrier), Is.EqualTo(2));
        }

        [Test]
        public void FindCarriersWithTag_SeesDirectBufferWrites()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            var tagId = _tagService.InternTag("State.Stunned");

            _em.GetBuffer<BFixedTag>(entity).Add(new BFixedTag { tag = tagId });

            Assert.That(_tagService.FindCarriersWithTag("State.Stunned"), Does.Contain(carrier));
        }

        [Test]
        public void FindCarriersWithTags_SeesDirectBufferWrites()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            var tagId = _tagService.InternTag("State.Burning");

            _em.GetBuffer<BFixedTag>(entity).Add(new BFixedTag { tag = tagId });

            var query = new TagQuery(all: new[] { "State.Burning" });
            Assert.That(_tagService.FindCarriersWithTags(query), Does.Contain(carrier));
        }
    }
}
