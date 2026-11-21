using NexusFramework.Identity;
using NUnit.Framework;

namespace NexusFramework.Core.Tests
{
    [TestFixture]
    public class TypedAllocatorTests
    {
        [Test]
        public void Allocate_RecordsType()
        {
            var allocator = new TypedAllocator("test");
            var id = allocator.Allocate(7);

            Assert.That(id, Is.EqualTo(1));
            Assert.That(allocator.TryGetType(id, out var typeId), Is.True);
            Assert.That(typeId, Is.EqualTo(7));
        }

        [Test]
        public void IsType_ChecksExactType()
        {
            var allocator = new TypedAllocator("test");
            var id = allocator.Allocate(7);

            Assert.That(allocator.IsType(id, 7), Is.True);
            Assert.That(allocator.IsType(id, 8), Is.False);
        }

        [Test]
        public void Free_ClearsType_AndReusesSlot()
        {
            var allocator = new TypedAllocator("test");
            var id = allocator.Allocate(7);

            Assert.That(allocator.Free(id), Is.True);
            Assert.That(allocator.TryGetType(id, out _), Is.False);
            Assert.That(allocator.Allocate(8), Is.EqualTo(id));
        }

        [Test]
        public void Allocate_NonPositiveType_ReturnsZero()
        {
            var allocator = new TypedAllocator("test");

            Assert.That(allocator.Allocate(0), Is.Zero);
            Assert.That(allocator.Allocate(-1), Is.Zero);
        }

        [Test]
        public void SupportsTypeIdsBeyondUShort()
        {
            var allocator = new TypedAllocator("test");
            const int typeId = 100_000;

            var id = allocator.Allocate(typeId);

            Assert.That(allocator.TryGetType(id, out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(typeId));
        }
    }
}
