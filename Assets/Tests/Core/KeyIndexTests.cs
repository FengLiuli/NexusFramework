using System.Collections.Generic;
using NexusFramework.Identity;
using NUnit.Framework;

namespace NexusFramework.Core.Tests
{
    [TestFixture]
    public class KeyIndexTests
    {
        [Test]
        public void Intern_IsIdempotent()
        {
            var index = new KeyIndex("test");

            Assert.That(index.Intern("core:a"), Is.EqualTo(1));
            Assert.That(index.Intern("core:a"), Is.EqualTo(1));
            Assert.That(index.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryGetKey_ReturnsRegisteredKey()
        {
            var index = new KeyIndex("test");
            var id = index.Intern("core:a");

            Assert.That(index.TryGetKey(id, out var key), Is.True);
            Assert.That(key, Is.EqualTo("core:a"));
        }

        [Test]
        public void Remove_FreesId_AndItCanBeReused()
        {
            var index = new KeyIndex("test");
            var id = index.Intern("core:a");

            Assert.That(index.Remove("core:a"), Is.True);
            Assert.That(index.TryGet("core:a", out _), Is.False);
            Assert.That(index.Intern("core:b"), Is.EqualTo(id));
        }

        [Test]
        public void MaxValue_RejectsBeyondLimit()
        {
            var index = new KeyIndex("test", new KeyIndexOptions(maxValue: 1));

            Assert.That(index.Intern("a"), Is.EqualTo(1));
            Assert.That(index.TryIntern("b", out _, out var error, out _), Is.False);
            Assert.That(error, Is.EqualTo(KeyIndexError.IdOutOfRange));
        }

        [Test]
        public void RequireNamespace_RejectsLegacyKey()
        {
            var index = new KeyIndex("test", new KeyIndexOptions(requireNamespace: true));

            Assert.That(index.TryIntern("legacy", out _, out var error, out _), Is.False);
            Assert.That(error, Is.EqualTo(KeyIndexError.MissingNamespace));
        }

        [Test]
        public void RequireNamespace_RejectsUnregisteredNamespace()
        {
            var authority = new FakeAuthority("core");
            var index = new KeyIndex("test", new KeyIndexOptions(requireNamespace: true), authority);

            Assert.That(index.Intern("core:a"), Is.EqualTo(1));
            Assert.That(index.TryIntern("other:a", out _, out var error, out _), Is.False);
            Assert.That(error, Is.EqualTo(KeyIndexError.NamespaceNotRegistered));
        }

        [Test]
        public void InvalidKey_ReturnsFalse()
        {
            var index = new KeyIndex("test");

            Assert.That(index.TryIntern("   ", out _, out var error, out _), Is.False);
            Assert.That(error, Is.EqualTo(KeyIndexError.InvalidKey));
        }

        [Test]
        public void ParseNamespaceDisabled_AcceptsKeysWithMultipleColons()
        {
            var index = new KeyIndex("test", new KeyIndexOptions(parseNamespace: false));

            var id = index.Intern("a:b:c");

            Assert.That(id, Is.GreaterThan(0));
            Assert.That(index.TryGetKey(id, out var key), Is.True);
            Assert.That(key, Is.EqualTo("a:b:c"));
        }

        [Test]
        public void RequireNamespace_WithoutAuthority_IsAllowedAndCounted()
        {
            var index = new KeyIndex("test", new KeyIndexOptions(requireNamespace: true));

            Assert.That(index.Intern("core:a"), Is.EqualTo(1));
            Assert.That(index.GetDiagnostics().AuthorityUnavailableCount, Is.EqualTo(1),
                "authority 缺失时应放行但计数，便于发现未真正校验的索引");
        }

        [Test]
        public void NamespaceRejection_IsCounted()
        {
            var authority = new FakeAuthority("core");
            var index = new KeyIndex("test", new KeyIndexOptions(requireNamespace: true), authority);

            Assert.That(index.TryIntern("other:a", out _, out _, out _), Is.False);
            Assert.That(index.GetDiagnostics().NamespaceRejectedCount, Is.EqualTo(1));
        }

        [Test]
        public void Options_AreExposedForRegistryConsistencyChecks()
        {
            var index = new KeyIndex("test", new KeyIndexOptions(parseNamespace: false));

            Assert.That(index.Options.ParseNamespace, Is.False);
            Assert.That(index.Options.RequireNamespace, Is.False);
        }

        [Test]
        public void TryRegister_BindsExplicitId()
        {
            var index = new KeyIndex("test");

            Assert.That(index.TryRegister("core:a", 42, out _, out _), Is.True);
            Assert.That(index.IsAlive(42), Is.True);
            Assert.That(index.TryGetKey(42, out var key), Is.True);
            Assert.That(key, Is.EqualTo("core:a"));
            Assert.That(index.Capacity, Is.GreaterThanOrEqualTo(42));
        }

        [Test]
        public void TryRegister_ConflictsAreRejected()
        {
            var index = new KeyIndex("test");
            index.TryRegister("core:a", 10, out _, out _);

            Assert.That(index.TryRegister("core:a", 11, out var rebind, out _), Is.False);
            Assert.That(rebind, Is.EqualTo(KeyIndexError.IdConflict));

            Assert.That(index.TryRegister("core:b", 10, out var duplicateId, out _), Is.False);
            Assert.That(duplicateId, Is.EqualTo(KeyIndexError.IdConflict));

            Assert.That(index.TryRegister("core:a", 10, out _, out _), Is.True,
                "同 key 同 id 应幂等成功");
        }

        [Test]
        public void Intern_SkipsExplicitlyRegisteredIds()
        {
            var index = new KeyIndex("test");
            index.TryRegister("core:reserved", 1, out _, out _);

            Assert.That(index.Intern("core:auto"), Is.EqualTo(2),
                "自动发号必须跳过显式注册占用的 1");
        }

        private sealed class FakeAuthority : IIdNamespaceAuthority
        {
            private readonly HashSet<string> mNames;

            public FakeAuthority(params string[] names)
            {
                mNames = new HashSet<string>(names);
            }

            public bool IsRegistered(string namespaceId)
            {
                return mNames.Contains(namespaceId);
            }
        }
    }
}
