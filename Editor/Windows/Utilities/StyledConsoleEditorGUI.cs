using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.Callbacks;
using UnityEngine;

namespace BattleTurn.StyledLog.Editor
{
    internal static class StyledConsoleEditorGUI
    {
        // Strip <font="..."> tags; IMGUI does not render them and we set Font via GUIStyle.
        private static readonly Regex _fontOpenTagRx = new Regex(@"<font.*?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _fileLineRx    = new Regex(@"\(at\s+(.+):(\d+)\)", RegexOptions.Compiled);

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            StyledConsoleController.EnsurePrefsLoaded();
            if (!StyledConsoleController.ClearOnRecompile)
            {
                if (StyledConsoleController.LoadSnapshot())
                {
                    // notify any open windows to repaint
                    StyledConsoleController.RaiseCleared();
                }
            }
        }

        public static string StripFontTags(string richWithFont)
        {
            if (string.IsNullOrEmpty(richWithFont)) return string.Empty;
            return _fontOpenTagRx.Replace(richWithFont, string.Empty).Replace("</font>", string.Empty);
        }

        // Returns true if a (path, line) pair could be located in the given stack.
        public static bool TryGetFirstUserFrame(string stack, out string path, out int line)
        {
            path = null; line = 0;
            if (string.IsNullOrEmpty(stack)) return false;

            foreach (var ln in stack.Split('\n'))
            {
                // Skip internal frames
                if (ln.Contains("StyledDebug") || ln.Contains("StyledConsoleWindow"))
                    continue;

                var m = _fileLineRx.Match(ln);
                if (!m.Success) continue;

                var p = m.Groups[1].Value.Trim();
                if (!p.EndsWith(".cs")) continue;

                if (int.TryParse(m.Groups[2].Value, out var lnNum))
                {
                    path = p; line = lnNum;
                    return true;
                }
            }
            return false;
        }

        // Opens the first user frame in external editor, if present.
        public static void OpenFirstUserFrame(string stack)
        {
            if (TryGetFirstUserFrame(stack, out var path, out var line))
            {
                InternalEditorUtility.OpenFileAtLineExternal(path, line);
            }
            else
            {
                Debug.LogWarning("No user frame found in stacktrace.");
            }
        }

        // Shared list renderer that draws all rows and lets controller handle interactions.
        public static void DrawRows(
            Rect rect,
            StyledConsoleController controller,
            GUIContent iconInfo,
            GUIContent iconWarn,
            GUIContent iconError,
            float colIconW,
            float colTypeW,
            float colTagW,
            bool collapsed,
            ref Vector2 scroll)
        {
            int count = controller.GetVisibleCount();
            scroll = GUI.BeginScrollView(rect, scroll, new Rect(0, 0, rect.width - 20, count * 22));

            if (count == 0)
            {
                GUI.Label(new Rect(10, 10, rect.width - 30, 20), "No logs to show. Check filters or search.", EditorStyles.miniLabel);
                GUI.EndScrollView();
                return;
            }

            for (int i = 0; i < count; i++)
            {
                controller.GetVisibleRow(i, out var type, out var tag, out var rich, out var font, out var rowCount, out var stack);
                var rowRect = new Rect(0, i * 22, rect.width - 20, 22);

                var bgStyle = (i == controller.SelectedIndex) ? "SelectionRect" : (i % 2 == 0 ? "CN EntryBackOdd" : "CN EntryBackEven");
                GUI.Box(rowRect, GUIContent.none, bgStyle);

                var iconRect = new Rect(2, rowRect.y + 2, 18, 18);
                var icon = type == LogType.Error ? iconError : type == LogType.Warning ? iconWarn : iconInfo;
                if (icon != null && icon.image != null) GUI.DrawTexture(iconRect, icon.image);

                var typeRect = new Rect(colIconW, rowRect.y, colTypeW - colIconW, rowRect.height);
                GUI.Label(typeRect, type.ToString(), EditorStyles.miniLabel);

                var tagRect = new Rect(colTypeW, rowRect.y, colTagW, rowRect.height);
                GUI.Label(tagRect, tag, EditorStyles.miniLabel);

                var msgRect = new Rect(colTypeW + colTagW, rowRect.y, rowRect.width - colTypeW - colTagW, rowRect.height);
                var msgStyle = new GUIStyle(EditorStyles.label) { richText = true, font = font };
                if (collapsed && rowCount > 1)
                {
                    var textRect = new Rect(msgRect.x, msgRect.y, msgRect.width - 32, msgRect.height);
                    var countRect = new Rect(msgRect.xMax - 32, msgRect.y, 32, msgRect.height);
                    GUI.Label(textRect, rich, msgStyle);
                    GUI.Label(countRect, $"x{rowCount}", EditorStyles.miniLabel);
                }
                else
                {
                    GUI.Label(msgRect, rich, msgStyle);
                }

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    controller.HandleRowMouseDown(i, Event.current.clickCount);
                    Event.current.Use();
                }
                if (Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
                {
                    controller.HandleRowContextMenu(i);
                    Event.current.Use();
                }
            }

            GUI.EndScrollView();
        }

        // Thin vertical splitter (with drag handle area).
        public static void DrawVSplitter(Rect r, System.Action<float> onDragDelta, System.Action onFinish)
        {
            var hit = r; hit.width = Mathf.Max(8f, r.width);
            EditorGUIUtility.AddCursorRect(hit, MouseCursor.ResizeHorizontal);

            var line = r; line.width = 1f;
            EditorGUI.DrawRect(line, new Color(0, 0, 0, 0.25f));

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (hit.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        onDragDelta?.Invoke(e.delta.x);
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        onFinish?.Invoke();
                        e.Use();
                    }
                    break;
            }
        }

        // Thin horizontal splitter (for the top/bottom panes).
        public static void DrawHSplitter(Rect r, System.Action<float> onDragDelta, System.Action onFinish)
        {
            EditorGUIUtility.AddCursorRect(r, MouseCursor.ResizeVertical);
            EditorGUI.DrawRect(r, new Color(0, 0, 0, 0.25f));

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (r.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        onDragDelta?.Invoke(e.delta.y);
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        onFinish?.Invoke();
                        e.Use();
                    }
                    break;
            }
        }

        // Toolbar search field with cancel button (Unity style).
        public static string ToolbarSearch(string value, float minWidth = 180f)
        {
#if UNITY_2021_1_OR_NEWER
            value = GUILayout.TextField(value, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(minWidth));
            if (GUILayout.Button(GUIContent.none, GUI.skin.FindStyle("ToolbarSearchCancelButton")))
            {
                value = string.Empty;
                GUI.FocusControl(null);
            }
#else
            value = GUILayout.TextField(value, GUI.skin.FindStyle("ToolbarSeachTextField"), GUILayout.MinWidth(minWidth));
            if (GUILayout.Button(GUIContent.none, GUI.skin.FindStyle("ToolbarSeachCancelButton")))
            {
                value = string.Empty;
                GUI.FocusControl(null);
            }
#endif
            return value;
        }
    }
}
