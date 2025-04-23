using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEngine.UI;
using System;

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

                EditorGUILayout.LabelField("TEST CASES", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("--Unity Engine Log--", EditorStyles.miniLabel);
                if (GUILayout.Button("Test Call Original UnityEngine Log"))
                {
                    DebugBeforeRun(out float timeStart, out long beforeMemory);
                    UnityEngine.Debug.Log("<color=#8B5F5F60> Test Call</color> <color=#ff0000ff> Test Call</color> <color=#00ff00> Test Call</color>");
                    DebugAfterRun(timeStart, beforeMemory);
                }

                EditorGUILayout.LabelField("--MULTI COLOR--", EditorStyles.miniLabel);

                if (GUILayout.Button("Test Call Multicolor"))
                {
                    Debug.LogMultiColor("[#F37D7D60: Test Call] asdwf [#ff0000ff: Test Call] [#00ff00: Test Call] [#0000ff: Test Call] [#ffff00: Test Call] [#ff00ff: Test Call] [#00ffff: Test Call]");
                }

                if (GUILayout.Button("Test Warning Call Multicolor"))
                {
                    Debug.LogWarningMultiColor("[#ffffff: Test Call] [#ff0000: Test Call] [#00ff00: Test Call] [#0000ff: Test Call] [#ffff00: Test Call] [#ff00ff: Test Call] [#00ffff: Test Call]");
                }

                if (GUILayout.Button("Test Error Call Multicolor"))
                {
                    Debug.LogErrorMultiColor("[#ffffff: Test Call] [#ff0000: Test Call] [#00ff00: Test Call] [#0000ff: Test Call] [#ffff00: Test Call] [#ff00ff: Test Call] [#00ffff: Test Call]");
                }

                if (GUILayout.Button("Test $ Call Multicolor"))
                {
                    int adaw = 1;
                    int adaw2 = 2;
                    Debug.LogMultiColor($"[#ffffff: Test Call {adaw}] [#ff0000: Test Call {adaw2}] [#00ff00: Test Call] [#0000ff: Test Call] [#ffff00: Test Call] [#ff00ff: Test Call] [#00ffff: Test Call]");
                }

                if (GUILayout.Button("Test Format Call Multicolor"))
                {
                    int adaw = 1;
                    int adaw2 = 2;
                    Debug.LogMultiColor(string.Format("[#ffffff: Test Call {0}] [#ff0000: Test Call {1}]  [#00ff00: Test Call] [#0000ff: Test Call] [#ffff00: Test Call] [#ff00ff: Test Call] [#00ffff: Test Call]", adaw, adaw2));
                }

                EditorGUILayout.LabelField("--SINGLE COLOR--", EditorStyles.miniLabel);
                if (GUILayout.Button("Test Call Hex Single Color"))
                {
                    Debug.Log("Hi motherfucker", "#91FF83FF");
                }

                if (GUILayout.Button("Test Call Error Hex Single Color"))
                {
                    Debug.LogError("Hi motherfucker");
                }

                if (GUILayout.Button("Test Call Warning Hex Single Color"))
                {
                    Debug.LogWarning("Hi motherfucker");
                }

                if (GUILayout.Button("Test $ Call Hex Single Color"))
                {
                    int adaw = 1;
                    int adaw2 = 2;
                    Debug.Log($"Hi motherfucker {adaw}, {adaw2}", "#83FFF5FF");
                }

                if (GUILayout.Button("Test Format Call Hex Single Color"))
                {
                    int adaw = 1;
                    int adaw2 = 2;
                    Debug.Log(string.Format("Hi motherfucker {0}, {1}", adaw, adaw2), "#C783FFFF");
                }

                if (GUILayout.Button("Test Call Color Single Color"))
                {
                    Debug.Log("Hi motherfucker", new Color(0.000f, 1.000f, 0.533f, 1.000f));
                }

                if (GUILayout.Button("Test $ Call Color Single Color"))
                {
                    int adaw = 1;
                    int adaw2 = 2;
                    Debug.Log($"Hi motherfucker {adaw}, {adaw2}", Color.cyan);
                }

                if (GUILayout.Button("Test Format Call Color Single Color"))
                {
                    int adaw = 1;
                    int adaw2 = 2;
                    Debug.Log(string.Format("Hi motherfucker {0}, {1}", adaw, adaw2), Color.magenta);
                }
            }
        }
        
        [InitializeOnLoadMethod]
        private static void OnEditorStart()
        {
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnEditorQuitting()
        {
            // Unsubscribe from events when editor is quitting
            EditorApplication.delayCall -= CheckAndCreateSettingsAsset;
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
                Debug.Log("Created ColorfulLog settings asset at " + fullAssetPath);
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
                    .Cast<UnityEngine.Object>()
                    .ToArray();
            }
            return true;
        }

        private void DebugBeforeRun(out float timeStart, out long beforeMemory)
        {
            timeStart = 0;
            beforeMemory = 0;
            if (Setting.IsTestingDebugMode)
            {
                beforeMemory = GC.GetTotalMemory(false);
                timeStart = Time.realtimeSinceStartup;
            }
        }

        private void DebugAfterRun(float timeStart, long beforeMemory)
        {
            if (Setting.IsTestingDebugMode)
            {
                float timeEnd = Time.realtimeSinceStartup;
                float timeElapsed = timeEnd - timeStart;


                long afterMemory = GC.GetTotalMemory(false);
                long memoryUsed = afterMemory - beforeMemory;

                // Convert bytes to KB for more readable output
                float memoryUsedKB = memoryUsed / 1024f;
                UnityEngine.Debug.Log($"Memory used: {memoryUsedKB:F2} KB");
                UnityEngine.Debug.Log($"Time taken process: {timeElapsed} seconds");
            }
        }
    }
}