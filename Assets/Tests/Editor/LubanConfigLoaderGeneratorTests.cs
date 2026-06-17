// using System.IO;
// using System.Reflection;
// using NexusFramework.GAS.Config;
// using NexusFramework.GAS.Editor;
// using NUnit.Framework;
// using UnityEditor;
// using UnityEngine;
//
// namespace NexusFramework.GAS.Tests.Editor
// {
//     [TestFixture]
//     public class LubanConfigLoaderGeneratorTests
//     {
//         private const string GeneratedFilePath = "Assets/Demo/Emberheart/GAS/Generated/LubanConfigLoader.cs";
//
//         /// <summary>
//         /// 验证菜单项存在且可调用（通过反射检查 MenuItem 属性）
//         /// </summary>
//         [Test]
//         public void Generator_MenuItem_Exists()
//         {
//             var type = typeof(LubanConfigLoaderGenerator);
//             var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
//             bool hasMenuItem = false;
//             foreach (var m in methods)
//             {
//                 var attr = m.GetCustomAttribute<MenuItem>();
//                 if (attr != null && attr.menuItem == "NF.GAS/Generate/LubanConfigLoader")
//                 {
//                     hasMenuItem = true;
//                     break;
//                 }
//             }
//             Assert.That(hasMenuItem, Is.True, "菜单项 NF.GAS/Generate/LubanConfigLoader 应存在");
//         }
//
//         /// <summary>
//         /// 执行生成器不抛异常
//         /// </summary>
//         [Test]
//         public void Generator_Execute_DoesNotThrow()
//         {
//             Assert.DoesNotThrow(() => LubanConfigLoaderGenerator.Generate());
//         }
//
//         /// <summary>
//         /// 执行后输出文件存在且非空
//         /// </summary>
//         [Test]
//         public void Generator_OutputFile_ExistsAndNotEmpty()
//         {
//             LubanConfigLoaderGenerator.Generate();
//             var fullPath = Path.GetFullPath(GeneratedFilePath);
//             Assert.That(File.Exists(fullPath), Is.True, "生成文件应存在");
//             var content = File.ReadAllText(fullPath);
//             Assert.That(content, Is.Not.Empty, "生成文件不应为空");
//         }
//
//         /// <summary>
//         /// 生成的 LubanConfigLoader 可实例化
//         /// </summary>
//         [Test]
//         public void Generated_Loader_CanInstantiate()
//         {
//             var loader = new LubanConfigLoader();
//             Assert.That(loader, Is.Not.Null);
//             Assert.That(loader.Initialized, Is.False, "新实例 Initialized 应为 false");
//         }
//
//         /// <summary>
//         /// 不存在的 ID 返回 null 不抛异常
//         /// </summary>
//         [Test]
//         public void GetEffectConfig_UnknownId_ReturnsNull()
//         {
//             var result = LubanConfigLoader.GetEffectConfig(99999);
//             Assert.That(result, Is.Null);
//         }
//
//         /// <summary>
//         /// 不存在的 Ability ID 返回 null 不抛异常
//         /// </summary>
//         [Test]
//         public void GetAbilityConfig_UnknownId_ReturnsNull()
//         {
//             var result = LubanConfigLoader.GetAbilityConfig(99999);
//             Assert.That(result, Is.Null);
//         }
//
//         /// <summary>
//         /// 不存在的 Cue ID 返回 null 不抛异常
//         /// </summary>
//         [Test]
//         public void GetCueConfig_UnknownId_ReturnsDefault()
//         {
//             var result = LubanConfigLoader.GetCueConfig(99999);
//             Assert.That(result, Is.Null);
//         }
//
//         /// <summary>
//         /// 生成的代码中 LoadTablesForEditor 只依赖 string 参数，不依赖 GASSettingAsset
//         /// </summary>
//         [Test]
//         public void LoadTablesForEditor_Signature_NoAssetDependency()
//         {
//             var method = typeof(LubanConfigLoader).GetMethod(
//                 "LoadTablesForEditor",
//                 BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
//                 null,
//                 new[] { typeof(string) },
//                 null);
//             Assert.That(method, Is.Not.Null,
//                 "LoadTablesForEditor(string jsonDir) 应存在且只接受一个 string 参数");
//         }
//
//         /// <summary>
//         /// GetTagHierarchyData 不抛异常（编译期验证——只要编译通过就正确）
//         /// </summary>
//         [Test]
//         public void GetTagHierarchyData_DoesNotThrow()
//         {
//             Assert.DoesNotThrow(() => LubanConfigLoader.GetTagHierarchyData());
//         }
//     }
// }
