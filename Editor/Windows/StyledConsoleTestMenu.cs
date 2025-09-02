using UnityEngine;
using UnityEditor;
using BattleTurn.StyledLog;

namespace BattleTurn.StyledLog.Editor
{
    public static class StyledConsoleTestMenu
    {
        [MenuItem("Tools/StyledDebug/Test Console Integration")]
        public static void TestConsoleIntegration()
        {
            Debug.Log("=== Testing Styled Console Integration ===");
            
            // First, open the styled console window
            StyledConsoleWindow.Open();
            
            // Test basic StyledDebug logging
            StyledDebug.Log("test", "Test Log Message");
            StyledDebug.LogWarning("test", "Test Warning Message");
            StyledDebug.LogError("test", "Test Error Message");
            
            Debug.Log("=== Test Complete - Check Styled Console Window ===");
        }

        [MenuItem("Tools/StyledDebug/Test Regular Unity Logs")]
        public static void TestRegularLogs()
        {
            Debug.Log("Regular Unity Log");
            Debug.LogWarning("Regular Unity Warning");
            Debug.LogError("Regular Unity Error");
        }
    }
}
