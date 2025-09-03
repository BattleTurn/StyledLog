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
            StyledConsoleWindow.EnsurePrefsLoaded();
            if (!StyledConsoleWindow.ClearOnRecompile)
            {
                if (StyledConsoleWindow.LoadSnapshot())
                {
                    // notify any open windows to repaint
                    StyledConsoleWindow.RaiseCleared();
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
