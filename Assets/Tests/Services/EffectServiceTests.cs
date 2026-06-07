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
    public class EffectServiceTests
    {
        private TestArchitecture _arch;
        private EffectService _service;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _arch = new TestArchitecture();
            _arch.Initialize();
            _arch.GetCarrierManager().RegisterType("TestUnit");
            _service = _arch.GetService<EffectService>();
            _em = _arch.GetService<WorldService>().EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            _arch?.Dispose();
            _arch = null;
        }

        // ── 无配置时应容错 ──────────────────────────

        [Test]
        public void ApplyEffect_WithNonExistentConfig_DoesNotThrow()
        {
            var source = _arch.CreateGASCarrier("TestUnit");
            var target = _arch.CreateGASCarrier("TestUnit");

            Assert.DoesNotThrow(() =>
            {
                _service.ApplyEffect(configId: 99999, target: target, source: source);
            });
        }

        [Test]
        public void ApplyEffect_WithNonExistentCarrier_DoesNotThrow()
        {
            var invalid = new CarrierId();

            Assert.DoesNotThrow(() =>
            {
                _service.ApplyEffect(configId: 0, target: invalid, source: invalid);
            });
        }
    }
}
