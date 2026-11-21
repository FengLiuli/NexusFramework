using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NexusFramework.DataCarrier;
using NexusFramework.Identity;
using NUnit.Framework;

namespace NexusFramework.Core.Tests
{
    /// <summary>
    ///     普通 Trait：有字段、无自定义构造函数。
    /// </summary>
    [Serializable]
    public class SerializationTestTrait : DataTrait
    {
        public int value;
        public string label;
    }

    /// <summary>
    ///     第二个 Trait，用于验证多 Trait 恢复。
    /// </summary>
    [Serializable]
    public class SerializationTestFlagTrait : DataTrait
    {
        public bool flag;
    }

    /// <summary>
    ///     只有带参构造的 Trait，用于验证未初始化对象回退路径。
    /// </summary>
    [Serializable]
    public class NoDefaultCtorTrait : DataTrait
    {
        public int value;

        public NoDefaultCtorTrait(int value)
        {
            this.value = value;
        }
    }

    /// <summary>
    ///     带运行时 id 引用的 Trait：存档必须只写稳定 key，读档按 key 重建 id。
    /// </summary>
    [Serializable]
    public class IdRefTestTrait : DataTrait, IIdRefTrait
    {
        public const string Domain = "test.definitions";

        public int definitionId;
        public int count;

        public void CaptureIdRefs(List<IdRef> refs)
        {
            refs.Add(new IdRef { Domain = Domain, Id = definitionId });
            definitionId = 0;
        }

        public void ApplyIdRefs(IReadOnlyList<IdRef> refs)
        {
            definitionId = refs.Count > 0 ? refs[0].Id : 0;
        }
    }

    /// <summary>
    ///     Trait 存档往返的回归测试（core backlog A1）。
    ///     覆盖：AQN 解析、旧存档短名解析、无参构造缺失回退、未知类型隔离。
    /// </summary>
    [TestFixture]
    public class CarrierTraitSerializationTests
    {
        private CoreTestArchitecture arch;
        private CarrierManager manager;

        [SetUp]
        public void SetUp()
        {
            TraitTypeRegistry.Clear();
            arch = new CoreTestArchitecture();
            arch.Initialize();
            manager = (CarrierManager)arch.GetCarrierManager();
            manager.RegisterType("Test");
        }

        [TearDown]
        public void TearDown()
        {
            arch?.Dispose();
        }

        [Test]
        public void SerializeAndDeserialize_RestoresTraitValues()
        {
            var id = manager.CreateCarrier("Test");
            manager.AddTrait(id, new SerializationTestTrait { value = 42, label = "abc" });

            var json = manager.SerializeCarrier(id);
            Assert.IsNotNull(json);

            manager.DestroyCarrier(id);
            var restored = manager.DeserializeCarrier(json);

            Assert.IsTrue(restored > 0, "载体应当被重建");
            var trait = manager.GetTrait<SerializationTestTrait>(restored);
            Assert.IsNotNull(trait, "Trait 应当被恢复");
            Assert.AreEqual(42, trait.value);
            Assert.AreEqual("abc", trait.label);
        }

        [Test]
        public void SerializeAndDeserialize_RestoresMultipleTraits()
        {
            var id = manager.CreateCarrier("Test");
            manager.AddTrait(id, new SerializationTestTrait { value = 7, label = "x" });
            manager.AddTrait(id, new SerializationTestFlagTrait { flag = true });

            var json = manager.SerializeCarrier(id);
            manager.DestroyCarrier(id);
            var restored = manager.DeserializeCarrier(json);

            var valueTrait = manager.GetTrait<SerializationTestTrait>(restored);
            var flagTrait = manager.GetTrait<SerializationTestFlagTrait>(restored);
            Assert.IsNotNull(valueTrait);
            Assert.IsNotNull(flagTrait);
            Assert.AreEqual(7, valueTrait.value);
            Assert.IsTrue(flagTrait.flag);
        }

        [Test]
        public void Deserialize_LegacyShortName_ResolvesViaAssemblyScan()
        {
            var id = manager.CreateCarrier("Test");
            manager.AddTrait(id, new SerializationTestTrait { value = 11 });
            var json = manager.SerializeCarrier(id);

            // 模拟旧存档：没有程序集限定名，只有短名
            json = StripAssemblyQualifiedName(json);
            manager.DestroyCarrier(id);
            TraitTypeRegistry.Clear(); // 强制走惰性扫描路径

            var restored = manager.DeserializeCarrier(json);
            var trait = manager.GetTrait<SerializationTestTrait>(restored);

            Assert.IsNotNull(trait, "短名应能经程序集扫描解析");
            Assert.AreEqual(11, trait.value);
        }

