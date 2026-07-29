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
    public class TagTraitTests
    {
        private CoreTestArchitecture arch;
        private CarrierManager manager;

        [SetUp]
        public void SetUp()
        {
            arch = new CoreTestArchitecture();
            arch.Initialize();
            manager = (CarrierManager)arch.GetCarrierManager();
        }

        [TearDown]
        public void TearDown()
        {
            arch.Dispose();
        }

        [Test]
        public void TagTrait_HasParameterlessConstructor()
        {
            Assert.DoesNotThrow(() => Activator.CreateInstance<TagTrait>());
        }

        [Test]
        public void ToJson_ReturnsOnlyStringKeys()
        {
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "物品.食物.蘋果");

            var json = manager.SerializeCarrier(c);
            Assert.That(json, Does.Contain("物品.食物.蘋果"));
            Assert.That(json, Does.Not.Contain("slot"));
            Assert.That(json, Does.Not.Contain("Slot"));
            Assert.That(json, Does.Not.Contain("Value"));
        }

        [Test]
        public void Clone_ProducesIndependentCopy()
        {
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "狀態.中毒");
            manager.AddTag(c, "狀態.燃燒");

            var original = manager.GetTrait<TagTrait>(c);
            var clone = (TagTrait)original.Clone();

            Assert.That(this.GetSlotCount(clone), Is.EqualTo(2));
        }

        [Test]
        public void DeserializeCarrier_RestoresTags()
        {
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "物品.食物");
            manager.AddTag(c, "狀態.中毒");

            var json = manager.SerializeCarrier(c);

            var manager2 = new CarrierManager();
            manager2.Init();
            manager2.RegisterType("Test");
            var restored = manager2.DeserializeCarrier(json);

            Assert.That(manager2.HasExactTag(restored, "物品.食物"), Is.True);
            Assert.That(manager2.HasExactTag(restored, "狀態.中毒"), Is.True);
            Assert.That(manager2.HasExactTag(restored, "不存在.Tag"), Is.False);

            manager2.Dispose();
        }

        [Test]
        public void DeserializeCarrier_AfterDestroy_NoLeak()
        {
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "Tag.ToRemove");
            manager.DestroyCarrier(c);

            var json = manager.SerializeCarrier(c);
            Assert.That(json, Is.Null);
        }

        private int GetSlotCount(TagTrait trait)
        {
            return trait.Slots.Count();
        }
    }

    [TestFixture]
    public class CarrierManagerTagApiTests
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
        public void AddTag_Idempotent()
        {
            var c = manager.CreateCarrier("Test");
            Assert.That(manager.AddTag(c, "物品.食物"), Is.True);
            Assert.That(manager.AddTag(c, "物品.食物"), Is.False);
        }

        [Test]
        public void RemoveTag_Idempotent()
        {
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "物品.食物");
            Assert.That(manager.RemoveTag(c, "物品.食物"), Is.True);
            Assert.That(manager.RemoveTag(c, "物品.食物"), Is.False);
        }

        [Test]
        public void HasExactTag_MatchesOnlyExact()
        {
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "物品.食物.蘋果");
            Assert.That(manager.HasExactTag(c, "物品.食物.蘋果"), Is.True);
            Assert.That(manager.HasExactTag(c, "物品.食物"), Is.False);
            Assert.That(manager.HasExactTag(c, "物品"), Is.False);
        }

        [Test]
        public void HasTag_MatchesHierarchical()
        {
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "物品.食物.蘋果");
            Assert.That(manager.HasTag(c, "物品.食物.蘋果"), Is.True);
            Assert.That(manager.HasTag(c, "物品.食物"), Is.True);
            Assert.That(manager.HasTag(c, "物品"), Is.True);
            Assert.That(manager.HasTag(c, "物品.水果"), Is.False);
            Assert.That(manager.HasTag(c, "裝備"), Is.False);
        }

        [Test]
        public void HasAllTags_AllPresent_ReturnsTrue()
        {
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "物品.食物.蘋果");
            manager.AddTag(c, "狀態.中毒");
            Assert.That(manager.HasAllTags(c, new[] { "物品", "狀態" }), Is.True);
            Assert.That(manager.HasAllTags(c, new[] { "物品", "未知" }), Is.False);
        }

        [Test]
        public void HasAnyTag_AnyPresent_ReturnsTrue()
        {
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "物品.食物.蘋果");
            Assert.That(manager.HasAnyTag(c, new[] { "未知", "物品" }), Is.True);
            Assert.That(manager.HasAnyTag(c, new[] { "未知", "其他" }), Is.False);
        }

        [Test]
        public void HasNoneTags_AllAbsent_ReturnsTrue()
        {
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "物品.食物");
            Assert.That(manager.HasNoneTags(c, new[] { "未知", "其他" }), Is.True);
            Assert.That(manager.HasNoneTags(c, new[] { "物品", "其他" }), Is.False);
        }

        [Test]
        public void FindCarriersWithTag_ExactMatch()
        {
            var c1 = manager.CreateCarrier("Test");
            var c2 = manager.CreateCarrier("Test");
            manager.AddTag(c1, "物品.食物.蘋果");
            manager.AddTag(c2, "物品.食物.香蕉");

            var result = manager.FindCarriersWithTag("物品.食物.蘋果");
            Assert.That(result.Count(), Is.EqualTo(1));
            Assert.That(result.First(), Is.EqualTo(c1));
        }

        [Test]
        public void FindCarriersWithTag_HierarchicalMatch()
        {
            var c1 = manager.CreateCarrier("Test");
            var c2 = manager.CreateCarrier("Test");
            manager.AddTag(c1, "物品.食物.蘋果");
            manager.AddTag(c2, "物品.食物.香蕉");

            var result = manager.FindCarriersWithTag("物品.食物");
            Assert.That(result.Count(), Is.EqualTo(2));
        }

        [Test]
        public void FindCarriersWithTags_AllAnyNone()
        {
            var c1 = manager.CreateCarrier("Test");
            var c2 = manager.CreateCarrier("Test");
            var c3 = manager.CreateCarrier("Test");

            manager.AddTag(c1, "物品.食物.蘋果");
            manager.AddTag(c1, "狀態.燃燒");

            manager.AddTag(c2, "物品.食物.香蕉");
            manager.AddTag(c2, "狀態.中毒");

            manager.AddTag(c3, "物品.食物.香蕉");
            manager.AddTag(c3, "狀態.燃燒");

            // all: [物品.食物.蘋果, 狀態.燃燒] -> only c1 (exact match for both, not hierarchical)
            var query1 = new TagQuery(all: new[] { "物品.食物.蘋果", "狀態.燃燒" });
            var result1 = manager.FindCarriersWithTags(query1);
            Assert.That(result1.Count(), Is.EqualTo(1));
            Assert.That(result1.First(), Is.EqualTo(c1));

            // any: [狀態.中毒, 狀態.燃燒] -> c1,c2,c3 all match
            var query2 = new TagQuery(any: new[] { "狀態.中毒", "狀態.燃燒" });
            var result2 = manager.FindCarriersWithTags(query2);
            Assert.That(result2.Count(), Is.EqualTo(3));

            // none: [物品.食物.蘋果] -> c2,c3 (exclude c1)
            var query3 = new TagQuery(none: new[] { "物品.食物.蘋果" });
            var result3 = manager.FindCarriersWithTags(query3);
            Assert.That(result3.Count(), Is.EqualTo(2));
        }

        [Test]
        public void DestroyCarrier_RemovesFromReverseIndex()
        {
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "物品.食物.蘋果");

            manager.DestroyCarrier(c);
            Assert.That(manager.FindCarriersWithTag("物品.食物.蘋果").Count(), Is.EqualTo(0));
        }

        [Test]
        public void RemoveTag_UpdatesReverseIndex()
        {
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "物品.食物.蘋果");

            manager.RemoveTag(c, "物品.食物.蘋果");
            Assert.That(manager.FindCarriersWithTag("物品.食物.蘋果").Count(), Is.EqualTo(0));
        }
    }

    [TestFixture]
    public class TagEventTests
    {
        [Test]
        public void AddTag_Fires_CarrierTagAddedEvent()
        {
            var arch = new CoreTestArchitecture();
            arch.Initialize();
            var manager = (CarrierManager)arch.GetCarrierManager();
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");

            CarrierTagAddedEvent? received = null;
            arch.RegisterEvent<CarrierTagAddedEvent>(e => received = e);

            manager.AddTag(c, "物品.食物.蘋果");

            Assert.That(received.HasValue, Is.True);
            Assert.That(received.Value.Carrier, Is.EqualTo(c));
            Assert.That(received.Value.Key, Is.EqualTo("物品.食物.蘋果"));

            arch.Dispose();
        }

        [Test]
        public void RemoveTag_Fires_CarrierTagRemovedEvent()
        {
            var arch = new CoreTestArchitecture();
            arch.Initialize();
            var manager = (CarrierManager)arch.GetCarrierManager();
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "物品.食物.蘋果");

            CarrierTagRemovedEvent? received = null;
            arch.RegisterEvent<CarrierTagRemovedEvent>(e => received = e);

            manager.RemoveTag(c, "物品.食物.蘋果");

            Assert.That(received.HasValue, Is.True);
            Assert.That(received.Value.Carrier, Is.EqualTo(c));
            Assert.That(received.Value.Key, Is.EqualTo("物品.食物.蘋果"));

            arch.Dispose();
        }

        [Test]
        public void AddTag_Duplicate_NoEvent()
        {
            var arch = new CoreTestArchitecture();
            arch.Initialize();
            var manager = (CarrierManager)arch.GetCarrierManager();
            manager.RegisterType("Test");
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "物品.食物");

            int eventCount = 0;
            arch.RegisterEvent<CarrierTagAddedEvent>(_ => eventCount++);

            manager.AddTag(c, "物品.食物");
            Assert.That(eventCount, Is.EqualTo(0));

            arch.Dispose();
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
            m1.RegisterType("Test");
            m2.RegisterType("Test");

            var c1 = m1.CreateCarrier("Test");
            var c2 = m2.CreateCarrier("Test");
            m1.AddTag(c1, "物品.食物");
            m2.AddTag(c2, "物品.食物");

            // 两个 Manager 完全隔离
            Assert.That(m1.FindCarriersWithTag("物品.食物").Count(), Is.EqualTo(1));
            Assert.That(m2.FindCarriersWithTag("物品.食物").Count(), Is.EqualTo(1));
            Assert.That(m1.FindCarriersWithTag("物品.食物").First(), Is.EqualTo(c1));
            Assert.That(m2.FindCarriersWithTag("物品.食物").First(), Is.EqualTo(c2));

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

            // 通过 AddTag 内部创建的 TagTrait
            manager.AddTag(c1, "A");
            manager.AddTag(c2, "B");

            var carriers = manager.FindCarriersWithTrait<TagTrait>();
            Assert.That(carriers.Count(), Is.EqualTo(2));
        }

        [Test]
        public void RemoveTrait_CleansReverseIndex()
        {
            var c = manager.CreateCarrier("Test");
            manager.AddTag(c, "測試");

            manager.RemoveTrait<TagTrait>(c);
            Assert.That(manager.FindCarriersWithTrait<TagTrait>().Count(), Is.EqualTo(0));
        }
    }
}
