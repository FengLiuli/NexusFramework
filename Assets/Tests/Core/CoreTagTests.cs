using System;
using System.Collections.Generic;
using System.Linq;
using NexusFramework;
using NexusFramework.DataCarrier;
using NUnit.Framework;

namespace NexusFramework.Core.Tests
{
    /// <summary>
    ///     最小 Core Architecture，不依赖 GAS，供 Tag 独立测试使用。
    /// </summary>
    internal class CoreTestArchitecture : Architecture
    {
        public override string ArchitectureType => "CoreTest";

        protected override void OnInit()
        {
        }
    }

    [TestFixture]
    public class TagKeyTests
    {
        [Test]
        public void Normalize_SimpleKey_ReturnsTrimmed()
        {
            Assert.That(TagKey.Normalize("物品.食物.苹果"), Is.EqualTo("物品.食物.苹果"));
        }

        [Test]
        public void Normalize_TrimsSegments()
        {
            Assert.That(TagKey.Normalize(" 物品 . 食物 . 苹果 "), Is.EqualTo("物品.食物.苹果"));
        }

        [Test]
        public void Normalize_EmptyOrWhitespace_Throws()
        {
            Assert.Throws<ArgumentException>(() => TagKey.Normalize(null));
            Assert.Throws<ArgumentException>(() => TagKey.Normalize(""));
            Assert.Throws<ArgumentException>(() => TagKey.Normalize("   "));
        }

        [Test]
        public void Normalize_EmptySegment_Throws()
        {
            Assert.Throws<ArgumentException>(() => TagKey.Normalize("物品..食物"));
            Assert.Throws<ArgumentException>(() => TagKey.Normalize(".苹果"));
            Assert.Throws<ArgumentException>(() => TagKey.Normalize("苹果."));
        }

        [Test]
        public void Normalize_CaseSensitive()
        {
            Assert.That(TagKey.Normalize("Item.Food"), Is.Not.EqualTo("item.food"));
        }

        [Test]
        public void Normalize_UnicodeAllowed()
        {
            Assert.DoesNotThrow(() => TagKey.Normalize("状态.中毒"));
            Assert.DoesNotThrow(() => TagKey.Normalize("裝備.武器.劍"));
        }

        [Test]
        public void GetSegments_ReturnsCorrectSegments()
        {
            var segments = TagKey.GetSegments("物品.食物.苹果");
            Assert.That(segments.Count, Is.EqualTo(3));
            Assert.That(segments[0], Is.EqualTo("物品"));
            Assert.That(segments[1], Is.EqualTo("食物"));
            Assert.That(segments[2], Is.EqualTo("苹果"));
        }
    }

    [TestFixture]
    public class TagRuntimeIndexTests
    {
        private TagRuntimeIndex index;

        [SetUp]
        public void SetUp()
        {
            index = new TagRuntimeIndex();
        }

        [Test]
        public void Intern_SameKey_ReturnsSameSlot()
        {
            var slot1 = index.Intern("物品.食物.苹果");
            var slot2 = index.Intern("物品.食物.苹果");
            Assert.That(slot1, Is.EqualTo(slot2));
        }

        [Test]
        public void Intern_DifferentKey_ReturnsDifferentSlot()
        {
            var slot1 = index.Intern("物品.食物.苹果");
            var slot2 = index.Intern("物品.食物.香蕉");
            Assert.That(slot1, Is.Not.EqualTo(slot2));
        }

        [Test]
        public void Intern_ParentKey_AutoInterned()
        {
            var childSlot = index.Intern("物品.食物.苹果");

            Assert.That(index.TryGetSlot("物品", out var rootSlot), Is.True);
            Assert.That(index.TryGetSlot("物品.食物", out var foodSlot), Is.True);

            Assert.That(index.MatchesOrIsAncestor(childSlot, rootSlot), Is.True);
            Assert.That(index.MatchesOrIsAncestor(childSlot, foodSlot), Is.True);
        }

        [Test]
        public void TryGetSlot_ExistingKey_ReturnsTrue()
        {
            index.Intern("物品.食物");
            Assert.That(index.TryGetSlot("物品.食物", out var slot), Is.True);
            Assert.That(slot.IsValid, Is.True);
        }

        [Test]
        public void TryGetSlot_UnknownKey_ReturnsFalse()
        {
            Assert.That(index.TryGetSlot("未知.Tag", out _), Is.False);
        }

        [Test]
        public void GetKey_ReturnsCorrectString()
        {
            var slot = index.Intern("狀態.中毒");
            Assert.That(index.GetKey(slot), Is.EqualTo("狀態.中毒"));
        }

