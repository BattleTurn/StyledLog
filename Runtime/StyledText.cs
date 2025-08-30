// StyledText.cs
using System.Text;
using UnityEngine;
using TMPro;

namespace BattleTurn.StyledLog
{
    /// <summary>
    /// Lightweight styled text segment for composing rich debug logs.
    /// Uses Unity rich-text tags (and TMP <font> tag by name) for in-game UI rendering.
    /// </summary>
    public struct StyledText
    {
        // Public fields follow the user's naming convention (lowercase for fields)
        public string text;
        public string hexColor;
        public TextStyle style;
        public string font; // resolved font asset name for <font="..."> tag

        public StyledText(string text, string hexColor = null, TextStyle style = TextStyle.None, string font = null)
        {
            this.text = text;
            this.hexColor = hexColor;
            this.style = style;
            this.font = font;
        }

        public StyledText(string text, StyleSetting setting)
        {
            this.text = text;
            this.hexColor = setting != null ? setting.HexColor : null;
            this.style = setting != null ? setting.Style : TextStyle.None;

            // Prefer TMP font name; fallback to legacy Font name
            string name = null;
            if (setting != null)
            {
                if (setting.TmpFont != null) name = setting.TmpFont.name;
                else if (setting.Font != null) name = setting.Font.name;
            }
            this.font = name;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(font))
                sb.Append($"<font=\"{font}\">");

            if (!string.IsNullOrEmpty(hexColor))
                sb.Append($"<color={hexColor}>");

            if (style.HasFlag(TextStyle.Bold)) sb.Append("<b>");
            if (style.HasFlag(TextStyle.Underline)) sb.Append("<u>");
            if (style.HasFlag(TextStyle.Strikethrough)) sb.Append("<s>");

            sb.Append(text);

            if (style.HasFlag(TextStyle.Strikethrough)) sb.Append("</s>");
            if (style.HasFlag(TextStyle.Underline)) sb.Append("</u>");
            if (style.HasFlag(TextStyle.Bold)) sb.Append("</b>");

            if (!string.IsNullOrEmpty(hexColor)) sb.Append("</color>");
            if (!string.IsNullOrEmpty(font)) sb.Append("</font>");

            return sb.ToString();
        }
    }
}
