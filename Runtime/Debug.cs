using System;
using System.Text;
using System.Text.RegularExpressions;
using Colorful.ScriptableObjects;
using UnityEngine;

namespace Colorful
{
    /// <summary>
    /// Utility class for displaying colored debug messages in the Unity console.
    /// </summary>
    public static class Debug
    {
        public delegate string FormatDelegate(string message, params object[] parameters);
        public delegate string StringBuilderAppendDelegate(params object[] parameters);

        public static event FormatDelegate onLogEvent;
        public static event StringBuilderAppendDelegate stringBuilderAppendEvent;

        /// <summary>
        /// Log a message with multiple color sections using special markup.
        /// Supports both color markup and object interpolation.
        /// </summary>
        /// <param name="formattedText">Text with color markup {#RRGGBB:colored text}</param>
        /// <param name="logType">The type of log (default is regular log)</param>
        /// <param name="args">Objects for string formatting</param>
        /// <returns>The processed rich text string</returns>
        public static string LogMultiColor(string formattedText, LogType logType = LogType.Log, params object[] args)
        {
            // Apply any standard formatting with args if provided
            string interpolatedText = formattedText;
            if (args != null && args.Length > 0)
            {
                // Temporarily protect color markup from string.Format
                interpolatedText = ProtectColorMarkup(formattedText);

                // Apply standard string formatting
                interpolatedText = onLogEvent == null ? string.Format(interpolatedText, args) : onLogEvent.Invoke(interpolatedText, args);

                // Restore the protected color markup
                interpolatedText = RestoreColorMarkup(interpolatedText);
            }

            // Process color markup
            string processedText = ProcessMultiColorMarkup(interpolatedText);

            switch (logType)
            {
                case LogType.Warning:
                    UnityEngine.Debug.LogWarning(processedText);
                    break;
                case LogType.Error:
                    UnityEngine.Debug.LogError(processedText);
                    break;
                default:
                    UnityEngine.Debug.Log(processedText);
                    break;
            }

            return processedText;
        }

        /// <summary>
        /// Temporarily replace color markup so it's not processed by string.Format
        /// </summary>
        private static string ProtectColorMarkup(string text)
        {
            return Regex.Replace(text, @"\{#([0-9A-Fa-f]{6}):([^{}]*)\}", "<<COLOR:$1>>$2<<END>>");
        }

        /// <summary>
        /// Restore the protected color markup
        /// </summary>
        private static string RestoreColorMarkup(string text)
        {
            return Regex.Replace(text, @"<<COLOR:([0-9A-Fa-f]{6})>>([^<]*)<<END>>", "{#$1:$2}");
        }

        /// <summary>
        /// Log a warning message with multiple color sections
        /// </summary>
        /// <param name="formattedText">Text with color markup {#RRGGBB:colored text}</param>
        /// <param name="args">Objects for string formatting</param>
        /// <returns>The processed rich text string</returns>
        public static string LogWarningMultiColor(string formattedText, params object[] args)
        {
            return LogMultiColor(formattedText, LogType.Warning, args);
        }

        /// <summary>
        /// Log an error message with multiple color sections
        /// </summary>
        /// <param name="formattedText">Text with color markup {#RRGGBB:colored text}</param>
        /// <param name="args">Objects for string formatting</param>
        /// <returns>The processed rich text string</returns>
        public static string LogErrorMultiColor(string formattedText, params object[] args)
        {
            return LogMultiColor(formattedText, LogType.Error, args);
        }

        /// <summary>
        /// Log a message with a specific color.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="hexColor">The color in hexadecimal format (e.g., "FF0000" for red)</param>
        /// <param name="parameters">Additional parameters for formatting the message</param>
        /// <returns>The formatted log message</returns>
        public static string LogHex(object message, string hexColor = "#ffffff", params object[] parameters)
        {
            return Log(message, hexColor, UnityEngine.Debug.Log, parameters);
        }

        /// <summary>
        /// Log a message with RGB values (0-255)
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="color">color (0-255)</param>
        /// <param name="parameters">Additional parameters for formatting the message</param>
        /// <returns>The formatted log message</returns>
        public static string Log(object message, Color color = default, params object[] parameters)
        {
            return Log(message, color, UnityEngine.Debug.Log, parameters);
        }

        /// <summary>
        /// Log a warning message with a specific color.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="hexColor">The color in hexadecimal format (e.g., "FF0000" for red)</param>
        /// <param name="parameters">Additional parameters for formatting the message</param>
        /// <returns>The formatted log message</returns>
        public static string LogHexWarning(object message, string hexColor = "#ffffff", params object[] parameters)
        {
            return Log(message, hexColor, UnityEngine.Debug.LogWarning, parameters);
        }

