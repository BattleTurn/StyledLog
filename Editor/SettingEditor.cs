using UnityEngine;
using UnityEditor;

namespace Colorful.ScriptableObjects.Editor
{
    // This part runs in the editor
    [InitializeOnLoad]
    [CustomEditor(typeof(Setting))]
    public sealed class SettingEditor : UnityEditor.Editor
    {
        const string SETTINGS_PATH = "Assets/Colorful/Resources";
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
                EditorGUILayout.LabelField("Instance Settings", EditorStyles.boldLabel);
                
                Setting newInstance = (Setting)EditorGUILayout.ObjectField("Current Instance", Setting.Instance, typeof(Setting), false);
                if (newInstance != Setting.Instance && newInstance != null)
                {
                    // Use reflection to set the private _instance field
                    var instanceField = typeof(Setting).GetField("_instance", 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Static);
                    
                    if (instanceField != null)
                    {
                        instanceField.SetValue(null, newInstance);
                        EditorUtility.SetDirty(newInstance);
                        AssetDatabase.SaveAssets();
                        Debug.Log($"Colorful Log instance changed to {AssetDatabase.GetAssetPath(newInstance)}");
                    }
                }
                
                if (Setting.Instance != settings)
                {
                    EditorGUILayout.HelpBox("This is not the active settings instance. Settings will only apply if this asset is in Resources folder.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("This is the active settings instance being used by Colorful Log.", MessageType.Info);
                }
                
                if (GUI.changed)
                {
                    EditorUtility.SetDirty(target);
                }
            }
        }

        private static void CheckAndCreateSettingsAsset()
        {
            const string fullAssetPath = SETTINGS_PATH + "/" + SETTINGS_ASSET_NAME;

            // Check if directory exists, if not create it
            if (!AssetDatabase.IsValidFolder("Assets/Colorful"))
            {
                AssetDatabase.CreateFolder("Assets", "Colorful");
            }

            if (!AssetDatabase.IsValidFolder(SETTINGS_PATH))
            {
                AssetDatabase.CreateFolder("Assets/Colorful", "Resources");
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

        private static bool CheckIfSettingExist(string fullAssetPath, out Setting settings)
        {
            settings = AssetDatabase.LoadAssetAtPath<Setting>(fullAssetPath);

            if (settings != null)
            {
                return false;
            }

            // If not found, search for it in the entire project
            string[] guids = AssetDatabase.FindAssets("t:Setting");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                settings = AssetDatabase.LoadAssetAtPath<Setting>(path);
                if (settings == null)
                {
                    continue; // Not a Setting asset, skip it
                }

                // Found the settings somewhere else
                if (!path.Contains("/Resources/"))
                {
                    Debug.LogWarning("ColorfulLog settings found at " + path + " but it should be in a Resources folder to work correctly.", Color.yellow);
                }
                else
                {
                    Debug.Log("ColorfulLog settings found at " + path, Color.green);
                }
                return true; // Settings found, no need to create
            }

            return false; // Settings not found anywhere
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