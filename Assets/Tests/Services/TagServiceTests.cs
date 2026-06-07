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
    public class TagServiceTests
    {
        private TestArchitecture _arch;
        private TagService _tagService;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _arch = new TestArchitecture();
            _arch.Initialize();
            _arch.GetCarrierManager().RegisterType("TestUnit");
            _tagService = _arch.GetService<TagService>();
            _em = _arch.GetService<WorldService>().EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            _arch?.Dispose();
            _arch = null;
        }

        // ── 无标签查询 ──────────────────────────────

        [Test]
        public void HasTag_WithoutTags_ReturnsFalse()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            Assert.That(_tagService.HasTag(carrier,  1), Is.False);
        }

        [Test]
        public void HasTag_WithInvalidCarrier_ReturnsFalse()
        {
            Assert.That(_tagService.HasTag(new CarrierId(),  1), Is.False);
        }

        // ── 固定标签 buffer 操作 ────────────────────

        [Test]
        public void AddFixedTag_AppearsIn_Buffer()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            var buffer = _em.GetBuffer<BFixedTag>(entity);

            buffer.Add(new BFixedTag { tag = 42 });

            var found = false;
            for (var i = 0; i < buffer.Length; i++)
                if (buffer[i].tag == 42) { found = true; break; }
            Assert.That(found, Is.True);
        }

        [Test]
        public void RemoveFixedTag_DisappearsFrom_Buffer()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            var buffer = _em.GetBuffer<BFixedTag>(entity);

            buffer.Add(new BFixedTag { tag = 42 });
            for (var i = buffer.Length - 1; i >= 0; i--)
                if (buffer[i].tag == 42) buffer.RemoveAt(i);

            var found = false;
            for (var i = 0; i < buffer.Length; i++)
                if (buffer[i].tag == 42) { found = true; break; }
            Assert.That(found, Is.False);
        }

        // ── 临时标签 buffer 操作 ────────────────────

        [Test]
        public void AddTemporaryTag_AppearsIn_Buffer()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            var buffer = _em.GetBuffer<BTemporaryTag>(entity);

            buffer.Add(new BTemporaryTag { tag = 99, source = Entity.Null });

            var found = false;
            for (var i = 0; i < buffer.Length; i++)
                if (buffer[i].tag == 99) { found = true; break; }
            Assert.That(found, Is.True);
        }
    }
}
