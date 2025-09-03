// StyledConsoleWindow.cs
// Table view + search + resizable columns + selection + bottom stacktrace pane
// + double-click to open callsite, status bar counters, auto-scroll, collapse.
//
// Put this file in an Editor folder.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditorInternal;
using UnityEditor.Callbacks;
using UnityEditor.Build.Reporting;
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

        // persistent clear options
        private const string PrefKey_ClearOnPlay = "StyledConsole.ClearOnPlay";
        private const string PrefKey_ClearOnBuild = "StyledConsole.ClearOnBuild";
        private const string PrefKey_ClearOnRecompile = "StyledConsole.ClearOnRecompile";

        private static bool s_clearOnPlay = true;    // defaults match Unity Console
        private static bool s_clearOnBuild = true;
        private static bool s_clearOnRecompile = false;
        private static bool s_prefsLoaded;

        // expose read-only for hooks
        internal static bool ClearOnPlay => s_clearOnPlay;
        internal static bool ClearOnBuild => s_clearOnBuild;
        internal static bool ClearOnRecompile => s_clearOnRecompile;

        // notified when storage cleared (to update any open windows)
        internal static event System.Action Cleared;
        internal static void RaiseCleared() { Cleared?.Invoke(); }

        // session-state keys for snapshot across domain reload
        private const string SessionKey_Snapshot = "StyledConsole.Snapshot";
        private const string SessionKey_HasSnapshot = "StyledConsole.HasSnapshot";

        // load prefs lazily to avoid calling EditorPrefs during ScriptableObject construction
        internal static void EnsurePrefsLoaded()
        {
            if (s_prefsLoaded) return;
            s_clearOnPlay = EditorPrefs.GetBool(PrefKey_ClearOnPlay, true);
            s_clearOnBuild = EditorPrefs.GetBool(PrefKey_ClearOnBuild, true);
            s_clearOnRecompile = EditorPrefs.GetBool(PrefKey_ClearOnRecompile, false);
            s_prefsLoaded = true;
        }

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

        // tooltip state to avoid flicker
        private string _ttAbsPath;
        private int _ttLine;

        [MenuItem("Tools/StyledDebug/Styled Console")]
        public static void Open()
        {
            var w = GetWindow<StyledConsoleWindow>("Styled Console");
            w.minSize = new Vector2(760, 380);
            w.Show();
        }

        private void OnEnable()
        {
            StyledConsoleWindow.EnsurePrefsLoaded();
            _iconInfo = EditorGUIUtility.IconContent("console.infoicon");
            _iconWarn = EditorGUIUtility.IconContent("console.warnicon");
            _iconError = EditorGUIUtility.IconContent("console.erroricon");

            // Unsubscribe first to avoid double subscription
            StyledDebug.onEmit -= OnEmit;
            StyledDebug.onEmit += OnEmit;

            // react to external clears
            Cleared -= HandleCleared;
            Cleared += HandleCleared;

            // if we re-opened after a reload and snapshot exists, restore it
            if (!s_clearOnRecompile)
            {
                if (LoadSnapshot())
                {
                    RaiseCleared();
                }
            }
        }

        private void OnDisable()
        {
            StyledDebug.onEmit -= OnEmit;
            Cleared -= HandleCleared;
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
            var msg = StyledConsoleEditorGUI.StripFontTags(richWithFont);

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

            // persist snapshot so logs survive recompile when option is off
            EnsurePrefsLoaded();
            if (!s_clearOnRecompile)
            {
                SaveSnapshot();
            }
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

        // central clear routine (used by button + hooks)
        internal static void ClearAllStorage()
        {
            _all.Clear();
            _collapsed.Clear();
            _collapseIndex.Clear();
            _visible.Clear();
            Cleared?.Invoke();

            // also clear any persisted snapshot
            SessionState.SetBool(SessionKey_HasSnapshot, false);
            SessionState.SetString(SessionKey_Snapshot, "");
        }

        private void HandleCleared()
        {
            _countLog = _countWarn = _countError = 0;
            _selectedIndex = -1;
            _scrollList = Vector2.zero;
            _autoScrollRequest = false;
            Repaint();
        }

        // Serialize current logs into SessionState to survive domain reload when "Clear on Recompile" is off
        internal static void SaveSnapshot()
        {
            try
            {
                var dto = new SnapshotDTO { all = new List<EntryDTO>(_all.Count) };
                foreach (var e in _all)
                {
                    dto.all.Add(new EntryDTO
                    {
                        type = (int)e.type,
                        tag = e.tag,
                        rich = e.rich,
                        stack = e.stack,
                        count = e.count
                    });
                }
                var json = JsonUtility.ToJson(dto);
                SessionState.SetString(SessionKey_Snapshot, json ?? "");
                SessionState.SetBool(SessionKey_HasSnapshot, true);
            }
            catch { /* ignore */ }
        }

        // Restore snapshot if present; returns true if restored
        internal static bool LoadSnapshot()
        {
            try
            {
                if (!SessionState.GetBool(SessionKey_HasSnapshot, false)) return false;
                var json = SessionState.GetString(SessionKey_Snapshot, "");
                SessionState.SetBool(SessionKey_HasSnapshot, false);
                SessionState.SetString(SessionKey_Snapshot, "");
                if (string.IsNullOrEmpty(json)) return false;
                var dto = JsonUtility.FromJson<SnapshotDTO>(json);
                if (dto?.all == null) return false;

                _all.Clear();
                _collapsed.Clear();
                _collapseIndex.Clear();
                _visible.Clear();

                foreach (var d in dto.all)
                {
                    var e = new Entry
                    {
                        type = (LogType)d.type,
                        tag = d.tag,
                        rich = d.rich,
                        font = null,
                        stack = d.stack,
                        count = d.count <= 0 ? 1 : d.count
                    };
                    _all.Add(e);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        // persist toggle helper
        private static void TogglePref(string key, ref bool field)
        {
            field = !field;
            EditorPrefs.SetBool(key, field);
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
                StyledConsoleEditorGUI.DrawHSplitter(
                    splitRect,
                    dy =>
                    {
                        // Invert dy so dragging up increases the bottom pane (more stack), dragging down decreases it
                        float total = contentH;
                        float newStackH = Mathf.Clamp(stackH - dy, _minStackH, total - _minListH);
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

                _search = StyledConsoleEditorGUI.ToolbarSearch(_search, 180f);

                // Clear
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
                {
                    ClearAllStorage();
                }

                // Options dropdown (Clear on: Play/Build/Recompile)
                if (EditorGUILayout.DropdownButton(new GUIContent(""), FocusType.Passive, EditorStyles.toolbarDropDown))
                {
                    EnsurePrefsLoaded();
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Clear on Play"), s_clearOnPlay, () => TogglePref(PrefKey_ClearOnPlay, ref s_clearOnPlay));
                    menu.AddItem(new GUIContent("Clear on Build"), s_clearOnBuild, () => TogglePref(PrefKey_ClearOnBuild, ref s_clearOnBuild));
                    menu.AddItem(new GUIContent("Clear on Recompile"), s_clearOnRecompile, () => TogglePref(PrefKey_ClearOnRecompile, ref s_clearOnRecompile));
                    menu.ShowAsContext();
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
                StyledConsoleEditorGUI.DrawVSplitter(
                    rTypeRight,
                    dx => { _dragTypeSplit = true; _dragStartX += dx; _colTypeW = Mathf.Max(_minColW, _colTypeW + dx); Repaint(); },
                    () => { _dragTypeSplit = false; }
                );

                // tag|message splitter
                StyledConsoleEditorGUI.DrawVSplitter(
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
                    if (Event.current.clickCount == 2) StyledConsoleEditorGUI.OpenFirstUserFrame(e.stack);
                    Event.current.Use();
                }

                if (Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Open Callsite"), false, () => StyledConsoleEditorGUI.OpenFirstUserFrame(e.stack));
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
            // draw boxed background
            GUI.Box(rect, GUIContent.none);

            // toolbar
            float y = rect.y + 2f;
            var toolbarRect = new Rect(rect.x + 4f, y, rect.width - 8f, 20f);
            GUI.Box(toolbarRect, GUIContent.none, EditorStyles.toolbar);
            var lblRect = new Rect(toolbarRect.x + 6f, toolbarRect.y, 200f, toolbarRect.height);
            GUI.Label(lblRect, "Stack Trace", EditorStyles.miniBoldLabel);
            var copyRect = new Rect(toolbarRect.xMax - 60f, toolbarRect.y + 1f, 56f, toolbarRect.height - 2f);
            if (GUI.Button(copyRect, "Copy", EditorStyles.toolbarButton))
            {
                var e = SelectedEntry();
                EditorGUIUtility.systemCopyBuffer = e?.stack ?? string.Empty;
            }

            // parse frames
            var stack = SelectedEntry()?.stack ?? string.Empty;
            var frames = ParseStackFrames(stack);

            // callsite summary
            y = toolbarRect.yMax + 4f;
            var callsite = GetFirstUserFrame(frames);
            var callRect = new Rect(rect.x + 6f, y, rect.width - 12f, 18f);
            if (callsite != null)
            {
                GUI.Label(callRect, $"Callsite: {callsite.display}", EditorStyles.miniLabel);
                var openRect = new Rect(callRect.xMax - 52f, callRect.y - 1f, 48f, callRect.height);
                if (GUI.Button(openRect, "Open", EditorStyles.miniButton))
                    OpenFrame(callsite);
            }
            else
            {
                GUI.Label(callRect, "Callsite: <unknown>", EditorStyles.miniLabel);
            }

            // frames area
            float listTop = callRect.yMax + 2f;
            float listH = Mathf.Max(0f, rect.yMax - listTop - 4f);
            var viewRect = new Rect(rect.x + 2f, listTop, rect.width - 4f, listH);
            int lineH = 18;
            var contentRect = new Rect(0, 0, viewRect.width - 16f, Mathf.Max(listH, frames.Count * lineH));
            _scrollStack = GUI.BeginScrollView(viewRect, _scrollStack, contentRect);
            bool hoveringAny = false;

            if (frames.Count == 0)
            {
                var noneRect = new Rect(6f, 4f, contentRect.width - 12f, 18f);
                GUI.Label(noneRect, "<no stacktrace>", EditorStyles.helpBox);
            }
            else
            {
                var baseStyle = new GUIStyle(EditorStyles.label) { richText = false, wordWrap = false };
                var linkColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.85f, 0.5f) : new Color(0.05f, 0.6f, 0.25f);
                var linkStyle = new GUIStyle(baseStyle);
                linkStyle.normal.textColor = linkColor;

                for (int i = 0; i < frames.Count; i++)
                {
                    var f = frames[i];
                    var row = new Rect(6f, i * lineH, contentRect.width - 12f, lineH);

                    if (!string.IsNullOrEmpty(f.path) && f.line > 0)
                    {
                        // draw "Method (at "
                        string prefix = string.IsNullOrEmpty(f.method) ? "" : f.method;
                        prefix += " (at ";
                        var prefixSize = baseStyle.CalcSize(new GUIContent(prefix));
                        var prefixRect = new Rect(row.x, row.y, prefixSize.x, row.height);
                        GUI.Label(prefixRect, prefix, baseStyle);

                        // draw clickable link
                        string linkText = f.display;
                        var linkSize = linkStyle.CalcSize(new GUIContent(linkText));
                        var linkRect = new Rect(prefixRect.xMax, row.y, linkSize.x, row.height);
                        EditorGUIUtility.AddCursorRect(linkRect, MouseCursor.Link);
                        GUI.Label(linkRect, linkText, linkStyle);
                        // underline
                        var ul = new Rect(linkRect.x, linkRect.yMax - 1f, Mathf.Min(linkRect.width, row.xMax - linkRect.x), 1f);
                        EditorGUI.DrawRect(ul, linkColor);

                        // draw ")"
                        var postRect = new Rect(linkRect.xMax, row.y, 12f, row.height);
                        GUI.Label(postRect, ")", baseStyle);

                        bool hover = linkRect.Contains(Event.current.mousePosition);
                        if (hover)
                        {
                            hoveringAny = true;
                            var abs = NormalizeToAbsolutePath(f.path);
                            if (_ttAbsPath != abs || _ttLine != f.line)
                            {
                                _ttAbsPath = abs; _ttLine = f.line;
                            }

                            Vector2 rectPos = postRect.position;
                            rectPos.x -= 1; // Adjust for tooltip to left (for hover into tooltip hand preview code).
                            Vector2 linkScreenPoint = GUIUtility.GUIToScreenPoint(rectPos);
                            linkScreenPoint.y -= ConsoleCodeTooltip.MaxHeight / 2; // nudge above link
                            ConsoleCodeTooltip.ShowAtScreenRect(linkScreenPoint, this, abs, Mathf.Max(1, f.line));
                        }

                        if (Event.current.type == EventType.MouseDown && hover)
                        {
                            OpenFrame(f);
                            Event.current.Use();
                        }
                    }
                    else
                    {
                        // fallback: draw raw line (no link)
                        GUI.Label(row, f.raw, baseStyle);
                    }
                }
            }
            GUI.EndScrollView();

            // hide tooltip when not hovering any link
            if (!hoveringAny && Event.current.type == EventType.Repaint)
            {
                _ttAbsPath = null; _ttLine = 0;
                ConsoleCodeTooltip.HideIfOwner(this);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Stack parsing and navigation

        private static readonly Regex s_rxIn = new Regex(@"^\s*at\s+(.*?)\s+in\s+(.+?):line\s+(\d+)", RegexOptions.Compiled);
        private static readonly Regex s_rxIn2 = new Regex(@"^\s*at\s+(.*?)\s+(?:\[[^\]]*\]\s+)?in\s+(.+?):(\d+)", RegexOptions.Compiled);
        private static readonly Regex s_rxAt = new Regex(@"^\s*(.*?)\s+\(at\s+(.+?):(\d+)\)", RegexOptions.Compiled);

        private class Frame
        {
            public string raw;        // original line
            public string method;     // Namespace.Type.Method
            public string path;       // file path (absolute or relative)
            public int line;          // 1-based line
            public bool isUser;       // heuristic user code
            public string display;    // short display (Assets/..:line or file.cs:line)
        }

        private static List<Frame> ParseStackFrames(string stack)
        {
            var list = new List<Frame>();
            if (string.IsNullOrEmpty(stack)) return list;
            var lines = stack.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var ln in lines)
            {
                var line = ln.TrimEnd();
                if (TryParseFrame(line, out var f))
                {
                    f.isUser = IsUserMethod(f.method) || IsUserPath(f.path);
                    f.display = MakeDisplay(f.path, f.line);
                    list.Add(f);
                }
                else
                {
                    list.Add(new Frame { raw = line, method = line, path = null, line = 0, isUser = false, display = line });
                }
            }
            return list;
        }

        private static bool TryParseFrame(string line, out Frame frame)
        {
            // Matches: at Namespace.Type.Method(...) in C:\path\file.cs:line 123
            var m = s_rxIn.Match(line);
            if (m.Success)
            {
                frame = new Frame { raw = line, method = m.Groups[1].Value, path = m.Groups[2].Value, line = SafeParseInt(m.Groups[3].Value) };
                return true;
            }
            // Matches: at Namespace.Type.Method(...) [0x..] in C:\path\file.cs:123 (no 'line' keyword)
            m = s_rxIn2.Match(line);
            if (m.Success)
            {
                frame = new Frame { raw = line, method = m.Groups[1].Value, path = m.Groups[2].Value, line = SafeParseInt(m.Groups[3].Value) };
                return true;
            }
            // Matches: Namespace.Type.Method(...) (at Assets/Some.cs:123)
            m = s_rxAt.Match(line);
            if (m.Success)
            {
                frame = new Frame { raw = line, method = m.Groups[1].Value, path = m.Groups[2].Value, line = SafeParseInt(m.Groups[3].Value) };
                return true;
            }
            frame = null; return false;
        }

        private static bool IsUserMethod(string method)
        {
            if (string.IsNullOrEmpty(method)) return false;
            if (method.StartsWith("UnityEngine.")) return false;
            if (method.StartsWith("UnityEditor.")) return false;
            return true;
        }

        private static bool IsUserPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            // Consider anything under Assets or non-unity packages as user
            var p = path.Replace('\\', '/');
            if (p.Contains("/Assets/")) return true;
            if (p.Contains("/Packages/") && !p.Contains("/Packages/com.unity.")) return true;
            // absolute outside library also likely user
            return !(p.Contains("/Editor/Data/") || p.Contains("/Editor/"));
        }

        private static string MakeDisplay(string path, int line)
        {
            if (string.IsNullOrEmpty(path)) return "<unknown>";
            var p = path.Replace('\\', '/');
            string shortP = p;
            int idx = p.IndexOf("/Assets/");
            if (idx >= 0) shortP = p.Substring(idx + 1); // keep from Assets/...
            else
            {
                idx = p.IndexOf("/Packages/");
                if (idx >= 0) shortP = p.Substring(idx + 1);
                else
                {
                    // fall back to filename
                    int slash = p.LastIndexOf('/');
                    if (slash >= 0) shortP = p.Substring(slash + 1);
                }
            }
            return line > 0 ? $"{shortP}:{line}" : shortP;
        }

        private static Frame GetFirstUserFrame(List<Frame> frames)
        {
            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i].isUser && frames[i].line > 0 && !string.IsNullOrEmpty(frames[i].path))
                    return frames[i];
            }
            return null;
        }

        private static int SafeParseInt(string s)
        {
            if (int.TryParse(s, out var v)) return v; return 0;
        }

        private static void OpenFrame(Frame f)
        {
            if (f == null || string.IsNullOrEmpty(f.path)) return;
            var abs = NormalizeToAbsolutePath(f.path);
            var rel = AbsoluteToUnityPath(abs);
            if (!string.IsNullOrEmpty(rel))
            {
                var obj = AssetDatabase.LoadAssetAtPath<Object>(rel);
                if (obj != null) { AssetDatabase.OpenAsset(obj, Mathf.Max(1, f.line)); return; }
            }
            // fallback to external open
            InternalEditorUtility.OpenFileAtLineExternal(abs, Mathf.Max(1, f.line));
        }

        private static string NormalizeToAbsolutePath(string path)
        {
            var p = path.Replace('\\', '/');
            if (System.IO.Path.IsPathRooted(p)) return p;
            // under project
            var proj = System.IO.Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(proj, p)).Replace('\\', '/');
        }

        private static string AbsoluteToUnityPath(string abs)
        {
            if (string.IsNullOrEmpty(abs)) return null;
            abs = abs.Replace('\\', '/');
            var data = Application.dataPath.Replace('\\', '/');
            if (abs.StartsWith(data)) return "Assets" + abs.Substring(data.Length);
            // allow Packages/... direct
            int idx = abs.IndexOf("/Packages/");
            if (idx >= 0) return abs.Substring(idx + 1);
            return null;
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
                // compute counts from current data so restored snapshots show correct totals
                ComputeCounts(out var cLog, out var cWarn, out var cErr);
                GUILayout.Label($"Log: {cLog}", EditorStyles.miniLabel);
                GUILayout.Space(10);
                GUILayout.Label($"Warning: {cWarn}", EditorStyles.miniLabel);
                GUILayout.Space(10);
                GUILayout.Label($"Error: {cErr}", EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();
                GUILayout.Label(_collapse ? "Collapsed view" : "Full view", EditorStyles.miniLabel);
            }
        }

        private static void ComputeCounts(out int logs, out int warns, out int errors)
        {
            int l = 0, w = 0, e = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                var t = _all[i].type;
                if (t == LogType.Log) l++;
                else if (t == LogType.Warning) w++;
                else if (t == LogType.Error) e++;
            }
            logs = l; warns = w; errors = e;
        }
    }

    // global editor hooks
    [InitializeOnLoad]
    internal static class StyledConsoleHooks
    {
        static StyledConsoleHooks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            StyledConsoleWindow.EnsurePrefsLoaded();
            if (state == PlayModeStateChange.EnteredPlayMode && StyledConsoleWindow.ClearOnPlay)
            {
                StyledConsoleWindow.ClearAllStorage();
            }
        }

        private static void OnBeforeAssemblyReload()
        {
            StyledConsoleWindow.EnsurePrefsLoaded();
            if (StyledConsoleWindow.ClearOnRecompile)
            {
                StyledConsoleWindow.ClearAllStorage();
            }
            else
            {
                // persist snapshot so logs survive domain reload
                StyledConsoleWindow.SaveSnapshot();
            }
        }
    }

    // build hook to clear on build start
    internal sealed class StyledConsoleBuildHook : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            StyledConsoleWindow.EnsurePrefsLoaded();
            if (StyledConsoleWindow.ClearOnBuild)
            {
                StyledConsoleWindow.ClearAllStorage();
            }
        }
    }
}
