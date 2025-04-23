using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Colorful.ScriptableObjects;

namespace Colorful
{
    /// <summary>
    /// Utility class for displaying colored debug messages in the Unity console.
    /// </summary>
    public static class Debug
    {
        /// <summary>
        /// Log a warning message with multiple color sections
        /// </summary>
        /// <param name="formattedText">Text with color markup [#RRGGBB:colored text]</param>
        /// <param name="args">Objects for string formatting</param>
        /// <returns>The processed rich text string</returns>
        [HideInCallstack]
        public static string LogMultiColor(string formattedText)
        {
            string log = ProcesssMultiColorLog(formattedText);
            UnityEngine.Debug.unityLogger.Log(LogType.Log, log);
            return log;
        }

        /// <summary>
        /// Log a warning message with multiple color sections
        /// </summary>
        /// <param name="formattedText">Text with color markup [#RRGGBB:colored text]</param>
        /// <param name="args">Objects for string formatting</param>
        /// <returns>The processed rich text string</returns>
        [HideInCallstack]
        public static string LogWarningMultiColor(string formattedText)
        {
            string log = ProcesssMultiColorLog(formattedText);
            UnityEngine.Debug.unityLogger.Log(LogType.Warning, log);
            return log;
        }

        /// <summary>
        /// Log an error message with multiple color sections
        /// </summary>
        /// <param name="formattedText">Text with color markup [#RRGGBB:colored text]</param>
        /// <param name="args">Objects for string formatting</param>
        /// <returns>The processed rich text string</returns>
        [HideInCallstack]
        public static string LogErrorMultiColor(string formattedText)
        {
            string log = ProcesssMultiColorLog(formattedText);
            UnityEngine.Debug.unityLogger.Log(LogType.Error, log);
            return log;
        }

        /// <summary>
        /// Log a message with a specific color.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="hexColor">The color in hexadecimal format (e.g., "FF0000" for red)</param>
        /// <returns>The formatted log message</returns>
        [HideInCallstack]
        public static string Log(object message, string hexColor = "#EEEEEE")
        {
            string log = ProcessLog(message, hexColor);
            UnityEngine.Debug.unityLogger.Log(LogType.Log, log);
            return log;
        }

        /// <summary>
        /// Log a message with RGB values (0-255)
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="color">color (0-255)</param>
        /// <returns>The formatted log message</returns>
        [HideInCallstack]
        public static string Log(object message, Color color)
        {
            string log =  ProcessColorLog(message, color);
            UnityEngine.Debug.unityLogger.Log(LogType.Log, log);
            return log;
        }

        /// <summary>
        /// Log a warning message with a specific color.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="hexColor">The color in hexadecimal format (e.g., "FF0000" for red)</param>
        /// <returns>The formatted log message</returns>
        [HideInCallstack]
        public static string LogWarning(object message, string hexColor = "#FFFF00")
        {
            string log = ProcessLog(message, hexColor);
            UnityEngine.Debug.unityLogger.Log(LogType.Warning, log);
            return log;
        }

        /// <summary>
        /// Log a warning message with RGB values (0-255)
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="color">color (0-255)</param>
        /// <returns>The formatted log message</returns>
        [HideInCallstack]
        public static string LogWarning(object message, Color color)
        {
            string log = ProcessColorLog(message, color);
            UnityEngine.Debug.unityLogger.Log(LogType.Warning, log);
            return log;
        }

        /// <summary>
        /// Log a error message with a specific color.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="hexColor">The color in hexadecimal format (e.g., "FF0000" for red)</param>
        /// <returns>The formatted log message</returns>
        [HideInCallstack]
        public static string LogError(object message, string hexColor = "#FF0000")
        {
            string log = ProcessLog(message, hexColor);
            UnityEngine.Debug.unityLogger.Log(LogType.Error, log);
            return log;
        }

        /// <summary>
        /// Log a error message with RGB values (0-255)
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="color">color (0-255)</param>
        /// <returns>The formatted log message</returns>
        [HideInCallstack]
        public static string LogError(object message, Color color)
        {
            string log =  ProcessColorLog(message, color);
            UnityEngine.Debug.unityLogger.Log(LogType.Error, log);
            return log;
        }

        /// <summary>
        /// Log a message with multiple color sections using special markup.
        /// Supports both color markup and object interpolation.
        /// </summary>
        /// <param name="formattedText">Text with color markup [#RRGGBB:colored text]</param>
        /// <param name="logType">The type of log (default is regular log)</param>
        /// <param name="args">Objects for string formatting</param>
        /// <returns>The processed rich text string</returns>
        [HideInCallstack]
        private static string ProcesssMultiColorLog(string formattedText)
        {
            // Apply any standard formatting with args if provided
            string interpolatedText = formattedText;

            // Process color markup
            string processedText = ProcessMultiColorMarkup(interpolatedText);
            return processedText;
        }

        
        /// <summary>
        /// Temporarily replace color markup so it's not processed by string.Format
        /// </summary>
        /// <param name="text">Text with color markup [#RRGGBB:colored text]</param>
        [HideInCallstack]
        private static string ProtectColorMarkup(string text)
        {
            // Update pattern to match both 6-character (RRGGBB) and 8-character (RRGGBBAA) hex colors
            return Regex.Replace(text, @"\{#([0-9A-Fa-f]{6,8}):([^{}]*)\}", "<<COLOR:$1>>$2<<END>>");
        }

