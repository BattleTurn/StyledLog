// StyledConsoleWindow.cs
// Table view + search + resizable columns + selection + bottom stacktrace pane
// + double-click to open callsite, status bar counters, auto-scroll, collapse.
//
// Put this file in an Editor folder.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BattleTurn.StyledLog.Editor
{
    public class StyledConsoleWindow : EditorWindow
    {
        private class Entry
        {
            public LogType type;
            public string tag;
            public string rich;    // message (rich text without <font>)
            public Font font;      // per-row Unity Font
            public string stack;   // raw stacktrace
            public int count = 1;  // collapse count
        }

        // data
        private static readonly List<Entry> _all = new();
        private static readonly List<Entry> _collapsed = new();
        private static readonly Dictionary<string, Entry> _collapseIndex = new();

        private static readonly List<Entry> _visible = new();

        // selection
        private int _selectedIndex = -1;
        private IList<Entry> ActiveList() => _collapse ? _collapsed : _all;

        // scroll states
        private Vector2 _scrollList;
        private Vector2 _scrollStack;

        // filters
        private bool _showLog = true, _showWarn = true, _showError = true;
        private string _search = "";

        // view toggles
        private bool _autoScroll = true;
        private bool _collapse = false;
        private bool _autoScrollRequest = false; // set when new entries arrive

        // counters (total, not filtered)
        private int _countLog, _countWarn, _countError;

        // icons (loaded in OnEnable)
        private GUIContent _iconInfo, _iconWarn, _iconError;

        // columns
        private float _colIconW = 24f;
        private float _colTypeW = 80f;
        private float _colTagW = 160f;
        private const float _minColW = 60f;
        private const float _splitW = 4f;
        private bool _dragTypeSplit, _dragTagSplit;
        private float _typeWStart, _tagWStart, _dragStartX;

        // vertical split (list vs. stack panel)
        private float _stackFrac = 0.38f;          // 0..1 (bottom pane height / content height)
        private const float _minStackH = 80f;
        private const float _minListH = 80f;

        [MenuItem("Tools/StyledDebug/Styled Console")]
        public static void Open()
        {
            var w = GetWindow<StyledConsoleWindow>("Styled Console");
            w.minSize = new Vector2(760, 380);
            w.Show();
        }

        private void OnEnable()
        {
            _iconInfo = EditorGUIUtility.IconContent("console.infoicon");
            _iconWarn = EditorGUIUtility.IconContent("console.warnicon");
            _iconError = EditorGUIUtility.IconContent("console.erroricon");

            // Unsubscribe first to avoid double subscription
            StyledDebug.onEmit -= OnEmit;
            StyledDebug.onEmit += OnEmit;
        }

        private void OnDisable()
        {
            StyledDebug.onEmit -= OnEmit;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // ingest

        private void OnEmit(string tag, string richWithFont, LogType type, string stack)
        {
            // resolve per-row Font from StyleSetting (for IMGUI)
            Font font = null;
            var mgr = StyledDebug.StyledLogManager;
            if (mgr != null)
            {
                var s = mgr[tag];
                if (s != null && s.Font != null) font = s.Font;
            }

            // strip <font> tags; IMGUI doesn't render them
            var msg = StyledConsoleUtil.StripFontTags(richWithFont);

            var e = new Entry
            {
                type = type,
                tag = string.IsNullOrEmpty(tag) ? "default" : tag,
                rich = msg,
                font = font,
                stack = stack
            };

            _all.Add(e);
            Tally(type, +1);

            if (_collapse) AddCollapsed(e);

            // autoselect newest if nothing selected
            if (_selectedIndex < 0 && ActiveList().Count > 0)
                _selectedIndex = ActiveList().Count - 1;

            // request autoscroll (only once per batch)
            _autoScrollRequest = _autoScroll;

            Repaint();
        }

        private static string CollapseKey(Entry e) => $"{(int)e.type}|{e.tag}|{e.rich}";

        private void AddCollapsed(Entry e)
        {
            var key = CollapseKey(e);
            if (_collapseIndex.TryGetValue(key, out var hit))
            {
                hit.count++;
            }
            else
            {
                var clone = new Entry { type = e.type, tag = e.tag, rich = e.rich, font = e.font, stack = e.stack, count = 1 };
                _collapseIndex[key] = clone;
                _collapsed.Add(clone);
            }
        }

        private void RebuildCollapsed()
        {
            _collapsed.Clear();
            _collapseIndex.Clear();
            foreach (var e in _all) AddCollapsed(e);
        }

        private void BuildVisible()
        {
            _visible.Clear();
            var list = ActiveList();

            string s = _search?.Trim();
            bool hasSearch = !string.IsNullOrEmpty(s);
            if (hasSearch) s = s.ToLowerInvariant();

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];

                if ((e.type == LogType.Log && !_showLog) ||
                    (e.type == LogType.Warning && !_showWarn) ||
                    (e.type == LogType.Error && !_showError))
                    continue;

                if (hasSearch)
                {
                    var t = e.type.ToString().ToLowerInvariant();
                    var g = (e.tag ?? "").ToLowerInvariant();
                    var m = (e.rich ?? "").ToLowerInvariant();
                    if (t.IndexOf(s) < 0 && g.IndexOf(s) < 0 && m.IndexOf(s) < 0)
                        continue;
                }
                _visible.Add(e);
            }

            if (_visible.Count > 0 && (_selectedIndex < 0 || _selectedIndex >= _visible.Count))
                _selectedIndex = 0;
        }

        private void Tally(LogType type, int delta)
        {
            if (type == LogType.Log) _countLog += delta;
            else if (type == LogType.Warning) _countWarn += delta;
            else if (type == LogType.Error) _countError += delta;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // UI

        private void OnGUI()
        {
            DrawToolbar();
            DrawHeaderWithSplitters();
            BuildVisible(); // <-- quan trọng: lọc trước khi vẽ list

            // Chiều cao còn lại của cửa sổ
            float availH = position.height;

            // Ước lượng phần header + toolbar + status (đã vẽ bằng layout ở trên & dưới)
            const float topChrome = 20f /*header*/ + 22f /*toolbar*/ + 6f;
            const float bottomChrome = 22f /*status*/ + 6f;

            float contentH = Mathf.Max(100f, availH - topChrome - bottomChrome);
            float listH = Mathf.Clamp(contentH * (1f - _stackFrac), _minListH, contentH - _minStackH);
            float stackH = contentH - listH;

            // LIST
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
            {
                // vùng list chiếm listH
                var listRect = GUILayoutUtility.GetRect(0, listH, GUILayout.ExpandWidth(true));
                DrawRowsAreaLayout(listRect);

                // SPLITTER
                var splitRect = GUILayoutUtility.GetRect(0, 4f, GUILayout.ExpandWidth(true));
                StyledConsoleUtil.DrawHSplitter(
                    splitRect,
                    dy =>
                    {
                        // dy > 0: kéo xuống => giảm listH => tăng stackFrac
                        float total = contentH;
                        float newStackH = Mathf.Clamp(stackH + dy, _minStackH, total - _minListH);
                        _stackFrac = newStackH / total;
                        Repaint();
                    },
                    null
                );

                // STACK PANE
                var stackRect = GUILayoutUtility.GetRect(0, stackH, GUILayout.ExpandWidth(true));
                DrawStackPaneLayout(stackRect);
            }

            DrawStatusBar();

            if (_autoScrollRequest && Event.current.type == EventType.Repaint)
            {
                _scrollList.y = float.MaxValue;
                _autoScrollRequest = false;
            }
        }


        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _showLog = GUILayout.Toggle(_showLog, "Log", EditorStyles.toolbarButton);
                _showWarn = GUILayout.Toggle(_showWarn, "Warning", EditorStyles.toolbarButton);
                _showError = GUILayout.Toggle(_showError, "Error", EditorStyles.toolbarButton);

                GUILayout.Space(6);
                _collapse = GUILayout.Toggle(_collapse, "Collapse", EditorStyles.toolbarButton);
                _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-scroll", EditorStyles.toolbarButton);

                GUILayout.FlexibleSpace();

                _search = StyledConsoleUtil.ToolbarSearch(_search, 180f);

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
                {
                    _all.Clear(); _collapsed.Clear(); _collapseIndex.Clear();
                    _countLog = _countWarn = _countError = 0;
                    _selectedIndex = -1;
                    _visible.Clear();
                    _scrollList = Vector2.zero;
                    _autoScrollRequest = false;
                }
            }

            // Rebuild collapsed view when toggled
            if (Event.current.type == EventType.Repaint && GUI.changed)
            {
                if (_collapse) RebuildCollapsed();
                var list = ActiveList();
                if (_selectedIndex >= list.Count) _selectedIndex = list.Count - 1;
            }
        }

        private void DrawHeaderWithSplitters()
        {
            var headerRect = EditorGUILayout.BeginHorizontal("box", GUILayout.Height(20));
            {
                GUILayout.Label("", GUILayout.Width(_colIconW)); // icon spacer
                GUILayout.Label("Type", GUILayout.Width(_colTypeW - _colIconW));
                GUILayout.Label("Tag", GUILayout.Width(_colTagW));
                GUILayout.Label("Message");

                // vertical splitters aligned to window coordinates
                var rTypeRight = new Rect(_colTypeW, headerRect.y, _splitW, headerRect.height);
                var rTagRight = new Rect(_colTypeW + _colTagW + _splitW, headerRect.y, _splitW, headerRect.height);

                // type|tag splitter
                StyledConsoleUtil.DrawVSplitter(
                    rTypeRight,
                    dx => { _dragTypeSplit = true; _dragStartX += dx; _colTypeW = Mathf.Max(_minColW, _colTypeW + dx); Repaint(); },
                    () => { _dragTypeSplit = false; }
                );

                // tag|message splitter
                StyledConsoleUtil.DrawVSplitter(
                    rTagRight,
                    dx => { _dragTagSplit = true; _dragStartX += dx; _colTagW = Mathf.Max(_minColW, _colTagW + dx); Repaint(); },
                    () => { _dragTagSplit = false; }
                );

                // bottom hairline rule
                Handles.color = new Color(0, 0, 0, 0.2f);
                Handles.DrawLine(new Vector2(headerRect.x, headerRect.yMax - 1),
                                 new Vector2(headerRect.xMax, headerRect.yMax - 1));
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRowsAreaLayout(Rect rect)
        {
            // Sử dụng GUI.BeginScrollView trực tiếp - FIX cho vấn đề không hiển thị logs
            _scrollList = GUI.BeginScrollView(rect, _scrollList, new Rect(0, 0, rect.width - 20, _visible.Count * 22));

            if (_visible.Count == 0)
            {
                GUI.Label(new Rect(10, 10, rect.width - 30, 20), "No logs to show. Check filters or search.", EditorStyles.miniLabel);
                GUI.EndScrollView();
                return;
            }

            for (int i = 0; i < _visible.Count; i++)
            {
                var e = _visible[i];
                var rowRect = new Rect(0, i * 22, rect.width - 20, 22);

                // Background
                var bgStyle = (i == _selectedIndex) ? "SelectionRect" : (i % 2 == 0 ? "CN EntryBackOdd" : "CN EntryBackEven");
                GUI.Box(rowRect, "", bgStyle);

                // Icon
                var iconRect = new Rect(2, rowRect.y + 2, 18, 18);
                var icon = e.type == LogType.Error ? _iconError :
                          e.type == LogType.Warning ? _iconWarn : _iconInfo;
                if (icon != null && icon.image != null) GUI.DrawTexture(iconRect, icon.image);

                // Type
                var typeRect = new Rect(_colIconW, rowRect.y, _colTypeW - _colIconW, rowRect.height);
                GUI.Label(typeRect, e.type.ToString(), EditorStyles.miniLabel);

                // Tag  
                var tagRect = new Rect(_colTypeW, rowRect.y, _colTagW, rowRect.height);
                GUI.Label(tagRect, e.tag, EditorStyles.miniLabel);

                // Message
                var msgRect = new Rect(_colTypeW + _colTagW, rowRect.y, rowRect.width - _colTypeW - _colTagW, rowRect.height);
                var msgStyle = new GUIStyle(EditorStyles.label) { richText = true, font = e.font };

                if (_collapse && e.count > 1)
                {
                    var textRect = new Rect(msgRect.x, msgRect.y, msgRect.width - 32, msgRect.height);
                    var countRect = new Rect(msgRect.xMax - 32, msgRect.y, 32, msgRect.height);
                    GUI.Label(textRect, e.rich, msgStyle);
                    GUI.Label(countRect, $"x{e.count}", EditorStyles.miniLabel);
                }
                else
                {
                    GUI.Label(msgRect, e.rich, msgStyle);
                }

                // Handle clicks
                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    _selectedIndex = i;
                    Repaint();
                    if (Event.current.clickCount == 2) StyledConsoleUtil.OpenFirstUserFrame(e.stack);
                    Event.current.Use();
                }

                if (Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Open Callsite"), false, () => StyledConsoleUtil.OpenFirstUserFrame(e.stack));
                    menu.AddItem(new GUIContent("Copy Message"), false, () => EditorGUIUtility.systemCopyBuffer = e.rich ?? "");
                    menu.AddItem(new GUIContent("Copy Stacktrace"), false, () => EditorGUIUtility.systemCopyBuffer = e.stack ?? "");
                    menu.ShowAsContext();
                    Event.current.Use();
                }
            }

            GUI.EndScrollView();
        }

        private void DrawStackPaneLayout(Rect rect)
        {
            GUILayout.BeginArea(rect);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.Label("Stack Trace", EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Copy", EditorStyles.toolbarButton))
                    {
                        var e = SelectedEntry();
                        EditorGUIUtility.systemCopyBuffer = e?.stack ?? "";
                    }
                }

                var stack = SelectedEntry()?.stack ?? "";
                _scrollStack = EditorGUILayout.BeginScrollView(_scrollStack);
                var st = new GUIStyle(EditorStyles.helpBox) { richText = false, wordWrap = false };
                GUILayout.Label(string.IsNullOrEmpty(stack) ? "<no stacktrace>" : stack, st, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }


        private Entry SelectedEntry()
        {
            var list = ActiveList();
            if (_selectedIndex < 0 || _selectedIndex >= list.Count) return null;
            return list[_selectedIndex];
        }

        private void DrawStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"Log: {_countLog}", EditorStyles.miniLabel);
                GUILayout.Space(10);
                GUILayout.Label($"Warning: {_countWarn}", EditorStyles.miniLabel);
                GUILayout.Space(10);
                GUILayout.Label($"Error: {_countError}", EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();
                GUILayout.Label(_collapse ? "Collapsed view" : "Full view", EditorStyles.miniLabel);
            }
        }
    }
}
