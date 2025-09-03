using UnityEditor;

namespace BattleTurn.StyledLog.Editor
{
    [InitializeOnLoad]
    internal static class StyledConsoleHooks
    {
        static StyledConsoleHooks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
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
    }
}