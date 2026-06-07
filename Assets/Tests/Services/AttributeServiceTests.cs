using NexusFramework;
using Unity.Collections;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using NexusFramework.GAS.Services;
using NUnit.Framework;
using Unity.Entities;

namespace NexusFramework.GAS.Tests.Services
{
    [TestFixture]
    public class AttributeServiceTests
    {
        private TestArchitecture _arch;
        private AttributeService _service;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _arch = new TestArchitecture();
            _arch.Initialize();
            _arch.GetCarrierManager().RegisterType("TestUnit");
            _service = _arch.GetService<AttributeService>();
            _em = _arch.GetService<WorldService>().EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            _arch?.Dispose();
            _arch = null;
        }

        [Test]
        public void GetCurrentValue_Returns_CorrectValue()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            SetupAttribute(entity, attrSetCode: 1, attrCode: 10, baseValue: 100f, currentValue: 75f);

            Assert.That(_service.GetCurrentValue(carrier, 1, 10), Is.EqualTo(75f).Within(0.01f));
        }

        [Test]
        public void GetBaseValue_Returns_CorrectValue()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            SetupAttribute(entity, attrSetCode: 2, attrCode: 20, baseValue: 200f, currentValue: 200f);

            Assert.That(_service.GetBaseValue(carrier, 2, 20), Is.EqualTo(200f).Within(0.01f));
        }

        [Test]
        public void HasAttribute_Existing_ReturnsTrue()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            SetupAttribute(entity, attrSetCode: 1, attrCode: 10, baseValue: 100f, currentValue: 100f);

            Assert.That(_service.HasAttribute(carrier, 1, 10), Is.True);
        }

        [Test]
        public void HasAttribute_Missing_ReturnsFalse()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            Assert.That(_service.HasAttribute(carrier, 99, 99), Is.False);
        }

        [Test]
        public void SetBaseValue_UpdatesValue()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            SetupAttribute(entity, attrSetCode: 3, attrCode: 30, baseValue: 50f, currentValue: 50f);

            _service.SetBaseValue(carrier, 3, 30, 999f);

            Assert.That(_service.GetBaseValue(carrier, 3, 30), Is.EqualTo(999f).Within(0.01f));
        }

        [Test]
        public void SetBaseValue_MarksDirty()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);

            SetupAttribute(entity, attrSetCode: 4, attrCode: 40, baseValue: 1f, currentValue: 1f);
            _service.SetBaseValue(carrier, 4, 40, 2f);

            Assert.That(_em.HasComponent<CAttributeIsDirty>(entity), Is.True,
                "SetBaseValue 应标记 CAttributeIsDirty");
        }

        [Test]
        public void SetCurrentValue_UpdatesValue()
        {
            var carrier = _arch.CreateGASCarrier("TestUnit");
            var entity = _arch.GetModel<GASEntityMapModel>().GetGASEntity(carrier);
            SetupAttribute(entity, attrSetCode: 5, attrCode: 50, baseValue: 10f, currentValue: 10f);

            _service.SetCurrentValue(carrier, 5, 50, 42f);

            Assert.That(_service.GetCurrentValue(carrier, 5, 50), Is.EqualTo(42f).Within(0.01f));
        }

        [Test]
        public void InvalidCarrier_ReturnsZero()
        {
            Assert.That(_service.GetCurrentValue(new NexusFramework.DataCarrier.CarrierId(), 1, 1), Is.EqualTo(0f));
            Assert.That(_service.GetBaseValue(new NexusFramework.DataCarrier.CarrierId(), 1, 1), Is.EqualTo(0f));
            Assert.That(_service.HasAttribute(new NexusFramework.DataCarrier.CarrierId(), 1, 1), Is.False);
        }

        private void SetupAttribute(Entity e, int attrSetCode, int attrCode, float baseValue, float currentValue)
        {
            var buf = _em.GetBuffer<BEAttrSet>(e);
            var attrs = new NativeArray<CAttributeData>(1, Allocator.Persistent);
            attrs[0] = new CAttributeData { Code = attrCode, BaseValue = baseValue, CurrentValue = currentValue };
            buf.Add(new BEAttrSet { Code = attrSetCode, Attributes = attrs });
        }
    }
}