        [Test]
        public void GetKey_WrongSlot_Throws()
        {
            var otherIndex = new TagRuntimeIndex();
            var slot = otherIndex.Intern("Other.Tag");

            Assert.Throws<ArgumentException>(() => index.GetKey(slot));
        }

        [Test]
        public void MatchesOrIsAncestor_ExactMatch_ReturnsTrue()
        {
            var slot = index.Intern("物品.食物");
            Assert.That(index.MatchesOrIsAncestor(slot, slot), Is.True);
        }

        [Test]
        public void MatchesOrIsAncestor_ParentMatch_ReturnsTrue()
        {
            var child = index.Intern("裝備.武器.劍");
            Assert.That(index.TryGetSlot("裝備", out var rootSlot), Is.True);
            Assert.That(index.MatchesOrIsAncestor(child, rootSlot), Is.True);
        }

        [Test]
        public void MatchesOrIsAncestor_DifferentBranch_ReturnsFalse()
        {
            var child = index.Intern("物品.食物.蘋果");
            // 先 intern "裝備" 确保它在索引中存在，再验证不同分支不匹配
            var equipChild = index.Intern("裝備.武器.劍");
            Assert.That(index.TryGetSlot("裝備", out var equipSlot), Is.True);
            Assert.That(index.MatchesOrIsAncestor(child, equipSlot), Is.False);
        }
    }

    [TestFixture]
    public class CarrierTagServiceTests
    {
        private CoreTestArchitecture arch;
        private CarrierManager manager;
        private ITagService tagService;

        [SetUp]
        public void SetUp()
        {
            arch = new CoreTestArchitecture();
            arch.Initialize();
            manager = (CarrierManager)arch.GetCarrierManager();
            tagService = arch.GetTagService();
        }

        [TearDown]
        public void TearDown()
        {
            arch.Dispose();
        }

        [Test]
        public void GetTagService_ReturnsManagedImplementation()
        {
            Assert.That(tagService, Is.Not.Null);
            Assert.That(tagService, Is.InstanceOf<CarrierTagService>());
        }

        [Test]
        public void SerializeCarrier_ContainsTagKeysOnly()
        {
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物.蘋果");

            var json = manager.SerializeCarrier(c);
            Assert.That(json, Does.Contain("物品.食物.蘋果"));
            Assert.That(json, Does.Not.Contain("slot"));
            Assert.That(json, Does.Not.Contain("Slot"));
            Assert.That(json, Does.Not.Contain("Value"));
        }

        [Test]
        public void DeserializeCarrier_RestoresTags()
        {
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物");
            tagService.AddTag(c, "狀態.中毒");

            var json = manager.SerializeCarrier(c);

            var arch2 = new CoreTestArchitecture();
            arch2.Initialize();
            var manager2 = (CarrierManager)arch2.GetCarrierManager();
            var tagService2 = arch2.GetTagService();
            manager2.RegisterType("Test");
            var restored = manager2.DeserializeCarrier(json);

            Assert.That(tagService2.HasExactTag(restored, "物品.食物"), Is.True);
            Assert.That(tagService2.HasExactTag(restored, "狀態.中毒"), Is.True);
            Assert.That(tagService2.HasExactTag(restored, "不存在.Tag"), Is.False);

            arch2.Dispose();
        }

        [Test]
        public void DeserializeCarrier_AfterDestroy_NoLeak()
        {
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "Tag.ToRemove");
            manager.DestroyCarrier(c);

            var json = manager.SerializeCarrier(c);
            Assert.That(json, Is.Null);
        }

        [Test]
        public void OnCarrierDestroyed_ClearsReverseIndex()
        {
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物");

            Assert.That(tagService.FindCarriersWithTag("物品.食物").Count(), Is.EqualTo(1));

            manager.DestroyCarrier(c);

            Assert.That(tagService.FindCarriersWithTag("物品.食物").Count(), Is.EqualTo(0));
            Assert.That(tagService.GetTagCount(c), Is.EqualTo(0));
        }

        [Test]
        public void OnCarrierDestroyed_ClearsFindCarriersWithTags()
        {
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物");

            manager.DestroyCarrier(c);

            var query = new TagQuery(none: new[] { "状态.中毒" });
            Assert.That(tagService.FindCarriersWithTags(query).Count(), Is.EqualTo(0));
        }

        [Test]
        public void OnCarrierDestroyed_SecondCall_NoThrow()
        {
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");

            Assert.DoesNotThrow(() => tagService.OnCarrierDestroyed(c));
            Assert.DoesNotThrow(() => tagService.OnCarrierDestroyed(c));
        }