        /// <summary>
        /// Restore the protected color markup
        /// </summary>
        /// <param name="text">Text with color markup [#RRGGBB:colored text]</param>
        [HideInCallstack]
        private static string RestoreColorMarkup(string text)
        {
            return Regex.Replace(text, @"<<COLOR:([0-9A-Fa-f]{6,8})>>([^<]*)<<END>>", "{#$1:$2}");
        }

        /// <summary>
        /// Process text with color markup into Unity rich text format
        /// </summary>
        /// <param name="text">Text with color markup [#RRGGBB:colored text]</param>
        /// <returns>Unity rich text with proper color tags</returns>
        [HideInCallstack]
        private static string ProcessMultiColorMarkup(string text, LogType logType = LogType.Log)
        {
            bool isDebugLogEnable = CheckCanDebug();
            DebugBeforeRun(out float timeStart, out long beforeMemory);
            if (!isDebugLogEnable)
            {
                return string.Empty;
            }
            // Match pattern [#RRGGBB:text] and replace with <color=#RRGGBB>text</color>
            string pattern = @"\[#([0-9A-Fa-f]{6,8}):([^\[\]]*)\]";

            string finalLog = string.Empty;


           if (Setting.IsTestingDebugMode)
            {
                UnityEngine.Debug.Log("Using default StringBuilder for color markup processing. Consider using a custom StringBuilder for better performance.");
            }
            
            StringBuilder sb = new StringBuilder();
            finalLog = Regex.Replace(text, pattern, match =>
            {
                string hexColor = match.Groups[1].Value;
                string coloredText = match.Groups[2].Value;
                sb.Clear();
                sb.Append("<color=#");
                sb.Append(hexColor);
                sb.Append(">");
                sb.Append(coloredText);
                sb.Append("</color>");
                return sb.ToString();

            });

            DebugAfterRun(timeStart, beforeMemory);
            return finalLog;
        }

        /// <summary>
        /// Log a message with a specific color.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="hexColor">The color in hexadecimal format (e.g., "FF0000" for red)</param>
        /// <param name="doLog">The logging action to perform</param>
        /// <param name="parameters">Additional parameters for formatting the message</param>
        /// <returns>The formatted log message</returns>
        [HideInCallstack]
        private static string ProcessLog(object message, string hexColor)
        {
            if (Setting.IsDebugLogOnDevMode == false)
            {
                return string.Empty;
            }

            if (hexColor.Contains("#"))
            {
                hexColor = hexColor.Replace("#", "");
            }
            return LogHex(message, hexColor);
        }

        /// <summary>
        /// Log a message with RGB values (0-255)
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="color">color (0-255)</param>
        /// <param name="doLog">The logging action to perform</param>
        /// <param name="parameters">Additional parameters for formatting the message</param>
        /// <returns>The formatted log message</returns>
        [HideInCallstack]
        private static string ProcessColorLog(object message, Color color)
        {
            bool isDebugLogEnable = CheckCanDebug();
            if (!isDebugLogEnable)
            {
                return string.Empty;
            }

            if (color == default)
            {
                color = Color.white;
            }

            string hexColor = ColorUtility.ToHtmlStringRGB(color);
            LogHex(message, hexColor);
            return hexColor;
        }

        private static bool CheckCanDebug()
        {
            bool isDebugLogEnable = Setting.IsDebugLogEnableOnProductMode;

#if UNITY_EDITOR
            isDebugLogEnable = Setting.IsDebugLogOnDevMode;
#else
            isDebugLogEnable = Setting.IsDebugLogEnableOnProductMode;
#endif
            return isDebugLogEnable;
        }

        /// <summary>
        /// Log a message with a specific color.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="hexColor">The color in hexadecimal format (e.g., "FF0000" for red)</param>
        [HideInCallstack]
        private static string LogHex(object message, string hexColor)
        {
            DebugBeforeRun(out float timeStart, out long beforeMemory);

            string log = HandleStringBuilderEvent(message, hexColor);
            DebugAfterRun(timeStart, beforeMemory);
            return log;
        }

        private static void DebugBeforeRun(out float timeStart, out long beforeMemory)
        {
            timeStart = 0;
            beforeMemory = 0;
            if (Setting.IsTestingDebugMode)
            {
                beforeMemory = GC.GetTotalMemory(false);
                timeStart = Time.realtimeSinceStartup;
            }
        }

        private static void DebugAfterRun(float timeStart, long beforeMemory)
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

        private static string HandleStringBuilderEvent(object message, string hexColor)
        {
            string log;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("<color=#");
            stringBuilder.Append(hexColor);
            stringBuilder.Append(">");
            stringBuilder.Append(message);
            stringBuilder.Append("</color>");
            log = stringBuilder.ToString();
            return log;
        }

    }
}