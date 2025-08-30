// StyledConsoleWindow.cs
// Table view with search + resizable columns + per-row Font + stacktrace nav.
// Put in an Editor folder.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace BattleTurn.StyledLog.Editor
{
    public class StyledConsoleWindow : EditorWindow
    {
        private class Entry
        {
            public LogType type;
            public string tag;
            public string rich;   // <color>/<b>/<u>/<s> (no <font>)
            public Font font;     // per-row Unity Font
            public string stack;
        }

        private static readonly List<Entry> _entries = new();
        private static readonly Regex _fontOpenTagRx = new Regex(@"<font.*?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _fileLineRx = new Regex(@"\(at\s+(.+):(\d+)\)", RegexOptions.Compiled);

        private Vector2 _scroll;

        // Filters
        private bool _showLog = true, _showWarn = true, _showError = true;
        private string _search = "";

        // Icons (load in OnEnable)
        private GUIContent _iconInfo, _iconWarn, _iconError;

        // Column widths + resizing
        private float _colIconW = 24f;
        private float _colTypeW = 80f;
        private float _colTagW = 160f;
        private const float MIN_COL_W = 60f;
        private const float SPLIT_W = 4f;
        private int _draggingCol = -1; // 0 = type|tag splitter, 1 = tag|msg splitter
        private float _dragStartX;
        private float _typeWStart, _tagWStart;

        [MenuItem("Tools/StyledDebug/Styled Console")]
        public static void Open()
        {
            var w = GetWindow<StyledConsoleWindow>("Styled Console");
            w.minSize = new Vector2(740, 320);
            w.Show();
        }

        private void OnEnable()
        {
            _iconInfo = EditorGUIUtility.IconContent("console.infoicon");
            _iconWarn = EditorGUIUtility.IconContent("console.warnicon");
            _iconError = EditorGUIUtility.IconContent("console.erroricon");

            StyledDebug.onEmit += OnEmit;
        }
        private void OnDisable()
        {
            StyledDebug.onEmit -= OnEmit;
        }

        private void OnEmit(string tag, string richWithFont, LogType type, string stack)
        {
            // Resolve per-row Font from StyleSetting
            Font font = null;
            var mgr = StyledDebug.StyledLogManager;
            if (mgr != null)
            {
                var style = mgr[tag];
                if (style != null && style.Font != null)
                    font = style.Font;
            }

            // Strip <font> tags; IMGUI won't render them (Font is applied via GUIStyle)
            var msg = _fontOpenTagRx.Replace(richWithFont, string.Empty).Replace("</font>", string.Empty);

            _entries.Add(new Entry { type = type, tag = string.IsNullOrEmpty(tag) ? "default" : tag, rich = msg, font = font, stack = stack });
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawHeaderWithSplitters();
            DrawRows();
            HandleResizeEvents(); // keep resize responsive even when mouse outside header
        }

        // ─────────────────────────────────────────────────────────────────────────────

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _showLog = GUILayout.Toggle(_showLog, "Log", EditorStyles.toolbarButton);
                _showWarn = GUILayout.Toggle(_showWarn, "Warning", EditorStyles.toolbarButton);
                _showError = GUILayout.Toggle(_showError, "Error", EditorStyles.toolbarButton);

                GUILayout.FlexibleSpace();

                // Search field (toolbar style)
#if UNITY_2021_1_OR_NEWER
                _search = GUILayout.TextField(_search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(160));
                if (GUILayout.Button(GUIContent.none, GUI.skin.FindStyle("ToolbarSearchCancelButton")))
                {
                    _search = "";
                    GUI.FocusControl(null);
                }
#else
                _search = GUILayout.TextField(_search, GUI.skin.FindStyle("ToolbarSeachTextField"), GUILayout.MinWidth(160));
                if (GUILayout.Button(GUIContent.none, GUI.skin.FindStyle("ToolbarSeachCancelButton")))
                {
                    _search = "";
                    GUI.FocusControl(null);
                }
#endif

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
                    _entries.Clear();
            }
        }

        private void DrawHeaderWithSplitters()
        {
            var headerRect = EditorGUILayout.BeginHorizontal("box", GUILayout.Height(20));
            {
                // Column labels
                GUILayout.Label("", GUILayout.Width(_colIconW)); // icon spacer
                GUILayout.Label("Type", GUILayout.Width(_colTypeW - _colIconW));
                GUILayout.Label("Tag", GUILayout.Width(_colTagW));
                GUILayout.Label("Message"); // fills remaining

                // Compute splitter rects (relative to header rect)
                var rTypeRight = new Rect(_colTypeW, headerRect.y, SPLIT_W, headerRect.height);
                var rTagRight = new Rect(_colTypeW + _colTagW + SPLIT_W, headerRect.y, SPLIT_W, headerRect.height);

                // Draw splitters (thin vertical lines) and cursor
                DrawSplitter(rTypeRight, 0);
                DrawSplitter(rTagRight, 1);

                // Bottom divider line (nice thin rule)
                Handles.color = new Color(0, 0, 0, 0.2f);
                Handles.DrawLine(new Vector2(headerRect.x, headerRect.yMax - 1), new Vector2(headerRect.xMax, headerRect.yMax - 1));
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSplitter(Rect r, int colIndex)
        {
            // Hit area slightly wider than the visual line
            var hit = r; hit.width = Mathf.Max(8f, r.width);
            EditorGUIUtility.AddCursorRect(hit, MouseCursor.ResizeHorizontal);

            // Visual thin line
            var line = r; line.width = 1f;
            EditorGUI.DrawRect(line, new Color(0, 0, 0, 0.25f));

            // Mouse handling (start)
            if (Event.current.type == EventType.MouseDown && hit.Contains(Event.current.mousePosition))
            {
                _draggingCol = colIndex;
                _dragStartX = Event.current.mousePosition.x;
                _typeWStart = _colTypeW;
                _tagWStart = _colTagW;
                GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                Event.current.Use();
            }
        }

        private void HandleResizeEvents()
        {
            if (_draggingCol < 0) return;

            if (Event.current.type == EventType.MouseDrag)
            {
                var dx = Event.current.mousePosition.x - _dragStartX;
                if (_draggingCol == 0)
                {
                    _colTypeW = Mathf.Max(MIN_COL_W, _typeWStart + dx);
                }
                else if (_draggingCol == 1)
                {
                    _colTagW = Mathf.Max(MIN_COL_W, _tagWStart + dx);
                }
                Repaint();
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                _draggingCol = -1;
                GUIUtility.hotControl = 0;
                Event.current.Use();
            }
        }

        private void DrawRows()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            var search = _search?.Trim();
            var hasSearch = !string.IsNullOrEmpty(search);
            if (hasSearch) search = search.ToLowerInvariant();

            foreach (var e in _entries)
            {
                if ((e.type == LogType.Log && !_showLog) ||
                    (e.type == LogType.Warning && !_showWarn) ||
                    (e.type == LogType.Error && !_showError))
                    continue;

                if (hasSearch)
                {
                    // Search in type, tag, and message text
                    var t = e.type.ToString().ToLowerInvariant();
                    var g = (e.tag ?? "").ToLowerInvariant();
                    var m = (e.rich ?? "").ToLowerInvariant();
                    if (t.IndexOf(search) < 0 && g.IndexOf(search) < 0 && m.IndexOf(search) < 0)
                        continue;
                }

                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    // icon
                    var icon = e.type == LogType.Error ? _iconError :
                               e.type == LogType.Warning ? _iconWarn : _iconInfo;
                    GUILayout.Label(icon, GUILayout.Width(_colIconW), GUILayout.Height(18));

                    // type (fixed width)
                    GUILayout.Label(e.type.ToString(), GUILayout.Width(_colTypeW - _colIconW));

                    // tag (fixed width)
                    GUILayout.Label(e.tag, GUILayout.Width(_colTagW));

                    // message (expands) with font + rich text
                    var rowStyle = new GUIStyle(EditorStyles.label)
                    {
                        richText = true,
                        wordWrap = true,
                        font = e.font
                    };
                    GUILayout.Label(e.rich, rowStyle);

                    // Double-click on message cell → open callsite
                    var msgRect = GUILayoutUtility.GetLastRect();
                    if (Event.current.type == EventType.MouseDown &&
                        Event.current.clickCount == 2 &&
                        msgRect.Contains(Event.current.mousePosition))
                    {
                        OpenFirstUserFrame(e.stack);
                        Event.current.Use();
                    }

                    // Context menu on message cell
                    if (Event.current.type == EventType.ContextClick &&
                        msgRect.Contains(Event.current.mousePosition))
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Open Callsite"), false, () => OpenFirstUserFrame(e.stack));
                        menu.AddItem(new GUIContent("Copy Message"), false, () => EditorGUIUtility.systemCopyBuffer = e.rich ?? "");
                        menu.AddItem(new GUIContent("Copy Stacktrace"), false, () => EditorGUIUtility.systemCopyBuffer = e.stack ?? "");
                        menu.ShowAsContext();
                        Event.current.Use();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void OpenFirstUserFrame(string stack)
        {
            if (string.IsNullOrEmpty(stack)) return;

            foreach (var line in stack.Split('\n'))
            {
                if (line.Contains("StyledDebug") || line.Contains(nameof(StyledConsoleWindow)))
                    continue;

                var m = _fileLineRx.Match(line);
                if (!m.Success) continue;

                var path = m.Groups[1].Value.Trim();
                if (!path.EndsWith(".cs")) continue;

                if (int.TryParse(m.Groups[2].Value, out int lineNum))
                {
                    InternalEditorUtility.OpenFileAtLineExternal(path, lineNum);
                    return;
                }
            }
            Debug.LogWarning("No user frame found in stacktrace.");
        }
    }
}
