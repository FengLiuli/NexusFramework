using System.IO;
using UnityEditor;
using UnityEngine;

namespace NexusFramework.GAS.Editor
{
    public class GASSettingsWindow : EditorWindow
    {
        private GASSettingAsset _setting;

        [MenuItem("NF.GAS/Settings")]
        private static void Open()
        {
            var window = GetWindow<GASSettingsWindow>(false, "NF.GAS Settings");
            window.minSize = new Vector2(400, 140);
            window.Show();
        }

        private void OnEnable()
        {
            _setting = GASSettingAsset.LoadOrCreate();
        }

        private void OnGUI()
        {
            if (_setting == null) return;

            var serialized = new SerializedObject(_setting);

            EditorGUILayout.LabelField("GAS 路径配置", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.PropertyField(serialized.FindProperty("TableOutputPath"));
            EditorGUILayout.PropertyField(serialized.FindProperty("ConfigProjectPath"));

            serialized.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                $"gen.bat: {_setting.FullGenBatPath()}",
                File.Exists(_setting.FullGenBatPath()) ? MessageType.Info : MessageType.Warning);

            if (GUILayout.Button("导出 Luban JSON 表", GUILayout.Height(30)))
            {
                _setting.RunGenBat();
            }
        }
    }
}