        /// <summary>
        /// Log a warning message with RGB values (0-255)
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="color">color (0-255)</param>
        /// <param name="parameters">Additional parameters for formatting the message</param>
        /// <returns>The formatted log message</returns>
        public static string LogWarning(object message, Color color = default, params object[] parameters)
        {
            return Log(message, color, UnityEngine.Debug.LogWarning, parameters);
        }

        /// <summary>
        /// Log a error message with a specific color.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="hexColor">The color in hexadecimal format (e.g., "FF0000" for red)</param>
        /// <param name="parameters">Additional parameters for formatting the message</param>
        /// <returns>The formatted log message</returns>
        public static string LogHexError(object message, string hexColor = "#ffffff", params object[] parameters)
        {
            return Log(message, hexColor, UnityEngine.Debug.LogError, parameters);
        }

        /// <summary>
        /// Log a error message with RGB values (0-255)
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="color">color (0-255)</param>
        /// <param name="parameters">Additional parameters for formatting the message</param>
        /// <returns>The formatted log message</returns>
        public static string LogError(object message, Color color = default, params object[] parameters)
        {
            return Log(message, color, UnityEngine.Debug.LogError, parameters);
        }

        /// <summary>
        /// Process text with color markup into Unity rich text format
        /// </summary>
        /// <param name="text">Text with color markup {#RRGGBB:colored text}</param>
        /// <returns>Unity rich text with proper color tags</returns>
        private static string ProcessMultiColorMarkup(string text)
        {
            // Match pattern {#RRGGBB:text} and replace with <color=#RRGGBB>text</color>
            string pattern = @"\{#([0-9A-Fa-f]{6}):([^{}]*)\}";
            StringBuilder sb = new StringBuilder();

            return Regex.Replace(text, pattern, match =>
            {
                string hexColor = match.Groups[1].Value;
                string coloredText = match.Groups[2].Value;

                string appendedStrings = string.Empty;
                if (stringBuilderAppendEvent == null)
                {
                    sb.Append("<color=#");
                    sb.Append(hexColor);
                    sb.Append(">");
                    sb.Append(coloredText);
                    sb.Append("</color>");
                    appendedStrings = sb.ToString();
                }
                else
                {
                    appendedStrings = stringBuilderAppendEvent.Invoke("<color=#", hexColor, ">", coloredText, "</color>");
                }

                return sb.ToString();
            });
        }

        /// <summary>
        /// Log a message with a specific color.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="hexColor">The color in hexadecimal format (e.g., "FF0000" for red)</param>
        /// <param name="doLog">The logging action to perform</param>
        /// <param name="parameters">Additional parameters for formatting the message</param>
        /// <returns>The formatted log message</returns>
        private static string Log(object message, string hexColor, Action<object> doLog, params object[] parameters)
        {
            if (Setting.IsDebugLogEnable == false)
            {
                return string.Empty;
            }

            if (hexColor.Contains("#"))
            {
                hexColor = hexColor.Replace("#", "");
            }
            return LogHex(message, hexColor, doLog, parameters);
        }

        /// <summary>
        /// Log a message with RGB values (0-255)
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="color">color (0-255)</param>
        /// <param name="doLog">The logging action to perform</param>
        /// <param name="parameters">Additional parameters for formatting the message</param>
        /// <returns>The formatted log message</returns>
        private static string Log(object message, Color color, Action<object> doLog, params object[] parameters)
        {
            if (Setting.IsDebugLogEnable == false)
            {
                return string.Empty;
            }

            if (color == default)
            {
                color = Color.white;
            }

            string hexColor = ColorUtility.ToHtmlStringRGB(color);
            LogHex(message, hexColor, doLog, parameters);
            return hexColor;
        }

        /// <summary>
        /// Log a message with a specific color.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="hexColor">The color in hexadecimal format (e.g., "FF0000" for red)</param>
        private static string LogHex(object message, string hexColor, Action<object> doLog, params object[] parameters)
        {
            string log = string.Empty;
            if (onLogEvent != null)
            {
                log = HandleStringBuilderEvent(message, hexColor, parameters);
            }
            else
            {
                log = string.Format($"<color=#{hexColor}>{message}</color>", parameters);
            }
            doLog.Invoke(log);
            return log;
        }

        private static string HandleStringBuilderEvent(object message, string hexColor, object[] parameters)
        {
            string log;
            if (stringBuilderAppendEvent != null)
            {
                string sb = stringBuilderAppendEvent.Invoke("<color=#", hexColor, ">", message, "</color>", parameters.Length == 0 || parameters == null ? "" : parameters);
                log = onLogEvent.Invoke(sb, parameters);
            }
            else
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append("<color=#");
                stringBuilder.Append(hexColor);
                stringBuilder.Append(">");
                stringBuilder.Append(message);
                stringBuilder.Append("</color>");
                log = onLogEvent.Invoke(stringBuilder.ToString(), parameters.Length == 0 || parameters == null ? "" : parameters);
            }

            return log;
        }

    }
}