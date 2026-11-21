using System.Text.RegularExpressions;
using NexusFramework;
using NexusFramework.DataCarrier;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NexusFramework.Core.Tests
{
    /// <summary>
    ///     最小 Architecture，供 ArchitectureFactory 的 ID 分配 / 回收测试使用。
    ///     同时暴露 (byte) 构造器以支持工厂的 Activator 创建。
    /// </summary>
    internal class FactoryTestArchitecture : Architecture
    {
        public FactoryTestArchitecture() : base() { }
        public FactoryTestArchitecture(byte id) : base(id) { }

        public override string ArchitectureType => "FactoryTest";

        protected override void OnInit()
        {
        }
    }

    /// <summary>
    ///     ArchitectureFactory 的架构ID分配器契约：自动分配、固定ID、回收与复用、
    ///     碰撞拒绝、显式保留、池耗尽。
    ///     架构ID为 byte（上限256），与 Carrier 实例 id 无关。
    /// </summary>
    [TestFixture]
    public class ArchitectureFactoryIdTests
    {
        [SetUp]
        public void SetUp()
        {
            ArchitectureFactory.Reset();
            ArchitectureFactory.RegisterArchitecture<FactoryTestArchitecture>("FactoryTest");
        }

        [TearDown]
        public void TearDown()
        {
            ArchitectureFactory.Reset();
        }

        [Test]
        public void AutoAssign_StartsFromOne_AndIncrements()
        {
            var a = ArchitectureFactory.CreateArchitecture("FactoryTest");
            var b = ArchitectureFactory.CreateArchitecture("FactoryTest");

            Assert.That(a.ArchitectureId, Is.EqualTo((byte)1), "自动分配应从1开始（0保留给常驻框架）");
            Assert.That(b.ArchitectureId, Is.EqualTo((byte)2));
        }

        [Test]
        public void ExplicitId_Occupies_ThenAutoSkipsIt()
        {
            var fixedArch = ArchitectureFactory.CreateArchitecture("FactoryTest", 0);
            Assert.That(fixedArch.ArchitectureId, Is.EqualTo((byte)0));

            var auto = ArchitectureFactory.CreateArchitecture("FactoryTest");
            Assert.That(auto.ArchitectureId, Is.EqualTo((byte)1));
        }

        [Test]
        public void Create_DuplicateId_Rejected_AndKeepsOriginal()
        {
            var first = ArchitectureFactory.CreateArchitecture("FactoryTest", 0);

            LogAssert.Expect(LogType.Error, new Regex(@"架构ID 0 已被占用"));
            var second = ArchitectureFactory.CreateArchitecture("FactoryTest", 0);

            Assert.That(second, Is.Null, "重复ID应被拒绝");
            Assert.That(ArchitectureFactory.GetArchitecture(0), Is.SameAs(first), "原实例应保留");
        }

        [Test]
        public void Destroy_ReleasesId_AndNextAllocationReusesIt()
        {
            var a = ArchitectureFactory.CreateArchitecture("FactoryTest"); // 1
            var b = ArchitectureFactory.CreateArchitecture("FactoryTest"); // 2

            Assert.That(ArchitectureFactory.DestroyArchitecture(a.ArchitectureId), Is.True);
            Assert.That(ArchitectureFactory.IsIdOccupied(a.ArchitectureId), Is.False, "销毁后ID应归还");

            var c = ArchitectureFactory.CreateArchitecture("FactoryTest");
            Assert.That(c.ArchitectureId, Is.EqualTo(a.ArchitectureId), "应复用被释放的最小ID");
            Assert.That(ArchitectureFactory.GetArchitecture(b.ArchitectureId), Is.SameAs(b));
        }

        [Test]
        public void ReserveId_Occupies_AndReleaseFrees()
        {
            ArchitectureFactory.ReserveId(1);

            Assert.That(ArchitectureFactory.IsIdOccupied(1), Is.True);
            Assert.That(ArchitectureFactory.TryAllocateId(out var first), Is.True);
            Assert.That(first, Is.EqualTo((byte)2), "被保留的ID不参与分配");

            ArchitectureFactory.ReleaseId(1);
            Assert.That(ArchitectureFactory.TryAllocateId(out var second), Is.True);
            Assert.That(second, Is.EqualTo((byte)1), "释放后该ID重新可用");
        }

        [Test]
        public void ReserveId_Duplicate_LogsWarning()
        {
            ArchitectureFactory.ReserveId(3);

            LogAssert.Expect(LogType.Warning, new Regex(@"架构ID 3 被重复保留"));
            ArchitectureFactory.ReserveId(3);
        }

        [Test]
        public void IdPool_ExhaustsAt255_ThenRejects()
        {
            // 自动分配可用 1..255 共 255 个槽位。
            for (var i = 0; i < 255; i++)
                Assert.That(ArchitectureFactory.CreateArchitecture("FactoryTest"), Is.Not.Null, $"第{i + 1}次分配应成功");

            LogAssert.Expect(LogType.Error, new Regex(@"池已耗尽"));
            Assert.That(ArchitectureFactory.TryAllocateId(out _), Is.False, "池耗尽后应拒绝分配");
        }

        [Test]
        public void Reset_ClearsIdAllocation()
        {
            var a = ArchitectureFactory.CreateArchitecture("FactoryTest"); // 1
            Assert.That(ArchitectureFactory.IsIdOccupied(a.ArchitectureId), Is.True);

            ArchitectureFactory.Reset();

            Assert.That(ArchitectureFactory.IsIdOccupied(1), Is.False, "Reset 应清空ID占用");
        }
    }

    /// <summary>
    ///     每个架构持有独立的 IdRegistry：同为 "Test" 的类型在各自架构内都从 1 起编号，
    ///     互不共享载体命名空间。
    /// </summary>
    [TestFixture]
    public class ArchitectureCarrierNamespaceTests
    {
        [SetUp]
        public void SetUp()
        {
            ArchitectureFactory.Reset();
            ArchitectureFactory.RegisterArchitecture<FactoryTestArchitecture>("FactoryTest");
        }

        [TearDown]
        public void TearDown()
        {
            ArchitectureFactory.Reset();
        }

        [Test]
        public void Carrier_UsesRuntimeId_FromManagerAllocator()
        {
            var arch = ArchitectureFactory.CreateArchitecture("FactoryTest", 7);
            var manager = (CarrierManager)arch.GetCarrierManager();
            manager.RegisterType("Test");

            var carrier = manager.CreateCarrier("Test");

            Assert.That(carrier > 0, Is.True);
            Assert.That(carrier, Is.EqualTo(1));
            Assert.That(manager.IsCarrierValid(carrier), Is.True);
        }

        [Test]
        public void TwoArchitectures_AllocateIndependently()
        {
            var a = ArchitectureFactory.CreateArchitecture("FactoryTest", 3);
            var b = ArchitectureFactory.CreateArchitecture("FactoryTest", 4);

            var managerA = (CarrierManager)a.GetCarrierManager();
            var managerB = (CarrierManager)b.GetCarrierManager();
            managerA.RegisterType("Test");
            managerB.RegisterType("Test");

            var carrierA = managerA.CreateCarrier("Test");
            var carrierB = managerB.CreateCarrier("Test");

            Assert.That(carrierA > 0, Is.True);
            Assert.That(carrierB > 0, Is.True);
            Assert.That(managerA.TotalCarrierCount, Is.EqualTo(1));
            Assert.That(managerB.TotalCarrierCount, Is.EqualTo(1));
        }

        [Test]
        public void Restart_OldCarrierIdsAreInvalid()
        {
            var a = ArchitectureFactory.CreateArchitecture("FactoryTest", 5);
            var managerA = (CarrierManager)a.GetCarrierManager();
            managerA.RegisterType("Test");
            var oldId = managerA.CreateCarrier("Test");
            Assert.That(oldId, Is.EqualTo(1));

            ArchitectureFactory.DestroyArchitecture(a.ArchitectureId);

            var b = ArchitectureFactory.CreateArchitecture("FactoryTest", 5);
            var managerB = (CarrierManager)b.GetCarrierManager();
            managerB.RegisterType("Test");

            Assert.That(managerA.IsCarrierValid(oldId), Is.False, "架构关闭后旧 id 必须失效");
            Assert.That(managerB.IsCarrierValid(oldId), Is.False, "新架构不得沿用旧 id");
            Assert.That(managerB.CreateCarrier("Test"), Is.EqualTo(1), "新架构从 1 重新发号");
        }
    }
}
