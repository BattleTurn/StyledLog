using UnityEditor;
using UnityEngine;

namespace BattleTurn.StyledLog.Editor
{
    [InitializeOnLoad]
    internal static class StyledConsoleHooks
    {
        static StyledConsoleHooks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            // Hook Unity's Debug log and mirror to Styled Console
            Application.logMessageReceived -= OnUnityLog;
            Application.logMessageReceived += OnUnityLog;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            StyledConsoleController.EnsurePrefsLoaded();
            if (state == PlayModeStateChange.EnteredPlayMode && StyledConsoleController.ClearOnPlay)
            {
                StyledConsoleController.ClearAllStorage();
            }
        }

        private static void OnBeforeAssemblyReload()
        {
            StyledConsoleController.EnsurePrefsLoaded();
            if (StyledConsoleController.ClearOnRecompile)
            {
                StyledConsoleController.ClearAllStorage();
            }
            else
            {
                // persist snapshot so logs survive domain reload
                StyledConsoleController.SaveSnapshot();
            }
        }

        private static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            // Avoid duplicating StyledDebug entries (they already emit via onEmit)
            if (!string.IsNullOrEmpty(stackTrace) &&
                (stackTrace.Contains("BattleTurn.StyledLog.StyledDebug") || stackTrace.Contains("StyledDebug.LogInternal")))
            {
                return;
            }

            // Tag Unity-originated logs as "Unity"
            StyledConsoleController.AddLog("Unity", condition ?? string.Empty, type, stackTrace ?? string.Empty);

            // Repaint any open StyledConsoleWindow so new entries are visible immediately
            EditorApplication.delayCall += RepaintAllStyledWindows;
        }

        private static void RepaintAllStyledWindows()
        {
            var windows = Resources.FindObjectsOfTypeAll<StyledConsoleWindow>();
            for (int i = 0; i < windows.Length; i++) windows[i]?.Repaint();
        }
    }
}