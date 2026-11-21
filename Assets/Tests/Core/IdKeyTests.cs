using NexusFramework.Identity;
using NUnit.Framework;

namespace NexusFramework.Core.Tests
{
    [TestFixture]
    public class IdKeyTests
    {
        [Test]
        public void Parse_ValidKey_SplitsNamespaceAndPath()
        {
            var key = IdKey.Parse(" core:item.potion ");

            Assert.That(key.Namespace, Is.EqualTo("core"));
            Assert.That(key.Path, Is.EqualTo("item.potion"));
            Assert.That(key.Value, Is.EqualTo("core:item.potion"));
            Assert.That(key.IsLegacy, Is.False);
        }

        [Test]
        public void TryParse_LegacyKey_Succeeds_WhenNamespaceNotRequired()
        {
            Assert.That(IdKey.TryParse("combat.buff", false, out var key), Is.True);

            Assert.That(key.IsLegacy, Is.True);
            Assert.That(key.Namespace, Is.Null);
            Assert.That(key.Path, Is.EqualTo("combat.buff"));
        }

        [Test]
        public void TryParse_LegacyKey_Fails_WhenNamespaceRequired()
        {
            Assert.That(IdKey.TryParse("combat.buff", true, out _), Is.False);
        }

        [Test]
        public void TryParse_MultipleColons_Fails()
        {
            Assert.That(IdKey.TryParse("a:b:c", false, out _), Is.False);
        }

        [Test]
        public void TryParse_EmptySegment_Fails()
        {
            Assert.That(IdKey.TryParse(":path", false, out _), Is.False);
            Assert.That(IdKey.TryParse("ns:", false, out _), Is.False);
        }

        [Test]
        public void TryParse_Whitespace_IsRejected()
        {
            Assert.That(IdKey.TryParse("   ", false, out _), Is.False);
        }

        [Test]
        public void Equality_UsesOrdinalValue()
        {
            var a = IdKey.Parse("core:item");
            var b = IdKey.Parse(" core:item ");

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }
    }
}
