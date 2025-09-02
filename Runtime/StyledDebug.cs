using System.Diagnostics;
using UnityEngine;

using Debug = UnityEngine.Debug;

namespace BattleTurn.StyledLog
{
    public static class StyledDebug
    {
        private static StyledLogManager _styledLogManager;

        /// <summary>
        /// Emitted when a styled log is generated. First parameter is the tag, second is the rich text, third is the log type, fourth is the stack trace.
        /// </summary>
        public static event System.Action<string, string, LogType, string> onEmit;

        /// <summary>
        /// Debug method to check if there are any subscribers to the onEmit event
        /// </summary>
        public static bool HasOnEmitSubscribers => onEmit != null;

        /// <summary>
        /// Debug method to get the number of subscribers to the onEmit event
        /// </summary>
        public static int OnEmitSubscriberCount => onEmit?.GetInvocationList()?.Length ?? 0;

        public static StyledLogManager StyledLogManager
        {
            get
            {
                if (_styledLogManager == null)
                {
                    _styledLogManager = Resources.Load<StyledLogManager>(nameof(StyledLogManager));
                }
                return _styledLogManager;
            }
            set => _styledLogManager = value;
        }

        public static void Log(string tag, string message)
        {
            LogInternal(Debug.Log, tag, message);
        }

        public static void Log(string tag, params StyledText[] parts)
        {
            LogInternal(Debug.Log, tag, parts);
        }

        public static void LogWarning(string tag, string message)
        {
            LogInternal(Debug.LogWarning, tag, message);
        }

        public static void LogWarning(string tag, params StyledText[] parts)
        {
            LogInternal(Debug.LogWarning, tag, parts);
        }

        public static void LogError(string tag, string message)
        {
            LogInternal(Debug.LogError, tag, message);
        }

        public static void LogError(string tag, params StyledText[] parts)
        {
            LogInternal(Debug.LogError, tag, parts);
        }

        private static void LogInternal(System.Action<object> logAction, string tag, string message)
        {
            LogInternal(logAction, tag, new StyledText(message, StyledLogManager[tag]));
        }

        private static void LogInternal(System.Action<object> logAction, string tag, params StyledText[] parts)
        {
            var style = StyledLogManager[tag];
            if (style != null && !style.Enabled) return;

            var sbConsole = new System.Text.StringBuilder();
            var sbRich = new System.Text.StringBuilder();

            foreach (var p in parts)
            {
                sbConsole.Append(p.ToRichText(includeFontTag: false)); // Console-safe: no <font>
                sbRich.Append(p.ToRichText(includeFontTag: true));     // Full rich for custom sinks (TMP/UI)
            }

            var msgConsole = sbConsole.ToString();
            var msgRich = sbRich.ToString();

            logAction(msgConsole);

            // Determine log type
            var type = (logAction == Debug.LogError) ? LogType.Error
                     : (logAction == Debug.LogWarning) ? LogType.Warning
                     : LogType.Log;

            // Capture stack trace (skip 2 frames: this method + caller wrapper)
            var st = new StackTrace(skipFrames: 2, fNeedFileInfo: true).ToString();

            onEmit?.Invoke(tag, msgRich, type, st);
        }
    }
}
