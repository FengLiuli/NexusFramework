using NexusFramework;
using NexusFramework.GAS.Config;
using NexusFramework.GAS.ECS;
using NexusFramework.GAS.Models;
using NexusFramework.GAS.Services;
using NUnit.Framework;

namespace NexusFramework.GAS.Tests.Config
{
    [TestFixture]
    public class ConfigWorkflowTests
    {
        /// <summary>
        /// 完整工作流模拟：JSON → IConfigLoader.Parse → ConfigModel.Register → Service 查询
        /// </summary>
        [Test]
        public void FullWorkflow_JsonToModelToService()
        {
            var arch = new TestArchitecture();
            arch.Initialize();
            arch.GetCarrierManager().RegisterType("TestUnit");

            var model = arch.GetModel<ConfigModel>();
            // 使用 JsonConfigLoader 验证 JSON 解析链路
            var loader = new JsonConfigLoader();

            // 模拟 gen.bat 产出的 JSON 标签层级数据
            var tagJson = "{\"Code\":0,\"Name\":\"Root\",\"Children\":[10,20]}";
            var tagData = loader.ParseTagHierarchy(tagJson);
            model.RegisterTagHierarchy(tagData);
            Assert.That(model.GetTagHierarchy().Tags, Is.Not.Null, "标签层级应成功载入");

            // 验证 ConfigModel 已通过 TestArchitecture.OnInit 预填充了测试数据
            var geConfig = model.GetGameplayEffectConfig(2);
            Assert.That(geConfig, Is.Not.Null, "持续 GE 配置应已就绪");

            // 验证 Service 能通过 Model 查询到
            var source = arch.CreateGASCarrier("TestUnit");
            var target = arch.CreateGASCarrier("TestUnit");
            Assert.DoesNotThrow(() =>
            {
                arch.GetService<EffectService>().ApplyEffect(configId: 2, target: target, source: source);
            });

            arch.Dispose();
        }

        /// <summary>后加载的数据覆盖先加载的（Mod 覆盖模式）</summary>
        [Test]
        public void LateRegistered_Data_Overrides_Early()
        {
            var arch = new TestArchitecture();
            arch.Initialize();

            var model = arch.GetModel<ConfigModel>();

            // 基表已通过 OnInit 预填 configId=1（瞬时 GE）
            var baseConfig = model.GetGameplayEffectConfig(1);
            Assert.That(baseConfig, Is.Not.Null);
            Assert.That(baseConfig.Length, Is.EqualTo(1), "base 包 configId=1 有 1 个组件");

            // Mod 在运行时动态注册，覆盖 configId=1
            model.RegisterEffect(1, null);
            Assert.That(model.GetGameplayEffectConfig(1), Is.Null, "Mod 覆盖为 null 后查询应返回 null");

            arch.Dispose();
        }

        /// <summary>未知配置 ID 返回 null，不抛异常</summary>
        [Test]
        public void UnknownConfigId_ReturnsNull()
        {
            var arch = new TestArchitecture();
            arch.Initialize();

            var model = arch.GetModel<ConfigModel>();
            Assert.That(model.GetGameplayEffectConfig(99999), Is.Null);
            Assert.That(model.GetAbilityConfig(99999), Is.Null);
            Assert.That(model.GetGameplayCueConfig(99999).CueType, Is.Null);

            arch.Dispose();
        }
    }
}
