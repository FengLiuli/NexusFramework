using System.IO;
using NexusFramework.GAS.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NexusFramework.GAS.Tests.Editor
{
    [TestFixture]
    public class EditorWorkflowTests
    {
        private const string PROJ_ROOT = "Assets/Settings/GASSetting.asset";

        /// <summary>LoadOrCreate 返回有效资产</summary>
        [Test]
        public void SettingAsset_LoadOrCreate_ReturnsValid()
        {
            var asset = GASSettingAsset.LoadOrCreate();
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.TableOutputPath, Is.Not.Null.And.Not.Empty);
            Assert.That(asset.ConfigProjectPath, Is.Not.Null.And.Not.Empty);

            // 验证路径非空即可，用户可自定义路径
            Assert.That(asset.TableOutputPath, Is.Not.Null.And.Not.Empty);
            Assert.That(asset.ConfigProjectPath, Is.Not.Null.And.Not.Empty);
        }

        /// <summary>Settings 窗口可正常创建和关闭</summary>
        [Test]
        public void SettingsWindow_Opens_WithoutError()
        {
            var window = EditorWindow.GetWindow<GASSettingsWindow>(false, "Test");
            Assert.That(window, Is.Not.Null);
            window.Close();
        }
    }
}
