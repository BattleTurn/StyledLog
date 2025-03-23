using UnityEngine;
using System;

namespace Colorful.Log
{
    /// <summary>
    /// Utility class for displaying colored debug messages in the Unity console.
    /// </summary>
    public static class Colorful
    {
        public delegate string FormatDelegate(string content, object message, string hexColor);

        public static event FormatDelegate onLogEvent;
        /// <summary>
        /// Log a message with a specific color.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="hexColor">The color in hexadecimal format (e.g., "FF0000" for red)</param>
        private static string LogHex(object message, string hexColor, Action<object> doLog)
        {
            string log = onLogEvent != null ? onLogEvent.Invoke("<color=#{1}>{0}</color>", message, hexColor)
                : string.Format("<color=#{1}>{0}</color>", hexColor, message);
            doLog.Invoke(log);
            return log;
        }

        /// <summary>
        /// Log a message with a specific color.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="hexColor">The color in hexadecimal format (e.g., "FF0000" for red)</param>
        public static string Log(object message, string hexColor, Action<object> doLog)
        {
            if (hexColor.Contains("#"))
            {
                hexColor = hexColor.Replace("#", "");
            }
            return LogHex(message, hexColor, doLog);
        }

        /// <summary>
        /// Log a message with RGB values (0-255)
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="color">color (0-255)</param>
        public static string Log(object message, Color color, Action<object> doLog)
        {
            string hexColor = ColorUtility.ToHtmlStringRGB(color);
            LogHex(message, hexColor, doLog);
            return hexColor;
        }
    }
}