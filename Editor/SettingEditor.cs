using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Colorful.ScriptableObjects.Editor
{
    // This part runs in the editor
    [InitializeOnLoad]
    [CustomEditor(typeof(Setting))]
    public sealed class SettingEditor : UnityEditor.Editor
    {
        const string SETTINGS_PATH = "Assets/Colorful";
        const string SETTING_CONTAINER_PATH = "ScriptableObjects";
        const string SETTINGS_ASSET_NAME = "ColorfulLogSettings.asset";

        static SettingEditor()
        {
            EditorApplication.delayCall += CheckAndCreateSettingsAsset;
        }

        public override void OnInspectorGUI()
        {
            // This code runs when the inspector is drawn
            base.OnInspectorGUI();

            Setting settings = target as Setting;
            if (settings != null)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("DEBUG", EditorStyles.boldLabel);

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("Instance", Setting.Instance, typeof(Setting), false);
                EditorGUI.EndDisabledGroup();
                
                if (Setting.Instance != settings)
                {
                    EditorGUILayout.HelpBox("There are more than one Colorful Log setting", MessageType.Warning);
                }
                
                if (GUI.changed)
                {
                    EditorUtility.SetDirty(target);
                }
            }
        }

        private static void CheckAndCreateSettingsAsset()
        {
            const string fullAssetPath = SETTINGS_PATH + "/" + SETTING_CONTAINER_PATH + "/" + SETTINGS_ASSET_NAME;

            // Check if directory exists, if not create it
            if (!AssetDatabase.IsValidFolder(SETTINGS_PATH))
            {
                AssetDatabase.CreateFolder("Assets", "Colorful");
            }

            if (!AssetDatabase.IsValidFolder(SETTINGS_PATH + "/" + SETTING_CONTAINER_PATH))
            {
                AssetDatabase.CreateFolder(SETTINGS_PATH, SETTING_CONTAINER_PATH);
            }

            // Check if settings asset exists
            // First try to load from our specified path
            if (CheckIfSettingExist(fullAssetPath, out Setting settings))
            {
                return;
            }

            if (settings == null)
            {
                // Create settings asset
                var settingsAsset = CreateInstance<Setting>();
                AssetDatabase.CreateAsset(settingsAsset, fullAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.LogHex("Created ColorfulLog settings asset at " + fullAssetPath);
            }
        }

        private static bool CheckIfSettingExist(string fullAssetPath, out Setting setting)
        {
            setting = AssetDatabase.LoadAssetAtPath<Setting>(fullAssetPath);

            if (setting != null)
            {
                return false;
            }

            CheckIfManySettings(out setting);
            return setting != null ? true : false;
        }

        private static bool CheckIfManySettings(out Setting setting)
        {
            // Found the settings somewhere else
            // Check for multiple settings assets in the project
            string[] settingGuids = AssetDatabase.FindAssets("t:Setting");

            setting = null;
            string instancedSettingPath = AssetDatabase.GetAssetPath(Setting.Instance);

            foreach (string settingGuid in settingGuids)
            {
                string settingPath = AssetDatabase.GUIDToAssetPath(settingGuid);
                if (settingPath == instancedSettingPath)
                {
                    setting = AssetDatabase.LoadAssetAtPath<Setting>(settingPath);
                    break;
                }
            }

            if (settingGuids.Length <= 1)
            {
                Debug.Log("ColorfulLog settings found at " + instancedSettingPath, Color.green);
                return false;
            }

            EditorGUILayout.HelpBox($"Found {settingGuids.Length} Setting assets in the project. " +
                    "Multiple settings may cause unexpected behavior. " +
                    $"Only the one in {instancedSettingPath} folder will be used.", MessageType.Warning);

            if (GUILayout.Button("Show All Settings In Project"))
            {
                // Find and select all settings assets
                Selection.objects = settingGuids
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<Setting>)
                    .Where(s => s != null)
                    .Cast<Object>()
                    .ToArray();
            }
            return true;
        }

        private void OnEnable()
        {
            // This code runs when the editor starts or scripts are recompiled
            Debug.LogHex("UnitTest initialized in editor. This runs whenever the editor starts or scripts are recompiled.");
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            // This code runs every editor frame
        }
    }
}