using UnityEditor;
using UnityEngine;
using UnityEditor.Compilation;

namespace BattleTurn.StyledLog.Editor
{
    [InitializeOnLoad]
    internal static class StyledConsoleHooks
    {
        static StyledConsoleHooks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

            // Hook Unity's Debug log and mirror to Styled Console
            Application.logMessageReceived -= OnUnityLog;
            Application.logMessageReceived += OnUnityLog;

            // Restore any persisted compiler diagnostics first, then sync current messages
            StyledConsoleController.LoadCompilerDiagnosticsSnapshot();
            StyledConsoleController.SyncCompilerMessages();

            // Live compilation pipeline hooks
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;

            // Continuous update for live compiler sync fallback
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        // Fallback polling window after domain reload to catch any compiler messages we missed
        private static double _pollEndTime;
        private static int _lastCompilerKeyCount;

        private static void BeginCompilerPollWindow()
        {
            _pollEndTime = EditorApplication.timeSinceStartup + 8.0; // poll up to 8s after reload
            _lastCompilerKeyCount = 0;
            EditorApplication.update -= PollCompilerMessages;
            EditorApplication.update += PollCompilerMessages;
        }

        private static void PollCompilerMessages()
        {
            if (EditorApplication.timeSinceStartup > _pollEndTime)
            {
                EditorApplication.update -= PollCompilerMessages;
                return;
            }
            // Attempt incremental sync; only triggers repaint if new entries
            StyledConsoleController.SyncCompilerMessages();
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

        private static void OnAfterAssemblyReload()
        {
            // Reload persisted compiler diagnostics (if any) then re-sync live
            StyledConsoleController.LoadCompilerDiagnosticsSnapshot();
            StyledConsoleController.SyncCompilerMessages();
            EditorApplication.delayCall += RepaintAllStyledWindows;
            BeginCompilerPollWindow(); // start fallback polling
        }

        private static void OnCompilationStarted(object obj)
        {
            // Clear previous compiler messages (keep runtime logs)
            StyledConsoleController.ClearCompilerMessages();
            // Persist clear state (removes snapshot) then ensure snapshot is empty
            StyledConsoleController.LoadCompilerDiagnosticsSnapshot(); // no-op if cleared
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            // Stream in messages per-assembly as they arrive (gives earlier feedback than waiting for whole compile)
            StyledConsoleController.AddCompilerMessages(messages);
        }

        private static void OnCompilationFinished(object obj)
        {
            // Final sync to catch any remaining diagnostics
            StyledConsoleController.SyncCompilerMessages();
        }

        private static void OnEditorUpdate()
        {
            StyledConsoleController.LiveCompilerUpdate();
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