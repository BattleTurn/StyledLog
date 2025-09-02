using UnityEngine;
using UnityEditor;
using BattleTurn.StyledLog;

namespace BattleTurn.StyledLog.Editor
{
    public class StyledConsoleDebugger : EditorWindow
    {
        [MenuItem("Tools/StyledDebug/Debug Console Issues")]
        public static void Open()
        {
            GetWindow<StyledConsoleDebugger>("Console Debugger").Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Styled Console Debugger", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Test buttons first - easier to debug
            if (GUILayout.Button("Test StyledDebug.Log"))
            {
                Debug.Log("=== Testing StyledDebug.Log ===");
                StyledDebug.Log("test", "This is a test log message");
            }

            if (GUILayout.Button("Test StyledDebug.LogWarning"))
            {
                Debug.Log("=== Testing StyledDebug.LogWarning ===");
                StyledDebug.LogWarning("test", "This is a test warning message");
            }

            if (GUILayout.Button("Test StyledDebug.LogError"))
            {
                Debug.Log("=== Testing StyledDebug.LogError ===");
                StyledDebug.LogError("test", "This is a test error message");
            }

            if (GUILayout.Button("Test Regular Debug.Log"))
            {
                Debug.Log("Regular Unity Debug.Log message");
            }

            EditorGUILayout.Space();

            // Check event subscribers
            EditorGUILayout.LabelField($"onEmit has subscribers: {StyledDebug.HasOnEmitSubscribers}");
            EditorGUILayout.LabelField($"Number of subscribers: {StyledDebug.OnEmitSubscriberCount}");

            EditorGUILayout.Space();

            // Check StyledLogManager
            var manager = StyledDebug.StyledLogManager;
            EditorGUILayout.LabelField($"StyledLogManager found: {manager != null}");
            if (manager != null)
            {
                var testStyle = manager["test"];
                EditorGUILayout.LabelField($"'test' tag style: {testStyle != null}");
                if (testStyle != null)
                {
                    EditorGUILayout.LabelField($"  - Enabled: {testStyle.Enabled}");
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Open Styled Console"))
            {
                StyledConsoleWindow.Open();
            }
        }
    }
}