        [Test]
        public void RestoreTags_DoesNotFireEvent()
        {
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");

            int eventCount = 0;
            arch.RegisterEvent<CarrierTagAddedEvent>(_ => eventCount++);

            tagService.RestoreTags(c, new[] { "物品.食物", "狀態.中毒" });

            Assert.That(eventCount, Is.EqualTo(0));
            Assert.That(tagService.GetTagCount(c), Is.EqualTo(2));
        }

        [Test]
        public void RestoreTags_UnknownCarrier_ReturnsZero()
        {
            Assert.That(tagService.RestoreTags(CarrierId.Invalid, new[] { "物品.食物" }), Is.EqualTo(0));
        }

        [Test]
        public void IsCarrierKnown_TracksCarrierLifecycle()
        {
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");

            Assert.That(tagService.IsCarrierKnown(c), Is.True);

            manager.DestroyCarrier(c);

            Assert.That(tagService.IsCarrierKnown(c), Is.False);
        }

        [Test]
        public void IsCarrierKnown_InvalidId_ReturnsFalse()
        {
            Assert.That(tagService.IsCarrierKnown(CarrierId.Invalid), Is.False);
        }
    }

    /// <summary>
    ///     ITagService 的 ID 空间契约。期二的 EcsTagService 必须实现同一套语义，
    ///     因此这里的断言即是那份参考语义。
    /// </summary>
    [TestFixture]
    public class CarrierTagServiceIdSpaceTests
    {
        private CoreTestArchitecture arch;
        private CarrierManager manager;
        private ITagService tagService;

        [SetUp]
        public void SetUp()
        {
            arch = new CoreTestArchitecture();
            arch.Initialize();
            manager = (CarrierManager)arch.GetCarrierManager();
            tagService = arch.GetTagService();
            manager.RegisterType("Test");
        }

        [TearDown]
        public void TearDown()
        {
            arch.Dispose();
        }

        [Test]
        public void InternTag_SameKey_ReturnsSameId()
        {
            var id1 = tagService.InternTag("物品.食物.蘋果");
            var id2 = tagService.InternTag("物品.食物.蘋果");

            Assert.That(id1, Is.GreaterThan(0));
            Assert.That(id1, Is.EqualTo(id2));
        }

        [Test]
        public void InternTag_CreatesAncestors()
        {
            var rootId = tagService.InternTag("物品");
            var childId = tagService.InternTag("物品.食物.蘋果");

            Assert.That(childId, Is.Not.EqualTo(rootId));
            // 子 Tag 的祖先链里应有 rootId，因此 root 命中 child
            Assert.That(tagService.MatchesOrIsAncestor(childId, rootId), Is.True);
            // root 不是 child 的祖先
            Assert.That(tagService.MatchesOrIsAncestor(rootId, childId), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("物品..食物")]
        [TestCase(".苹果")]
        [TestCase("苹果.")]
        public void InternTag_InvalidKey_ReturnsZero(string key)
        {
            Assert.That(tagService.InternTag(key), Is.EqualTo(0));
        }

        [Test]
        public void InternTag_NormalizesKey()
        {
            var id1 = tagService.InternTag(" 物品 . 食物 ");
            var id2 = tagService.InternTag("物品.食物");

            Assert.That(id1, Is.GreaterThan(0));
            Assert.That(id1, Is.EqualTo(id2));
            Assert.That(tagService.GetTagKey(id1), Is.EqualTo("物品.食物"));
        }

        [Test]
        public void GetTagKey_RoundTripsInternedKey()
        {
            var id = tagService.InternTag("狀態.中毒");
            Assert.That(tagService.GetTagKey(id), Is.EqualTo("狀態.中毒"));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void GetTagKey_NonPositiveId_ReturnsNull(int id)
        {
            Assert.That(tagService.GetTagKey(id), Is.Null);
        }

        [Test]
        public void GetTagKey_UnknownId_ReturnsNull()
        {
            tagService.InternTag("物品");
            Assert.That(tagService.GetTagKey(999999), Is.Null);
        }

        [Test]
        public void TryGetTagId_InternedKey_ReturnsTrue()
        {
            var id = tagService.InternTag("物品.食物");

            Assert.That(tagService.TryGetTagId("物品.食物", out var found), Is.True);
            Assert.That(found, Is.EqualTo(id));
        }

        [Test]
        public void TryGetTagId_UnknownKey_ReturnsFalse()
        {
            Assert.That(tagService.TryGetTagId("从未出现过.Tag", out var id), Is.False);
            Assert.That(id, Is.EqualTo(0));
        }

        [Test]
        public void TryGetTagId_DoesNotCreateEntry()
        {
            // 与 InternTag 的关键区别：查询不应产生副作用
            tagService.TryGetTagId("物品.食物", out _);
            Assert.That(tagService.TryGetTagId("物品.食物", out _), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("a..b")]
        public void TryGetTagId_InvalidKey_ReturnsFalse(string key)
        {
            Assert.That(tagService.TryGetTagId(key, out var id), Is.False);
            Assert.That(id, Is.EqualTo(0));
        }

        [Test]
        public void MatchesOrIsAncestor_SameId_ReturnsTrue()
        {
            var id = tagService.InternTag("物品.食物");
            Assert.That(tagService.MatchesOrIsAncestor(id, id), Is.True);
        }

        [Test]
        public void MatchesOrIsAncestor_ChildToParent_ReturnsTrue()
        {
            var parent = tagService.InternTag("物品");
            var child = tagService.InternTag("物品.食物.蘋果");

            Assert.That(tagService.MatchesOrIsAncestor(child, parent), Is.True);
        }

        [Test]
        public void MatchesOrIsAncestor_SiblingBranch_ReturnsFalse()
        {
            var food = tagService.InternTag("物品.食物.蘋果");
            var equip = tagService.InternTag("裝備.武器.劍");

            Assert.That(tagService.MatchesOrIsAncestor(food, equip), Is.False);
        }

        [TestCase(0, 1)]
        [TestCase(1, 0)]
        [TestCase(-1, -1)]
        [TestCase(0, 0)]
        public void MatchesOrIsAncestor_NonPositiveId_ReturnsFalse(int actual, int query)
        {
            tagService.InternTag("物品");
            Assert.That(tagService.MatchesOrIsAncestor(actual, query), Is.False);
        }

        [Test]
        public void MatchesOrIsAncestor_UnknownId_ReturnsFalse()
        {
            var id = tagService.InternTag("物品");
            Assert.That(tagService.MatchesOrIsAncestor(999999, id), Is.False);
            Assert.That(tagService.MatchesOrIsAncestor(id, 999999), Is.False);
        }
    }

    /// <summary>
    ///     批量写入 API。事件应当成批在全部写入完成后补发，而非逐条交错。
    /// </summary>
    [TestFixture]
    public class CarrierTagServiceBatchTests
    {
        private CoreTestArchitecture arch;
        private CarrierManager manager;
        private ITagService tagService;

        [SetUp]
        public void SetUp()
        {
            arch = new CoreTestArchitecture();
            arch.Initialize();
            manager = (CarrierManager)arch.GetCarrierManager();
            tagService = arch.GetTagService();
            manager.RegisterType("Test");
        }

        [TearDown]
        public void TearDown()
        {
            arch.Dispose();
        }

        [Test]
        public void AddTags_ReturnsAddedCount()
        {
            var c = manager.CreateCarrier("Test");

            var added = tagService.AddTags(c, new[] { "物品.食物", "狀態.中毒", "未知.Tag" });

            Assert.That(added, Is.EqualTo(3));
            Assert.That(tagService.GetTagCount(c), Is.EqualTo(3));
        }

        [Test]
        public void AddTags_SkipsDuplicates_CountsOnlyNew()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物");

            var added = tagService.AddTags(c, new[] { "物品.食物", "狀態.中毒" });

            Assert.That(added, Is.EqualTo(1));
            Assert.That(tagService.GetTagCount(c), Is.EqualTo(2));
        }

        [Test]
        public void AddTags_UnknownCarrier_ReturnsZero()
        {
            Assert.That(tagService.AddTags(CarrierId.Invalid, new[] { "物品.食物" }), Is.EqualTo(0));
        }

        [Test]
        public void AddTags_NullKeys_ReturnsZero()
        {
            var c = manager.CreateCarrier("Test");
            Assert.That(tagService.AddTags(c, null), Is.EqualTo(0));
        }

        [Test]
        public void AddTags_InvalidKeys_AreSkipped()
        {
            var c = manager.CreateCarrier("Test");

            var added = tagService.AddTags(c, new[] { "物品.食物", "", "a..b", "狀態.中毒" });

            Assert.That(added, Is.EqualTo(2));
            Assert.That(tagService.GetTagCount(c), Is.EqualTo(2));
        }

        [Test]
        public void AddTags_FiresOneEventPerAddedTag()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "已存在");

            var received = new List<string>();
            arch.RegisterEvent<CarrierTagAddedEvent>(e => received.Add(e.Key));

            tagService.AddTags(c, new[] { "甲", "乙", "已存在" });

            Assert.That(received.Count, Is.EqualTo(2));
            Assert.That(received, Is.EquivalentTo(new[] { "甲", "乙" }));
        }

        [Test]
        public void RemoveTags_ReturnsRemovedCount()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTags(c, new[] { "甲", "乙", "丙" });

            var removed = tagService.RemoveTags(c, new[] { "甲", "乙", "未持有" });

            Assert.That(removed, Is.EqualTo(2));
            Assert.That(tagService.GetTagCount(c), Is.EqualTo(1));
        }

        [Test]
        public void RemoveTags_FiresOneEventPerRemovedTag()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTags(c, new[] { "甲", "乙" });

            var received = new List<string>();
            arch.RegisterEvent<CarrierTagRemovedEvent>(e => received.Add(e.Key));

            tagService.RemoveTags(c, new[] { "甲", "乙", "未持有" });

            Assert.That(received.Count, Is.EqualTo(2));
            Assert.That(received, Is.EquivalentTo(new[] { "甲", "乙" }));
        }

        [Test]
        public void RemoveTags_NullKeys_ReturnsZero()
        {
            var c = manager.CreateCarrier("Test");
            Assert.That(tagService.RemoveTags(c, null), Is.EqualTo(0));
        }

        [Test]
        public void RestoreTags_CountsDuplicatesAndUnknownCarrier()
        {
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");

            // 首次全部新增
            Assert.That(tagService.RestoreTags(c, new[] { "甲", "乙" }), Is.EqualTo(2));
            // 重复项仍计入（已存在的键不算 Invalid）
            Assert.That(tagService.RestoreTags(c, new[] { "甲" }), Is.EqualTo(1));
            // 无效键不计入
            Assert.That(tagService.RestoreTags(c, new[] { "", "丙" }), Is.EqualTo(1));
        }

        [Test]
        public void RestoreTags_DoesNotCreateDuplicateSlots()
        {
            var c = manager.CreateCarrier("Test");
            tagService.RestoreTags(c, new[] { "甲", "甲", "甲" });

            Assert.That(tagService.GetTagCount(c), Is.EqualTo(1));
        }
    }

    /// <summary>
    ///     服务释放后的行为。释放应清空全部索引，且不留下可被后续查询命中的残留。
    /// </summary>
    [TestFixture]
    public class CarrierTagServiceDisposeTests
    {
        [Test]
        public void Dispose_ClearsAllTagState()
        {
            var arch = new CoreTestArchitecture();
            arch.Initialize();
            var manager = (CarrierManager)arch.GetCarrierManager();
            var tagService = arch.GetTagService();
            manager.RegisterType("Test");

            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物");
            var id = tagService.InternTag("物品.食物");

            tagService.Dispose();

            Assert.That(tagService.GetTagCount(c), Is.EqualTo(0));
            Assert.That(tagService.HasTag(c, "物品.食物"), Is.False);
            Assert.That(tagService.FindCarriersWithTag("物品.食物").Count(), Is.EqualTo(0));
            // 槽位空间已重置，旧 id 不再可解析
            Assert.That(tagService.GetTagKey(id), Is.Null);

            arch.Dispose();
        }

        [Test]
        public void Init_AfterDispose_ResetsSlotSpace()
        {
            var arch = new CoreTestArchitecture();
            arch.Initialize();
            var tagService = arch.GetTagService();

            var idBefore = tagService.InternTag("物品.食物");
            tagService.Init();
            var idAfter = tagService.InternTag("物品.食物");

            // 索引重置后槽位空间从头分配
            Assert.That(idAfter, Is.EqualTo(idBefore));
            Assert.That(tagService.GetTagKey(idAfter), Is.EqualTo("物品.食物"));

            arch.Dispose();
        }
    }

    [TestFixture]
    public class CarrierManagerTagApiTests
    {
        private CoreTestArchitecture arch;
        private CarrierManager manager;
        private ITagService tagService;

        [SetUp]
        public void SetUp()
        {
            arch = new CoreTestArchitecture();
            arch.Initialize();
            manager = (CarrierManager)arch.GetCarrierManager();
            tagService = arch.GetTagService();
            manager.RegisterType("Test");
        }

        [TearDown]
        public void TearDown()
        {
            arch.Dispose();
        }

        [Test]
        public void AddTag_Idempotent()
        {
            var c = manager.CreateCarrier("Test");
            Assert.That(tagService.AddTag(c, "物品.食物"), Is.True);
            Assert.That(tagService.AddTag(c, "物品.食物"), Is.False);
        }

        [Test]
        public void RemoveTag_Idempotent()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物");
            Assert.That(tagService.RemoveTag(c, "物品.食物"), Is.True);
            Assert.That(tagService.RemoveTag(c, "物品.食物"), Is.False);
        }

        [Test]
        public void AddTag_UnknownCarrier_ReturnsFalse()
        {
            Assert.That(tagService.AddTag(CarrierId.Invalid, "物品.食物"), Is.False);
        }

        [Test]
        public void HasExactTag_MatchesOnlyExact()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物.蘋果");
            Assert.That(tagService.HasExactTag(c, "物品.食物.蘋果"), Is.True);
            Assert.That(tagService.HasExactTag(c, "物品.食物"), Is.False);
            Assert.That(tagService.HasExactTag(c, "物品"), Is.False);
        }

        [Test]
        public void HasTag_MatchesHierarchical()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物.蘋果");
            Assert.That(tagService.HasTag(c, "物品.食物.蘋果"), Is.True);
            Assert.That(tagService.HasTag(c, "物品.食物"), Is.True);
            Assert.That(tagService.HasTag(c, "物品"), Is.True);
            Assert.That(tagService.HasTag(c, "物品.水果"), Is.False);
            Assert.That(tagService.HasTag(c, "裝備"), Is.False);
        }

        [Test]
        public void HasAllTags_AllPresent_ReturnsTrue()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物.蘋果");
            tagService.AddTag(c, "狀態.中毒");
            Assert.That(tagService.HasAllTags(c, new[] { "物品", "狀態" }), Is.True);
            Assert.That(tagService.HasAllTags(c, new[] { "物品", "未知" }), Is.False);
        }

        [Test]
        public void HasAnyTag_AnyPresent_ReturnsTrue()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物.蘋果");
            Assert.That(tagService.HasAnyTag(c, new[] { "未知", "物品" }), Is.True);
            Assert.That(tagService.HasAnyTag(c, new[] { "未知", "其他" }), Is.False);
        }

        [Test]
        public void HasNoneTags_AllAbsent_ReturnsTrue()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物");
            Assert.That(tagService.HasNoneTags(c, new[] { "未知", "其他" }), Is.True);
            Assert.That(tagService.HasNoneTags(c, new[] { "物品", "其他" }), Is.False);
        }

        [Test]
        public void GetTags_ReturnsAllKeys()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物");
            tagService.AddTag(c, "狀態.中毒");

            var tags = tagService.GetTags(c).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Assert.That(tags, Is.EquivalentTo(new[] { "物品.食物", "狀態.中毒" }));
            Assert.That(tagService.GetTagCount(c), Is.EqualTo(2));
        }

        [Test]
        public void FindCarriersWithTag_ExactMatch()
        {
            var c1 = manager.CreateCarrier("Test");
            var c2 = manager.CreateCarrier("Test");
            tagService.AddTag(c1, "物品.食物.蘋果");
            tagService.AddTag(c2, "物品.食物.香蕉");

            var result = tagService.FindCarriersWithTag("物品.食物.蘋果");
            Assert.That(result.Count(), Is.EqualTo(1));
            Assert.That(result.First(), Is.EqualTo(c1));
        }

        [Test]
        public void FindCarriersWithTag_HierarchicalMatch()
        {
            var c1 = manager.CreateCarrier("Test");
            var c2 = manager.CreateCarrier("Test");
            tagService.AddTag(c1, "物品.食物.蘋果");
            tagService.AddTag(c2, "物品.食物.香蕉");

            var result = tagService.FindCarriersWithTag("物品.食物");
            Assert.That(result.Count(), Is.EqualTo(2));
        }

        [Test]
        public void FindCarriersWithTags_AllAnyNone()
        {
            var c1 = manager.CreateCarrier("Test");
            var c2 = manager.CreateCarrier("Test");
            var c3 = manager.CreateCarrier("Test");

            tagService.AddTag(c1, "物品.食物.蘋果");
            tagService.AddTag(c1, "狀態.燃燒");

            tagService.AddTag(c2, "物品.食物.香蕉");
            tagService.AddTag(c2, "狀態.中毒");

            tagService.AddTag(c3, "物品.食物.香蕉");
            tagService.AddTag(c3, "狀態.燃燒");

            // all: [物品.食物.蘋果, 狀態.燃燒] -> 只有 c1
            var query1 = new TagQuery(all: new[] { "物品.食物.蘋果", "狀態.燃燒" });
            var result1 = tagService.FindCarriersWithTags(query1);
            Assert.That(result1.Count(), Is.EqualTo(1));
            Assert.That(result1.First(), Is.EqualTo(c1));

            // any: [狀態.中毒, 狀態.燃燒] -> c1,c2,c3 全中
            var query2 = new TagQuery(any: new[] { "狀態.中毒", "狀態.燃燒" });
            var result2 = tagService.FindCarriersWithTags(query2);
            Assert.That(result2.Count(), Is.EqualTo(3));

            // none: [物品.食物.蘋果] -> 排除 c1，剩 c2,c3
            var query3 = new TagQuery(none: new[] { "物品.食物.蘋果" });
            var result3 = tagService.FindCarriersWithTags(query3);
            Assert.That(result3.Count(), Is.EqualTo(2));
        }

        [Test]
        public void FindCarriersWithTag_UnknownTag_ReturnsEmpty()
        {
            manager.CreateCarrier("Test");
            Assert.That(tagService.FindCarriersWithTag("从未出现过的.Tag").Count(), Is.EqualTo(0));
        }

        [Test]
        public void FindCarriersWithTag_InvalidKey_ReturnsEmpty()
        {
            manager.CreateCarrier("Test");
            Assert.That(tagService.FindCarriersWithTag("").Count(), Is.EqualTo(0));
            Assert.That(tagService.FindCarriersWithTag("a..b").Count(), Is.EqualTo(0));
        }

        [Test]
        public void FindCarriersWithTags_AllPath_ExcludesDestroyedCarriers()
        {
            var c1 = manager.CreateCarrier("Test");
            var c2 = manager.CreateCarrier("Test");
            tagService.AddTags(c1, new[] { "物品.食物.蘋果", "狀態.燃燒" });
            tagService.AddTags(c2, new[] { "物品.食物.蘋果", "狀態.燃燒" });

            manager.DestroyCarrier(c1);

            var result = tagService.FindCarriersWithTags(
                new TagQuery(all: new[] { "物品.食物.蘋果", "狀態.燃燒" })).ToList();

            Assert.That(result, Is.EquivalentTo(new[] { c2 }));
        }

        [Test]
        public void FindCarriersWithTags_AllPath_UnknownTag_ReturnsEmpty()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "甲");

            var result = tagService.FindCarriersWithTags(new TagQuery(all: new[] { "甲", "从未出现过" }));

            Assert.That(result.Count(), Is.EqualTo(0));
        }

        [Test]
        public void FindCarriersWithTags_EmptyQuery_ReturnsEmpty()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "甲");

            Assert.That(tagService.FindCarriersWithTags(new TagQuery()).Count(), Is.EqualTo(0));
            Assert.That(tagService.FindCarriersWithTags(null).Count(), Is.EqualTo(0));
        }

        [Test]
        public void DestroyCarrier_RemovesFromReverseIndex()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物.蘋果");

            manager.DestroyCarrier(c);
            Assert.That(tagService.FindCarriersWithTag("物品.食物.蘋果").Count(), Is.EqualTo(0));
        }

        [Test]
        public void RemoveTag_UpdatesReverseIndex()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物.蘋果");

            tagService.RemoveTag(c, "物品.食物.蘋果");
            Assert.That(tagService.FindCarriersWithTag("物品.食物.蘋果").Count(), Is.EqualTo(0));
        }
    }

    [TestFixture]
    public class TagEventTests
    {
        private CoreTestArchitecture arch;
        private CarrierManager manager;
        private ITagService tagService;

        [SetUp]
        public void SetUp()
        {
            arch = new CoreTestArchitecture();
            arch.Initialize();
            manager = (CarrierManager)arch.GetCarrierManager();
            tagService = arch.GetTagService();
            manager.RegisterType("Test");
        }

        [TearDown]
        public void TearDown()
        {
            arch.Dispose();
        }

        [Test]
        public void AddTag_Fires_CarrierTagAddedEvent()
        {
            var c = manager.CreateCarrier("Test");

            CarrierTagAddedEvent? received = null;
            arch.RegisterEvent<CarrierTagAddedEvent>(e => received = e);

            tagService.AddTag(c, "物品.食物.蘋果");

            Assert.That(received.HasValue, Is.True);
            Assert.That(received.Value.Carrier, Is.EqualTo(c));
            Assert.That(received.Value.Key, Is.EqualTo("物品.食物.蘋果"));
        }

        [Test]
        public void RemoveTag_Fires_CarrierTagRemovedEvent()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物.蘋果");

            CarrierTagRemovedEvent? received = null;
            arch.RegisterEvent<CarrierTagRemovedEvent>(e => received = e);

            tagService.RemoveTag(c, "物品.食物.蘋果");

            Assert.That(received.HasValue, Is.True);
            Assert.That(received.Value.Carrier, Is.EqualTo(c));
            Assert.That(received.Value.Key, Is.EqualTo("物品.食物.蘋果"));
        }

        [Test]
        public void AddTag_Duplicate_NoEvent()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物");

            int eventCount = 0;
            arch.RegisterEvent<CarrierTagAddedEvent>(_ => eventCount++);

            tagService.AddTag(c, "物品.食物");
            Assert.That(eventCount, Is.EqualTo(0));
        }

        [Test]
        public void DestroyCarrier_Fires_CarrierDestroyedEvent()
        {
            var c = manager.CreateCarrier("Test");

            CarrierDestroyedEvent? received = null;
            arch.RegisterEvent<CarrierDestroyedEvent>(e => received = e);

            manager.DestroyCarrier(c);

            Assert.That(received.HasValue, Is.True);
            Assert.That(received.Value.Carrier, Is.EqualTo(c));
        }

        [Test]
        public void DestroyCarrier_CleansIndexBeforeEventFires()
        {
            var c = manager.CreateCarrier("Test");
            tagService.AddTag(c, "物品.食物");

            // 订阅者不应看到已销毁载体的残留索引
            int foundInCallback = -1;
            arch.RegisterEvent<CarrierDestroyedEvent>(_ =>
                foundInCallback = tagService.FindCarriersWithTag("物品.食物").Count());

            manager.DestroyCarrier(c);

            Assert.That(foundInCallback, Is.EqualTo(0));
        }
    }

    [TestFixture]
    public class MultiArchitectureIsolationTests
    {
        [Test]
        public void RuntimeTagSlots_ArePerManager()
        {
            var arch1 = new CoreTestArchitecture();
            var arch2 = new CoreTestArchitecture();
            arch1.Initialize();
            arch2.Initialize();

            var m1 = (CarrierManager)arch1.GetCarrierManager();
            var m2 = (CarrierManager)arch2.GetCarrierManager();
            var t1 = arch1.GetTagService();
            var t2 = arch2.GetTagService();
            m1.RegisterType("Test");
            m2.RegisterType("Test");

            var c1 = m1.CreateCarrier("Test");
            var c2 = m2.CreateCarrier("Test");
            t1.AddTag(c1, "物品.食物");
            t2.AddTag(c2, "物品.食物");

            // 两个 Tag 服务完全隔离
            Assert.That(t1.FindCarriersWithTag("物品.食物").Count(), Is.EqualTo(1));
            Assert.That(t2.FindCarriersWithTag("物品.食物").Count(), Is.EqualTo(1));
            Assert.That(t1.FindCarriersWithTag("物品.食物").First(), Is.EqualTo(c1));
            Assert.That(t2.FindCarriersWithTag("物品.食物").First(), Is.EqualTo(c2));

            arch1.Dispose();
            arch2.Dispose();
        }
    }

    [TestFixture]
    public class TraitReverseIndexTests
    {
        private CoreTestArchitecture arch;
        private CarrierManager manager;

        [SetUp]
        public void SetUp()
        {
            arch = new CoreTestArchitecture();
            arch.Initialize();
            manager = (CarrierManager)arch.GetCarrierManager();
            manager.RegisterType("Test");
        }

        [TearDown]
        public void TearDown()
        {
            arch.Dispose();
        }

        [Test]
        public void FindCarriersWithTrait_UsesReverseIndex()
        {
            var c1 = manager.CreateCarrier("Test");
            var c2 = manager.CreateCarrier("Test");

            manager.AddTrait(c1, new BasicInfoTrait("甲"));
            manager.AddTrait(c2, new BasicInfoTrait("乙"));

            var carriers = manager.FindCarriersWithTrait<BasicInfoTrait>();
            Assert.That(carriers.Count(), Is.EqualTo(2));
        }

        [Test]
        public void RemoveTrait_CleansReverseIndex()
        {
            var c = manager.CreateCarrier("Test");
            manager.AddTrait(c, new BasicInfoTrait("甲"));

            manager.RemoveTrait<BasicInfoTrait>(c);
            Assert.That(manager.FindCarriersWithTrait<BasicInfoTrait>().Count(), Is.EqualTo(0));
        }

        [Test]
        public void DestroyCarrier_CleansTraitReverseIndex()
        {
            var c = manager.CreateCarrier("Test");
            manager.AddTrait(c, new BasicInfoTrait("甲"));

            manager.DestroyCarrier(c);
            Assert.That(manager.FindCarriersWithTrait<BasicInfoTrait>().Count(), Is.EqualTo(0));
        }
    }
}