        [Test]
        public void Deserialize_TraitWithoutParameterlessCtor_FallsBackToUninitializedObject()
        {
            var id = manager.CreateCarrier("Test");
            manager.AddTrait(id, new NoDefaultCtorTrait(99));

            var json = manager.SerializeCarrier(id);
            manager.DestroyCarrier(id);
            var restored = manager.DeserializeCarrier(json);

            Assert.IsTrue(restored > 0);
            var trait = manager.GetTrait<NoDefaultCtorTrait>(restored);
            Assert.IsNotNull(trait, "缺少无参构造时应回退到未初始化对象");
            Assert.AreEqual(99, trait.value);
        }

        [Test]
        public void Deserialize_UnknownTraitType_KeepsCarrierAndSkipsTrait()
        {
            var id = manager.CreateCarrier("Test");
            manager.AddTrait(id, new SerializationTestTrait { value = 1 });
            var json = manager.SerializeCarrier(id);

            json = StripAssemblyQualifiedName(json);
            json = ReplaceTypeName(json, "NoSuchTraitType");
            manager.DestroyCarrier(id);

            var restored = manager.DeserializeCarrier(json);

            Assert.IsTrue(restored > 0, "未知 Trait 不应导致载体丢失");
            Assert.IsNull(manager.GetTrait<SerializationTestTrait>(restored));
        }

        [Test]
        public void SerializeAndDeserialize_CarrierWithoutTraits()
        {
            var id = manager.CreateCarrier("Test");
            var json = manager.SerializeCarrier(id);
            manager.DestroyCarrier(id);

            var restored = manager.DeserializeCarrier(json);

            Assert.IsTrue(restored > 0, "无 Trait 的载体也应能往返");
            Assert.AreEqual(0, manager.GetAllTraits(restored).Count());
        }

        [Test]
        public void Deserialize_LegacySaveWithCarrierIdOnly_IsRejected()
        {
            // 旧存档只有 CarrierId 数字，没有 typeKey：必须显式拒绝，不得按旧数字重建。
            const string legacy = "{\"carrierId\":123,\"traits\":[]}";

            var restored = manager.DeserializeCarrier(legacy);

            Assert.Zero(restored, "缺少 typeKey 的旧存档必须被拒绝");
        }

        [Test]
        public void SerializeAndDeserialize_IdRefTrait_WritesKeyNotRuntimeId()
        {
            var registry = new IdRegistry();
            var definitions = registry.GetOrCreateIndex(IdRefTestTrait.Domain,
                new KeyIndexOptions(parseNamespace: false));
            var potionId = definitions.Intern("core:item.potion");

            var source = new CarrierManager(registry);
            source.RegisterType("Test");
            var id = source.CreateCarrier("Test");
            source.AddTrait(id, new IdRefTestTrait { definitionId = potionId, count = 3 });

            var json = source.SerializeCarrier(id);

            Assert.IsNotNull(json);
            Assert.That(json, Does.Contain("idRefs"));
            Assert.That(json, Does.Contain("core:item.potion"), "存档应写稳定 key");
            Assert.That(source.GetTrait<IdRefTestTrait>(id).definitionId, Is.EqualTo(potionId),
                "序列化后应恢复原对象");

            // 新架构：先注册另一个 key，让 potion 拿到不同的运行时 id。
            var registry2 = new IdRegistry();
            var definitions2 = registry2.GetOrCreateIndex(IdRefTestTrait.Domain,
                new KeyIndexOptions(parseNamespace: false));
            definitions2.Intern("core:item.other");
            var expectedId = definitions2.Intern("core:item.potion");

            var target = new CarrierManager(registry2);
            target.RegisterType("Test");
            var restored = target.DeserializeCarrier(json);

            Assert.That(restored, Is.GreaterThan(0));
            var trait = target.GetTrait<IdRefTestTrait>(restored);
            Assert.IsNotNull(trait);
            Assert.That(trait.definitionId, Is.EqualTo(expectedId), "读档应按 key 重建新 id");
            Assert.That(trait.count, Is.EqualTo(3), "普通字段仍应恢复");

            source.Dispose();
            target.Dispose();
        }

        private static string StripAssemblyQualifiedName(string json)
        {
            return Regex.Replace(json, "\"assemblyQualifiedName\":\"[^\"]*\"",
                "\"assemblyQualifiedName\":\"\"");
        }

        private static string ReplaceTypeName(string json, string newName)
        {
            return Regex.Replace(json, "\"typeName\":\"[^\"]*\"", $"\"typeName\":\"{newName}\"");
        }
    }
}
