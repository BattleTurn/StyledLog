using System;
using System.Text;
using UnityEngine;

namespace Colorful
{
    /// <summary>
    /// Utility class for displaying colored debug messages in the Unity console.
    /// </summary>
    public static class Debug
    {
        public delegate string FormatDelegate(string message, params object[] parameters);
        public delegate string StringBuilderAppends(params object[] parameters);

        public static event FormatDelegate onLogEvent;
        public static event StringBuilderAppends onStringBuilderAppendEvent;

        /// <summary>
        /// Log a message with a specific color.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="hexColor">The color in hexadecimal format (e.g., "FF0000" for red)</param>
        /// <param name="parameters">Additional parameters for formatting the message</param>
        /// <returns>The formatted log message</returns>
        public static string Log(object message, string hexColor, params object[] parameters)
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
        public static string Log(object message, Color color, params object[] parameters)
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
        public static string LogWarning(object message, string hexColor, params object[] parameters)
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
        public static string LogWarning(object message, Color color, params object[] parameters)
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
        public static string LogError(object message, string hexColor, params object[] parameters)
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
        public static string LogError(object message, Color color, params object[] parameters)
        {
            return Log(message, color, UnityEngine.Debug.LogError, parameters);
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
            if (onStringBuilderAppendEvent != null)
            {
                string sb = onStringBuilderAppendEvent.Invoke("<color=#", hexColor, ">", message, "</color>", parameters.Length == 0 || parameters == null ? "" : parameters);
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