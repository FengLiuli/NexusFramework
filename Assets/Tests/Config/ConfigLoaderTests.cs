using NexusFramework.GAS.Config;
using NexusFramework.GAS.Models;
using NUnit.Framework;

namespace NexusFramework.GAS.Tests.Config
{
    [TestFixture]
    public class ConfigLoaderTests
    {
        /// <summary>JsonConfigLoader 可加载文件</summary>
        [Test]
        public void JsonLoader_LoadRaw_ReturnsNullForMissing()
        {
            var loader = new JsonConfigLoader();
            Assert.That(loader.LoadRaw("__nonexistent__.json"), Is.Null);
        }

        /// <summary>IConfigLoader 接口——所有方法可调用不抛异常</summary>
        [Test]
        public void IConfigLoader_All_Methods_Accessible()
        {
            IConfigLoader loader = new JsonConfigLoader();
            Assert.DoesNotThrow(() => loader.LoadRaw("x"));
            Assert.DoesNotThrow(() => loader.ParseGameplayEffect("{}"));
            Assert.DoesNotThrow(() => loader.ParseAbility("{}"));
            Assert.DoesNotThrow(() => loader.ParseTagHierarchy("{}"));
        }

        /// <summary>ConfigModel 注册和查询</summary>
        [Test]
        public void ConfigModel_RegisterAndQuery_Works()
        {
            var model = new ConfigModel();
            MockConfigLoader.Populate(model);

            Assert.That(model.GetGameplayEffectConfig(1), Is.Not.Null, "configId=1 应有配置");
            Assert.That(model.GetGameplayEffectConfig(999), Is.Null);
            Assert.That(model.GetAbilityConfig(1), Is.Not.Null, "abilityCode=1 应有配置");
            Assert.That(model.GetAbilityConfig(999), Is.Null);
        }

        /// <summary>后注册覆盖先注册</summary>
        [Test]
        public void ConfigModel_ReRegister_Overrides()
        {
            var model = new ConfigModel();
            MockConfigLoader.Populate(model);

            // 重新注册 configId=1 为空
            model.RegisterEffect(1, null);
            Assert.That(model.GetGameplayEffectConfig(1), Is.Null, "重注册 null 应覆盖原有配置");
        }
    }
}
