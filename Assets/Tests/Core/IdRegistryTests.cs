using NexusFramework.Identity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NexusFramework.Core.Tests
{
    [TestFixture]
    public class IdRegistryTests
    {
        [Test]
        public void GetOrCreateIndex_ReturnsSameInstance()
        {
            var registry = new IdRegistry();

            var first = registry.GetOrCreateIndex("test");
            var second = registry.GetOrCreateIndex("test");

            Assert.That(first, Is.Not.Null);
            Assert.That(first, Is.SameAs(second));
        }

        [Test]
        public void SameName_DifferentPrimitive_ReturnsNull()
        {
            var registry = new IdRegistry();
            Assert.That(registry.GetOrCreateIndex("test"), Is.Not.Null);

            LogAssert.Expect(LogType.Error,
                "[IdRegistry] Domain 'test' is already registered as a different primitive.");
            Assert.That(registry.GetOrCreateAllocator("test"), Is.Null);

            LogAssert.Expect(LogType.Error,
                "[IdRegistry] Domain 'test' is already registered as a different primitive.");
            Assert.That(registry.GetOrCreateTypedAllocator("test"), Is.Null);
        }

        [Test]
        public void Clear_RemovesAllDomains()
        {
            var registry = new IdRegistry();
            registry.GetOrCreateIndex("a");
            registry.GetOrCreateAllocator("b");
            registry.GetOrCreateTypedAllocator("c");

            registry.Clear();

            Assert.That(registry.Names, Is.Empty);
            Assert.That(registry.TryGetIndex("a", out _), Is.False);
            Assert.That(registry.TryGetAllocator("b", out _), Is.False);
            Assert.That(registry.TryGetTypedAllocator("c", out _), Is.False);
        }

        [Test]
        public void Remove_DeletesDomain()
        {
            var registry = new IdRegistry();
            registry.GetOrCreateIndex("a");

            Assert.That(registry.Remove("a"), Is.True);
            Assert.That(registry.TryGetIndex("a", out _), Is.False);
            Assert.That(registry.Remove("a"), Is.False);
        }
    }
}
