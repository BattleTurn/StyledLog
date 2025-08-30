// StyledLogInitializeEditor.cs
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace BattleTurn.StyledLog.Editor
{
    [InitializeOnLoad]
    public static class StyledLogInitializeEditor
    {
        private const string LOG_MANAGER_NAME = "StyledLogManager.asset";
        private const string LOG_MANAGER_FOLDER = "Assets/Plugins/StyledLog/Resources";
        private const string LOG_MANAGER_PATH = LOG_MANAGER_FOLDER + "/" + LOG_MANAGER_NAME;

        static StyledLogInitializeEditor()
        {
            EditorApplication.delayCall += EnsureManagerExists;
        }

        private static void EnsureManagerExists()
        {
            var manager = Resources.Load<StyledLogManager>($"{nameof(StyledLogManager)}");
            if (manager != null)
            {
                StyledDebug.StyledLogManager = manager;
                return;
            }

            if (!Directory.Exists(LOG_MANAGER_FOLDER))
            {
                Directory.CreateDirectory(LOG_MANAGER_FOLDER);
            }

            var newManager = ScriptableObject.CreateInstance<StyledLogManager>();
            AssetDatabase.CreateAsset(newManager, LOG_MANAGER_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            StyledDebug.StyledLogManager = newManager;

            Debug.Log($"Created new {nameof(StyledLogManager)} at {LOG_MANAGER_PATH}");
        }

        [MenuItem("Tools/StyledDebug/Create or Load Log Manager")]
        public static void CreateOrLoadLogManager()
        {
            var manager = Resources.Load<StyledLogManager>($"{nameof(StyledLogManager)}");
            if (manager != null)
            {
                Debug.Log($"{nameof(StyledLogManager)} already exists and was loaded.");
                Selection.activeObject = manager;
                return;
            }

            if (!Directory.Exists(LOG_MANAGER_FOLDER))
            {
                Directory.CreateDirectory(LOG_MANAGER_FOLDER);
            }

            var newManager = ScriptableObject.CreateInstance<StyledLogManager>();
            AssetDatabase.CreateAsset(newManager, LOG_MANAGER_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created new {nameof(StyledLogManager)} at {LOG_MANAGER_PATH}");
            Selection.activeObject = newManager;
        }
    }
}
