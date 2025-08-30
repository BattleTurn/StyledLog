using System.Text;
using UnityEngine;

namespace BattleTurn.StyledLog
{
    public static class StyledDebug
    {
        private static StyledLogManager _styledLogManager;

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
            var style = StyledLogManager[tag];
            if (style != null && !style.Enabled) return;

            var styled = style != null ? new StyledText(message, style) : new StyledText(message);
            logAction(styled.ToString());
        }

        private static void LogInternal(System.Action<object> logAction, string tag, params StyledText[] parts)
        {
            var style = StyledLogManager[tag];
            if (style != null && !style.Enabled) return;

            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                sb.Append(part.ToString());
            }

            logAction(sb.ToString());
        }
    }
}
