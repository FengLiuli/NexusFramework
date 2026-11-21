using NexusFramework.Identity;
using NUnit.Framework;

namespace NexusFramework.Core.Tests
{
    [TestFixture]
    public class IdAllocatorTests
    {
        [Test]
        public void Allocate_StartsAtOne_AndIncrements()
        {
            var allocator = new IdAllocator();

            Assert.That(allocator.Allocate(), Is.EqualTo(1));
            Assert.That(allocator.Allocate(), Is.EqualTo(2));
            Assert.That(allocator.Count, Is.EqualTo(2));
            Assert.That(allocator.Capacity, Is.EqualTo(2));
        }

        [Test]
        public void Free_ThenAllocate_ReusesSlot()
        {
            var allocator = new IdAllocator();
            var id = allocator.Allocate();

            Assert.That(allocator.Free(id), Is.True);
            Assert.That(allocator.Allocate(), Is.EqualTo(id));
        }

        [Test]
        public void Free_InvalidIds_ReturnFalse()
        {
            var allocator = new IdAllocator();

            Assert.That(allocator.Free(0), Is.False);
            Assert.That(allocator.Free(1), Is.False);

            var id = allocator.Allocate();
            Assert.That(allocator.Free(id), Is.True);
            Assert.That(allocator.Free(id), Is.False);
        }

        [Test]
        public void IsAlive_ReflectsState()
        {
            var allocator = new IdAllocator();
            var id = allocator.Allocate();

            Assert.That(allocator.IsAlive(id), Is.True);
            allocator.Free(id);
            Assert.That(allocator.IsAlive(id), Is.False);
        }

        [Test]
        public void Capacity_IsMonotonic()
        {
            var allocator = new IdAllocator();
            for (var i = 0; i < 10; i++)
                allocator.Allocate();

            var capacity = allocator.Capacity;
            for (var i = 1; i <= 10; i++)
                allocator.Free(i);

            Assert.That(allocator.Capacity, Is.EqualTo(capacity));
        }

        [Test]
        public void Clear_ResetsState()
        {
            var allocator = new IdAllocator();
            allocator.Allocate();

            allocator.Clear();

            Assert.That(allocator.Count, Is.Zero);
            Assert.That(allocator.Capacity, Is.Zero);
            Assert.That(allocator.Allocate(), Is.EqualTo(1));
        }
    }
}
