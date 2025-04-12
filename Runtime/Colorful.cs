using UnityEngine;
using System;
using System.Text;

namespace UniLog
{
    /// <summary>
    /// Utility class for displaying colored debug messages in the Unity console.
    /// </summary>
    public static class Colorful
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
        /// <param name="doLog">The logging action to perform</param>
        /// <param name="parameters">Additional parameters for formatting the message</param>
        /// <returns>The formatted log message</returns>
        public static string Log(string message, string hexColor, Action<object> doLog, params object[] parameters)
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
        public static string Log(string message, Color color, Action<object> doLog, params object[] parameters)
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
        private static string LogHex(string message, string hexColor, Action<object> doLog, params object[] parameters)
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

        private static string HandleStringBuilderEvent(string message, string hexColor, object[] parameters)
        {
            string log;
            if (onStringBuilderAppendEvent != null)
            {
                string sb = onStringBuilderAppendEvent.Invoke("<color=#>", hexColor, ">", message, "</color", parameters);
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
                log = onLogEvent.Invoke(stringBuilder.ToString(), parameters);
            }

            return log;
        }

    }
}