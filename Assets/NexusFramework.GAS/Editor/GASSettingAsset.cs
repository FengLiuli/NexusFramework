using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NexusFramework.GAS.Editor
{
    public class GASSettingAsset : ScriptableObject
    {
        private const string SETTING_PATH = "Assets/Settings/GASSetting.asset";

        public const string DEFAULT_TABLE_OUTPUT_PATH = "Assets/DataGenerated/Luban/Json/GAS";
        public const string DEFAULT_CONFIG_PROJECT_PATH = "Config/exgas_config";
        public const string DEFAULT_LUBAN_CONFIG_LOADER_PATH = "Assets/Demo/Emberheart/GAS/Generated/LubanConfigLoader.cs";

        [Header("表导出路径（Luban JSON 输出）")]
        public string TableOutputPath = DEFAULT_TABLE_OUTPUT_PATH;

        [Header("配置表工程路径（含 Datas/ 和 gen.bat）")]
        public string ConfigProjectPath = DEFAULT_CONFIG_PROJECT_PATH;

        [Header("LubanConfigLoader 输出路径")]
        public string LubanConfigLoaderOutputPath = DEFAULT_LUBAN_CONFIG_LOADER_PATH;

        public string GetLubanConfigLoaderOutputPath()
        {
            var projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
            return System.IO.Path.Combine(projectRoot, LubanConfigLoaderOutputPath);
        }

        public string FullGenBatPath()
        {
            var projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
            return Path.Combine(projectRoot, ConfigProjectPath, "gen.bat");
        }

        public void RunGenBat()
        {
            var bat = FullGenBatPath();
            if (!File.Exists(bat))
            {
                UnityEngine.Debug.LogError($"[NF.GAS] gen.bat not found: {bat}");
                return;
            }
            var projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
            var jsonOutputPath = Path.Combine(projectRoot, TableOutputPath);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = bat,
                    Arguments = $"\"{jsonOutputPath}\"",
                    WorkingDirectory = Path.GetDirectoryName(bat),
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            process.Start();
            process.WaitForExit();
            UnityEngine.Debug.Log($"[NF.GAS] Luban gen.bat exited with code: {process.ExitCode}");
            AssetDatabase.Refresh();
        }

        [MenuItem("NF.GAS/导出 Luban JSON 表")]
        private static void MenuGenJson()
        {
            LoadOrCreate().RunGenBat();
        }

        public static GASSettingAsset LoadOrCreate()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GASSettingAsset>(SETTING_PATH);
            if (asset != null) return asset;

            var dir = Path.GetDirectoryName(SETTING_PATH);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            asset = CreateInstance<GASSettingAsset>();
            AssetDatabase.CreateAsset(asset, SETTING_PATH);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
