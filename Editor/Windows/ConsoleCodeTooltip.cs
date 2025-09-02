// Hover popup to preview source code at a given file:line and highlight that line.
// Non-focusable popup following the link location.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BattleTurn.StyledLog.Editor
{
    internal sealed class ConsoleCodeTooltip : EditorWindow
    {
        private static ConsoleCodeTooltip s_win;

        private string _absPath;
        private int _line = 1; // 1-based
        private string[] _fileLines = Array.Empty<string>();
        private Vector2 _scroll;
        private Rect _anchorScreenRect;
        private EditorWindow _owner;
        private string _cacheKey;

        private const int ContextBefore = 6;
        private const int ContextAfter = 8;
        private const int MaxWidth = 720;
        private const int MaxHeight = 260;

        public static void Show(Rect guiRect, EditorWindow owner, string absolutePath, int line)
        {
            if (string.IsNullOrEmpty(absolutePath) || line <= 0) return;

            // Convert both corners to screen space (account for DPI scaling)
            float pp = Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
            var topLeft = GUIUtility.GUIToScreenPoint(new Vector2(guiRect.xMin, guiRect.yMin)) / pp;
            var bottomRight = GUIUtility.GUIToScreenPoint(new Vector2(guiRect.xMax, guiRect.yMax)) / pp;
            var anchor = Rect.MinMaxRect(Mathf.Floor(topLeft.x), Mathf.Floor(topLeft.y), Mathf.Floor(bottomRight.x), Mathf.Floor(bottomRight.y));
            var desired = PlaceNearAnchor(anchor, new Vector2(MaxWidth, MaxHeight));

            string key = absolutePath + ":" + line;
            string prevKey = s_win != null ? s_win._cacheKey : null;
            bool needShowPopup = s_win == null || prevKey != key;

            if (s_win == null)
            {
                s_win = CreateInstance<ConsoleCodeTooltip>();
                s_win.titleContent = new GUIContent("Code Preview");
                s_win.hideFlags = HideFlags.DontSave;
            }

            // If an existing tooltip belongs to another owner and we're about to show for this owner, close it first
            if (needShowPopup && s_win != null && s_win._owner != null && s_win._owner != owner)
            {
                try { s_win.Close(); } catch { }
                s_win = CreateInstance<ConsoleCodeTooltip>();
                s_win.titleContent = new GUIContent("Code Preview");
                s_win.hideFlags = HideFlags.DontSave;
            }

            s_win._owner = owner;
            s_win._anchorScreenRect = anchor;
            // Only (re)load when target changed
            if (s_win._cacheKey != key)
            {
                s_win.Load(absolutePath, line);
            }

            // Fit to screen bounds
            var pos = s_win.FitToScreen(desired);
            s_win.position = pos;
            // Show popup when first created or when target changes; keep it topmost
            if (needShowPopup)
            {
                s_win.ShowPopup();
                s_win.Focus();
            }
            s_win.Repaint();
        }

        public static void HideIfOwner(EditorWindow owner)
        {
            if (s_win != null && s_win._owner == owner)
            {
                s_win.Close();
                s_win = null;
            }
        }

        private void Load(string absolutePath, int line)
        {
            string key = absolutePath + ":" + line;
            if (_cacheKey == key) return;
            _cacheKey = key;

            _absPath = absolutePath;
            _line = Mathf.Max(1, line);
            try
            {
                if (File.Exists(_absPath))
                {
                    _fileLines = File.ReadAllLines(_absPath);
                }
                else
                {
                    _fileLines = new[] { "<file not found>", _absPath };
                }
            }
            catch (Exception ex)
            {
                _fileLines = new[] { "<error reading file>", ex.Message };
            }
        }

        private Rect FitToScreen(Rect desired)
        {
            var disp = GetMainDisplayRect();
            float x = Mathf.Clamp(desired.x, disp.xMin + 4, disp.xMax - desired.width - 4);
            float y = Mathf.Clamp(desired.y, disp.yMin + 4, disp.yMax - desired.height - 4);
            return new Rect(x, y, desired.width, desired.height);
        }

        private static Rect GetMainDisplayRect()
        {
            // Fallback to current display size
            float pp = Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
            float w = (Display.main != null ? Display.main.systemWidth : Screen.currentResolution.width) / pp;
            float h = (Display.main != null ? Display.main.systemHeight : Screen.currentResolution.height) / pp;
            return new Rect(0, 0, w, h);
        }

        private void OnGUI()
        {
            // Background
            var bg = EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f, 0.98f) : new Color(1f, 1f, 1f, 0.98f);
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), bg);
            var border = EditorGUIUtility.isProSkin ? new Color(0.25f, 0.6f, 0.4f, 0.9f) : new Color(0.1f, 0.5f, 0.25f, 0.9f);
            EditorGUI.DrawRect(new Rect(0, 0, position.width, 1), border);
            EditorGUI.DrawRect(new Rect(0, position.height - 1, position.width, 1), border);
            EditorGUI.DrawRect(new Rect(0, 0, 1, position.height), border);
            EditorGUI.DrawRect(new Rect(position.width - 1, 0, 1, position.height), border);

            GUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{_absPath}:{_line}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open", GUILayout.Width(60)))
                {
                    var rel = ToUnityPath(_absPath);
                    if (!string.IsNullOrEmpty(rel))
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rel);
                        if (obj != null) AssetDatabase.OpenAsset(obj, _line);
                    }
                }
            }

            GUILayout.Space(4);

            // Code preview (use label style so it doesn't draw its own dark background)
            var style = new GUIStyle(EditorStyles.label) { richText = false, wordWrap = false, clipping = TextClipping.Clip };
            style.font = EditorStyles.miniFont;

            int start = Mathf.Max(1, _line - ContextBefore);
            int end = Mathf.Min(_fileLines.Length, _line + ContextAfter);

            float lineHeight = EditorGUIUtility.singleLineHeight + 2f;
            float contentH = Mathf.Max(position.height - 48f, 50f);
            var view = GUILayoutUtility.GetRect(0, contentH, GUILayout.ExpandWidth(true));
            var content = new Rect(0, 0, view.width - 16f, Mathf.Max(contentH, (end - start + 1) * lineHeight + 8f));
            _scroll = GUI.BeginScrollView(view, _scroll, content);

            float y = 4f;
            for (int i = start; i <= end; i++)
            {
                string ln = i <= _fileLines.Length ? _fileLines[i - 1] : string.Empty;
                var row = new Rect(6, y, content.width - 12, lineHeight);
                if (i == _line)
                {
                    // Strong highlight background and accent lines for visibility
                    var hl = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.45f, 0.3f, 0.95f) : new Color(0.7f, 0.95f, 0.85f, 1f);
                    var accent = EditorGUIUtility.isProSkin ? new Color(0.35f, 0.9f, 0.55f, 1f) : new Color(0.1f, 0.6f, 0.35f, 1f);
                    var bgRect = new Rect(2, y - 2, content.width - 4, lineHeight + 4);
                    EditorGUI.DrawRect(bgRect, hl);
                    // top and bottom accent lines ensure it isn't visually swallowed by dark frames
                    EditorGUI.DrawRect(new Rect(bgRect.x, bgRect.y, bgRect.width, 1), accent);
                    EditorGUI.DrawRect(new Rect(bgRect.x, bgRect.yMax - 1, bgRect.width, 1), accent);
                }
                GUI.Label(row, $"{i,4}: {ln}", style);
                y += lineHeight;
            }

            GUI.EndScrollView();
        }

        private void Update()
        {
            // Owner controls show/hide to prevent flicker; no auto-close here.
        }

        // Decide a position that doesn't cover the link and stays on-screen
        private static Rect PlaceNearAnchor(Rect anchor, Vector2 size)
        {
            var screen = GetMainDisplayRect();
            // Try below the anchor
            var below = new Rect(anchor.xMin, anchor.yMax + 6f, size.x, size.y);
            if (!below.Overlaps(anchor))
            {
                below = FitToScreenStatic(below, screen);
                if (!below.Overlaps(anchor)) return below;
            }
            // Try above
            var above = new Rect(anchor.xMin, anchor.yMin - size.y - 6f, size.x, size.y);
            above = FitToScreenStatic(above, screen);
            if (!above.Overlaps(anchor)) return above;

            // Try to the right
            var right = new Rect(anchor.xMax + 6f, anchor.yMin, size.x, size.y);
            right = FitToScreenStatic(right, screen);
            if (!right.Overlaps(anchor)) return right;

            // Finally to the left
            var left = new Rect(anchor.xMin - size.x - 6f, anchor.yMin, size.x, size.y);
            left = FitToScreenStatic(left, screen);
            return left;
        }

        private static Rect FitToScreenStatic(Rect desired, Rect screen)
        {
            float x = Mathf.Clamp(desired.x, screen.xMin + 4, screen.xMax - desired.width - 4);
            float y = Mathf.Clamp(desired.y, screen.yMin + 4, screen.yMax - desired.height - 4);
            return new Rect(x, y, desired.width, desired.height);
        }

        private static string ToUnityPath(string abs)
        {
            if (string.IsNullOrEmpty(abs)) return null;
            abs = abs.Replace('\\', '/');
            var data = Application.dataPath.Replace('\\', '/');
            if (abs.StartsWith(data)) return "Assets" + abs.Substring(data.Length);
            int idx = abs.IndexOf("/Packages/");
            if (idx >= 0) return abs.Substring(idx + 1);
            return null;
        }
    }
}
