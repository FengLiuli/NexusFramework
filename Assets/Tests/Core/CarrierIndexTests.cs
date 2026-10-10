using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NexusFramework;
using NexusFramework.DataCarrier;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NexusFramework.Core.Tests
{
    /// <summary>
    ///     索引容器与 CarrierManager 索引注册表的行为契约。
    ///     对应设计文档 D005：写入三规则、引用完整性、销毁自动清理、异常隔离。
    /// </summary>
    internal class CarrierIndexHost : ICanGetCarrier
    {
        public IArchitecture Architecture { get; set; }
    }

    /// <summary>在 OnCarrierRemoved 中抛异常，用于验证派发的逐条异常隔离。</summary>
    internal class FaultyIndex : ICarrierIndex
    {
        public FaultyIndex(string name)
        {
            Name = name;
        }

        public bool RemovedCalled { get; private set; }

        public string Name { get; }

        public int EntryCount => 0;

        public void OnCarrierRemoved(CarrierId carrier)
        {
            RemovedCalled = true;
            throw new InvalidOperationException("boom");
        }
    }

    [TestFixture]
    public class CarrierIndexTests
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
        public void Ctor_EmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CarrierIndex<int>(null));
            Assert.Throws<ArgumentException>(() => new CarrierIndex<int>(""));
        }

        [Test]
        public void Bind_TryGet_RoundTrips()
        {
            var index = new CarrierIndex<int>("test");
            var c = manager.CreateCarrier("Test");

            Assert.That(index.Bind(c, 42), Is.True);
            Assert.That(index.TryGet(c, out var v), Is.True);
            Assert.That(v, Is.EqualTo(42));
            Assert.That(index.Contains(c), Is.True);
            Assert.That(index.EntryCount, Is.EqualTo(1));
        }

        [Test]
        public void Bind_Reverse_IsAlwaysMaintained()
        {
            var index = new CarrierIndex<string>("test");
            var c = manager.CreateCarrier("Test");

            index.Bind(c, "hero");

            Assert.That(index.TryGetCarrier("hero"), Is.EqualTo(c));
        }

        [Test]
        public void Bind_ValueConflict_EvictsOldOwner_AndMirrors()
        {
            var index = new CarrierIndex<int>("test");
            var a = manager.CreateCarrier("Test");
            var b = manager.CreateCarrier("Test");

            index.Bind(a, 7);
            index.Bind(b, 7); // 后者覆盖前者

            Assert.That(index.TryGet(a, out _), Is.False, "旧主应被驱逐");
            Assert.That(index.TryGet(b, out var v), Is.True);
            Assert.That(v, Is.EqualTo(7));
            Assert.That(index.TryGetCarrier(7), Is.EqualTo(b), "反查应指向新主");
            Assert.That(index.EntryCount, Is.EqualTo(1), "正反均只剩一条");
        }

        [Test]
        public void Bind_RebindSameCarrier_ReplacesValue_AndDropsOldReverse()
        {
            var index = new CarrierIndex<int>("test");
            var c = manager.CreateCarrier("Test");

            index.Bind(c, 1);
            index.Bind(c, 2);

            Assert.That(index.TryGet(c, out var v), Is.True);
            Assert.That(v, Is.EqualTo(2));
            Assert.That(index.TryGetCarrier(1), Is.EqualTo(CarrierId.Invalid), "旧值反向条目应被清除");
            Assert.That(index.TryGetCarrier(2), Is.EqualTo(c));
        }

        [Test]
        public void Bind_InvalidCarrier_Rejected_WhenRegistered()
        {
            var index = new CarrierIndex<int>("test");
            manager.RegisterIndex(index); // 注入 owner → 启用校验

            Assert.That(index.Bind(CarrierId.Invalid, 1), Is.False);
            Assert.That(index.EntryCount, Is.EqualTo(0));
        }

        [Test]
        public void Unbind_RemovesBothDirections()
        {
            var index = new CarrierIndex<int>("test");
            var c = manager.CreateCarrier("Test");
            index.Bind(c, 5);

            Assert.That(index.Unbind(c), Is.True);
            Assert.That(index.TryGet(c, out _), Is.False);
            Assert.That(index.TryGetCarrier(5), Is.EqualTo(CarrierId.Invalid));
            Assert.That(index.Unbind(c), Is.False, "重复解绑返回 false");
        }

        [Test]
        public void OnCarrierRemoved_ClearsSubjectRole()
        {
            var index = new CarrierIndex<int>("test");
            var c = manager.CreateCarrier("Test");
            index.Bind(c, 9);

            index.OnCarrierRemoved(c);

            Assert.That(index.TryGet(c, out _), Is.False);
            Assert.That(index.TryGetCarrier(9), Is.EqualTo(CarrierId.Invalid));
        }

        [Test]
        public void OnBindingRemoved_FiresOnEvict()
        {
            var index = new CarrierIndex<int>("test");
            var oldOwner = CarrierId.Invalid;
            var evictedValue = 0;
            index.OnBindingRemoved += (c, v) => { oldOwner = c; evictedValue = v; };

            var a = manager.CreateCarrier("Test");
            var b = manager.CreateCarrier("Test");
            index.Bind(a, 3);
            index.Bind(b, 3); // 驱逐 a

            Assert.That(oldOwner, Is.EqualTo(a));
            Assert.That(evictedValue, Is.EqualTo(3));
        }

        [Test]
        public void DestroyCarrier_AutoCleansIndex()
        {
            var index = new CarrierIndex<int>("test");
            manager.RegisterIndex(index);
            var c = manager.CreateCarrier("Test");
            index.Bind(c, 1);

            manager.DestroyCarrier(c);

            Assert.That(index.EntryCount, Is.EqualTo(0));
            Assert.That(index.TryGet(c, out _), Is.False);
        }
    }

    /// <summary>CarrierIndex&lt;CarrierId&gt; 的引用完整性：被指向的目标销毁时级联解除引用。</summary>
    [TestFixture]
    public class CarrierIndexReferenceIntegrityTests
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
        public void TargetDestroyed_EvictsSource_SourceSurvives()
        {
            var index = new CarrierIndex<CarrierId>("unit.target"); // 源 → 目标
            manager.RegisterIndex(index);
            var source = manager.CreateCarrier("Test");
            var target = manager.CreateCarrier("Test");
            index.Bind(source, target);

            manager.DestroyCarrier(target);

            Assert.That(index.TryGet(source, out _), Is.False, "指向已销毁目标的绑定应被级联解除");
            Assert.That(manager.IsCarrierValid(source), Is.True, "源本身存活");
        }

        [Test]
        public void SourceDestroyed_DoesNotAffectTarget()
        {
            var index = new CarrierIndex<CarrierId>("unit.target");
            manager.RegisterIndex(index);
            var source = manager.CreateCarrier("Test");
            var target = manager.CreateCarrier("Test");
            index.Bind(source, target);

            manager.DestroyCarrier(source);

            Assert.That(index.EntryCount, Is.EqualTo(0));
            Assert.That(manager.IsCarrierValid(target), Is.True, "目标不受影响");
        }

        [Test]
        public void SelfReference_RemovedWithoutThrow()
        {
            var index = new CarrierIndex<CarrierId>("self");
            manager.RegisterIndex(index);
            var c = manager.CreateCarrier("Test");
            index.Bind(c, c);

            Assert.DoesNotThrow(() => manager.DestroyCarrier(c));
            Assert.That(index.EntryCount, Is.EqualTo(0));
        }
    }

    [TestFixture]
    public class CarrierGroupIndexTests
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
        public void Ctor_EmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CarrierGroupIndex<int>(null));
        }

        [Test]
        public void Add_Get_Membership()
        {
            var g = new CarrierGroupIndex<string>("byFlag");
            var c1 = manager.CreateCarrier("Test");
            var c2 = manager.CreateCarrier("Test");

            Assert.That(g.Add("red", c1), Is.True);
            Assert.That(g.Add("red", c2), Is.True);
            Assert.That(g.Add("red", c1), Is.False, "重复加入幂等");

            Assert.That(g.Get("red").Count, Is.EqualTo(2));
            Assert.That(g.EntryCount, Is.EqualTo(2));
        }

        [Test]
        public void KeysOf_ReturnsAllKeysOfCarrier()
        {
            var g = new CarrierGroupIndex<string>("byFlag");
            var c = manager.CreateCarrier("Test");
            g.Add("red", c);
            g.Add("flying", c);

            Assert.That(g.KeysOf(c), Is.EquivalentTo(new[] { "red", "flying" }));
        }

        [Test]
        public void Remove_And_RemoveAll()
        {
            var g = new CarrierGroupIndex<string>("byFlag");
            var c1 = manager.CreateCarrier("Test");
            var c2 = manager.CreateCarrier("Test");
            g.Add("red", c1);
            g.Add("red", c2);

            Assert.That(g.Remove("red", c1), Is.True);
            Assert.That(g.Get("red"), Is.EquivalentTo(new[] { c2 }));

            Assert.That(g.RemoveAll("red"), Is.True);
            Assert.That(g.Get("red"), Is.Empty);
            Assert.That(g.KeysOf(c2), Is.Empty);
        }

        [Test]
        public void Add_InvalidCarrier_Rejected_WhenRegistered()
        {
            var g = new CarrierGroupIndex<string>("byFlag");
            manager.RegisterIndex(g);

            Assert.That(g.Add("red", CarrierId.Invalid), Is.False);
        }

        [Test]
        public void OnCarrierRemoved_RemovesMemberRole()
        {
            var g = new CarrierGroupIndex<string>("byFlag");
            manager.RegisterIndex(g);
            var c = manager.CreateCarrier("Test");
            g.Add("red", c);
            g.Add("blue", c);

            manager.DestroyCarrier(c);

            Assert.That(g.Get("red"), Is.Empty);
            Assert.That(g.Get("blue"), Is.Empty);
        }

        [Test]
        public void KeyIsCarrierId_TargetDestroyed_ClearsWholeGroup()
        {
            // target.lockers: (目标CarrierId) → {源CarrierId}
            var g = new CarrierGroupIndex<CarrierId>("target.lockers");
            manager.RegisterIndex(g);
            var target = manager.CreateCarrier("Test");
            var source1 = manager.CreateCarrier("Test");
            var source2 = manager.CreateCarrier("Test");
            g.Add(target, source1);
            g.Add(target, source2);

            Assert.That(g.Get(target).Count, Is.EqualTo(2));

            manager.DestroyCarrier(target);

            Assert.That(g.Get(target), Is.Empty, "以该载体为分组键的整组应被清除");
            Assert.That(manager.IsCarrierValid(source1), Is.True, "源本身存活");
        }

        [Test]
        public void KeyIsCarrierId_CarrierBothKeyAndMember_BothCleared()
        {
            var g = new CarrierGroupIndex<CarrierId>("mixed");
            manager.RegisterIndex(g);
            var c = manager.CreateCarrier("Test");
            var other = manager.CreateCarrier("Test");

            g.Add(c, other); // c 作为分组 key
            g.Add(other, c); // c 作为成员

            manager.DestroyCarrier(c);

            Assert.That(g.Get(c), Is.Empty, "c 作 key 的组被清");
            Assert.That(g.Get(other), Is.Empty, "c 作成员的组被清");
        }
    }

    [TestFixture]
    public class CarrierIndexRegistryTests
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
        public void RegisterIndex_DuplicateName_RejectsSecond()
        {
            var first = new CarrierIndex<int>("dup");
            var second = new CarrierIndex<int>("dup");

            manager.RegisterIndex(first);
            manager.RegisterIndex(second);

            Assert.That(manager.GetIndex("dup"), Is.SameAs(first), "保留先者");
        }

        [Test]
        public void RegisterIndex_SameInstance_NoOp()
        {
            var index = new CarrierIndex<int>("one");
            manager.RegisterIndex(index);
            manager.RegisterIndex(index);

            Assert.That(manager.GetAllIndexes().Count, Is.EqualTo(1));
        }

        [Test]
        public void RegisterIndex_EmptyName_Ignored()
        {
            var index = new CarrierIndex<int>("x");
            manager.RegisterIndex(index);
            manager.RegisterIndex(null);

            Assert.That(manager.GetAllIndexes().Count, Is.EqualTo(1));
        }

        [Test]
        public void UnregisterIndex_Removes()
        {
            var index = new CarrierIndex<int>("tmp");
            manager.RegisterIndex(index);

            Assert.That(manager.UnregisterIndex("tmp"), Is.True);
            Assert.That(manager.GetIndex("tmp"), Is.Null);
            Assert.That(manager.UnregisterIndex("tmp"), Is.False);
        }

        [Test]
        public void TryGetIndex_TypeMismatch_ReturnsFalse()
        {
            manager.RegisterIndex(new CarrierIndex<int>("t"));

            Assert.That(manager.TryGetIndex<CarrierIndex<string>>("t", out _), Is.False);
            Assert.That(manager.TryGetIndex<CarrierIndex<int>>("t", out var ok), Is.True);
            Assert.That(ok.Name, Is.EqualTo("t"));
        }

        [Test]
        public void TryGetIndex_UnknownName_ReturnsFalse()
        {
            Assert.That(manager.TryGetIndex<CarrierIndex<int>>("nope", out _), Is.False);
        }

        [Test]
        public void GetAllIndexes_ReturnsSnapshot()
        {
            manager.RegisterIndex(new CarrierIndex<int>("a"));
            var snapshot = manager.GetAllIndexes();
            manager.RegisterIndex(new CarrierIndex<int>("b"));

            Assert.That(snapshot.Count, Is.EqualTo(1), "快照不受后续注册影响");
            Assert.That(manager.GetAllIndexes().Count, Is.EqualTo(2));
        }

        [Test]
        public void RegisterCarrierIndex_Extension_RegistersAndReturns()
        {
            var host = new CarrierIndexHost { Architecture = arch };
            var index = new CarrierIndex<int>("ext");

            var returned = host.RegisterCarrierIndex(index);

            Assert.That(returned, Is.SameAs(index));
            Assert.That(manager.GetIndex("ext"), Is.SameAs(index));

            Assert.That(host.TryGetCarrierIndex<CarrierIndex<int>>("ext", out var got), Is.True);
            Assert.That(got, Is.SameAs(index));
        }
    }

    /// <summary>销毁派发：异常隔离、清理先于事件、Shutdown 后拒绝注册。</summary>
    [TestFixture]
    public class CarrierIndexDispatchTests
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
        public void FaultyIndex_DoesNotBlockOthers_AndEventStillFires()
        {
            var faulty = new FaultyIndex("faulty");
            var good = new CarrierIndex<int>("good");
            manager.RegisterIndex(faulty);
            manager.RegisterIndex(good);

            var c = manager.CreateCarrier("Test");
            good.Bind(c, 1);

            var eventFired = false;
            arch.RegisterEvent<CarrierDestroyedEvent>(_ => eventFired = true);

            // 隔离机制会为故障索引记录一条错误日志——声明为预期，避免被测试框架判为未处理错误。
            LogAssert.Expect(LogType.Error,
                new Regex(@"\[CarrierManager\] index 'faulty' OnCarrierRemoved failed: boom"));

            Assert.DoesNotThrow(() => manager.DestroyCarrier(c));
            Assert.That(faulty.RemovedCalled, Is.True, "异常索引被派发到");
            Assert.That(good.EntryCount, Is.EqualTo(0), "好索引仍被清理");
            Assert.That(eventFired, Is.True, "异常不阻断销毁广播");
        }

        [Test]
        public void IndexCleanedBeforeDestroyedEventFires()
        {
            var index = new CarrierIndex<int>("good");
            manager.RegisterIndex(index);
            var c = manager.CreateCarrier("Test");
            index.Bind(c, 1);

            var countInCallback = -1;
            arch.RegisterEvent<CarrierDestroyedEvent>(_ => countInCallback = index.EntryCount);

            manager.DestroyCarrier(c);

            Assert.That(countInCallback, Is.EqualTo(0), "订阅者反查不应看到残留");
        }

        [Test]
        public void RegisterIndex_AfterShutdown_Rejected()
        {
            var index = new CarrierIndex<int>("late");
            arch.Dispose(); // 触发 Shutdown → Deinit → Dispose

            manager.RegisterIndex(index);

            Assert.That(manager.GetIndex("late"), Is.Null, "关闭后拒绝注册");
        }
    }
}
