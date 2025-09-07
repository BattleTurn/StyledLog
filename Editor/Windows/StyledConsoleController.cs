using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEditor.Compilation;

namespace BattleTurn.StyledLog.Editor
{
    /// <summary>
    /// Controller for Styled Console: holds global log storage and per-window view state, handles input actions.
    /// </summary>
    internal sealed class StyledConsoleController
    {
        // Backlog import guard (ensures we only import existing Unity console entries once)
        private static bool _importedUnityBacklog;

        [System.Serializable]
        private class Entry
        {
            public LogType type;
            public string tag;
            public string rich; // message text stripped of font tags
            public Font font;
            public string stack;
            public int count = 1; // collapse count
            public string message => rich; // legacy alias used by backlog import
        }
        private static readonly List<Entry> s_all = new();
        private static readonly List<Entry> s_collapsed = new();
        private static readonly Dictionary<string, Entry> s_collapseIndex = new();
        // Track compiler messages already ingested (key: type|file|line|col|msg)
    private static readonly HashSet<string> s_compilerKeys = new();
        // Persist compiler diagnostics across domain reload even when ClearOnRecompile is ON
        private const string SessionKey_CompilerSnapshot = "StyledConsole.CompilerSnapshot";

        [System.Serializable]
        private class CompilerMsgDTO { public int type; public string file; public int line; public int col; public string message; }

        // Import existing Unity Console entries present before our hook (e.g., early startup warnings like ADB)
        public static void ImportUnityBacklog()
        {
            if (_importedUnityBacklog) return;
            _importedUnityBacklog = true;
            try
            {
                var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
                var logEntryType = Type.GetType("UnityEditor.LogEntry, UnityEditor.dll");
                if (logEntriesType == null || logEntryType == null) return;

                var getCount = logEntriesType.GetMethod("GetCount", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                var startGetting = logEntriesType.GetMethod("StartGettingEntries", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                var endGetting = logEntriesType.GetMethod("EndGettingEntries", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                var getEntry = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                if (getCount == null || startGetting == null || endGetting == null || getEntry == null) return;

                int total = (int)getCount.Invoke(null, null);
                if (total <= 0) return;
                startGetting.Invoke(null, null);
                var entry = Activator.CreateInstance(logEntryType);
                var conditionField = logEntryType.GetField("condition");
                var stackField = logEntryType.GetField("stackTrace");
                var modeField = logEntryType.GetField("mode");
                // mode bits guess (Unity internal): 1 error, 2 assert, 4 log, 8 warning, 16 fatal
                for (int i = 0; i < total; i++)
                {
                    object[] args = { i, entry };
                    bool ok = (bool)getEntry.Invoke(null, args);
                    if (!ok) continue;
                    string condition = conditionField?.GetValue(entry) as string ?? string.Empty;
                    string stack = stackField?.GetValue(entry) as string ?? string.Empty;
                    int mode = modeField != null ? (int)modeField.GetValue(entry) : 0;
                    LogType t = LogType.Log;
                    const int k_Error = 1, k_Assert = 2, k_Log = 4, k_Warning = 8, k_Fatal = 16;
                    if ((mode & k_Error) != 0 || (mode & k_Assert) != 0 || (mode & k_Fatal) != 0) t = LogType.Error;
                    else if ((mode & k_Warning) != 0) t = LogType.Warning;
                    else if ((mode & k_Log) != 0) t = LogType.Log;
                    if (s_all.Exists(e => e.rich == condition && e.type == t)) continue; // already captured
                    AddLog("Unity", condition, t, stack);
                }
                endGetting.Invoke(null, null);
            }
            catch { }
        }
        [System.Serializable]
        private class CompilerMsgListDTO { public List<CompilerMsgDTO> list = new(); }

        // prefs keys
        private const string PrefKey_ClearOnPlay = "StyledConsole.ClearOnPlay";
        private const string PrefKey_ClearOnBuild = "StyledConsole.ClearOnBuild";
        private const string PrefKey_ClearOnRecompile = "StyledConsole.ClearOnRecompile";
        private const string PrefKey_LiveCompilerSync = "StyledConsole.LiveCompilerSync";
        private static bool s_clearOnPlay = true;
        private static bool s_clearOnBuild = true;
        private static bool s_clearOnRecompile = false;
        private static bool s_liveCompilerSync = true; // new: auto sync compiler diagnostics while compiling
        private static bool s_prefsLoaded;

        internal static bool ClearOnPlay => s_clearOnPlay;
        internal static bool ClearOnBuild => s_clearOnBuild;
        internal static bool ClearOnRecompile => s_clearOnRecompile;
        internal static bool LiveCompilerSync => s_liveCompilerSync;

        internal static event System.Action Cleared;
        internal static void RaiseCleared() => Cleared?.Invoke();
        // Fired when new entries are appended (logs or compiler) without clearing existing storage
        internal static event System.Action Changed;

        // snapshot keys
        private const string SessionKey_Snapshot = "StyledConsole.Snapshot";
        private const string SessionKey_HasSnapshot = "StyledConsole.HasSnapshot";

        internal static void EnsurePrefsLoaded()
        {
            if (s_prefsLoaded) return;
            s_clearOnPlay = EditorPrefs.GetBool(PrefKey_ClearOnPlay, true);
            s_clearOnBuild = EditorPrefs.GetBool(PrefKey_ClearOnBuild, true);
            s_clearOnRecompile = EditorPrefs.GetBool(PrefKey_ClearOnRecompile, false);
            s_liveCompilerSync = EditorPrefs.GetBool(PrefKey_LiveCompilerSync, true);
            s_prefsLoaded = true;
        }

        internal static void TogglePref_ClearOnPlay() { s_clearOnPlay = !s_clearOnPlay; EditorPrefs.SetBool(PrefKey_ClearOnPlay, s_clearOnPlay); }
        internal static void TogglePref_ClearOnBuild() { s_clearOnBuild = !s_clearOnBuild; EditorPrefs.SetBool(PrefKey_ClearOnBuild, s_clearOnBuild); }
        internal static void TogglePref_ClearOnRecompile() { s_clearOnRecompile = !s_clearOnRecompile; EditorPrefs.SetBool(PrefKey_ClearOnRecompile, s_clearOnRecompile); }
        internal static void TogglePref_LiveCompilerSync() { s_liveCompilerSync = !s_liveCompilerSync; EditorPrefs.SetBool(PrefKey_LiveCompilerSync, s_liveCompilerSync); }

        // emit
        internal static void AddLog(string tag, string richWithFont, LogType type, string stack)
        {
            // Normalize Exception to Error per requirement
            if (type == LogType.Exception) type = LogType.Error;
            // resolve per-row Font from StyleSetting (for IMGUI)
            Font font = null;
            var mgr = StyledDebug.StyledLogManager;
            if (mgr != null)
            {
                var s = mgr[tag];
                if (s != null && s.Font != null) font = s.Font;
            }

            var msg = StyledConsoleEditorGUI.StripFontTags(richWithFont);
            var e = new Entry
            {
                type = type,
                tag = string.IsNullOrEmpty(tag) ? "default" : tag,
                rich = msg,
                font = font,
                stack = stack
            };

            s_all.Add(e);
            // update collapsed cache too so it stays in sync
            AddCollapsed(e);
            // Notify listeners that new entry appended
            Changed?.Invoke();
        }

        // Sync current compiler messages (warnings + errors) into log list
    internal static void SyncCompilerMessages()
        {
            CompilerMessage[] msgs = null;
            try { msgs = (CompilerMessage[])typeof(CompilationPipeline).GetProperty("compilerMessages")?.GetValue(null, null); } catch { }
            if (msgs == null)
            {
                var mi = typeof(CompilationPipeline).GetMethod("GetMessages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (mi != null)
                {
                    try { msgs = mi.Invoke(null, null) as CompilerMessage[]; } catch { }
                }
            }
            if (msgs == null) return;
            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            bool addedAny = false;
            for (int i = 0; i < msgs.Length; i++)
            {
                var cm = msgs[i];
                LogType lt;
                if (cm.type == CompilerMessageType.Error) lt = LogType.Error;
                else if (cm.type == CompilerMessageType.Warning) lt = LogType.Warning;
                else continue; // ignore others
                string file = cm.file?.Replace('\\', '/') ?? string.Empty;
                if (!string.IsNullOrEmpty(file) && file.StartsWith(projectRoot)) file = file.Substring(projectRoot.Length + 1);
                string key = $"{(int)lt}|{file}|{cm.line}|{cm.column}|{cm.message}";
                if (s_compilerKeys.Contains(key)) continue;
                s_compilerKeys.Add(key);
                string formatted = string.IsNullOrEmpty(file)
                    ? cm.message
                    : $"{file}({cm.line},{cm.column}): {(lt == LogType.Error ? "error" : "warning")}: {cm.message}";
                // Synthetic stack frame so stacktrace pane has clickable link
                string syntheticStack = string.IsNullOrEmpty(file) ? string.Empty : $"Compiler (at {file}:{cm.line})";
                var e = new Entry { type = lt, tag = "Compiler", rich = formatted, font = null, stack = syntheticStack, count = 1 };
                s_all.Add(e);
                AddCollapsed(e);
                addedAny = true;
            }
            if (addedAny)
            {
                SaveCompilerDiagnosticsSnapshot();
                Changed?.Invoke();
            }
        }

        private static string CollapseKey(Entry e) => $"{(int)e.type}|{e.tag}|{e.rich}";
        private static void AddCollapsed(Entry e)
        {
            var key = CollapseKey(e);
            if (s_collapseIndex.TryGetValue(key, out var hit))
            {
                hit.count++;
            }
            else
            {
                var clone = new Entry { type = e.type, tag = e.tag, rich = e.rich, font = e.font, stack = e.stack, count = 1 };
                s_collapseIndex[key] = clone;
                s_collapsed.Add(clone);
            }
        }

        internal static void RebuildCollapsed()
        {
            s_collapsed.Clear();
            s_collapseIndex.Clear();
            foreach (var e in s_all) AddCollapsed(e);
        }

        internal static void ClearAllStorage()
        {
            s_all.Clear();
            s_collapsed.Clear();
            s_collapseIndex.Clear();
            Cleared?.Invoke();

            SessionState.SetBool(SessionKey_HasSnapshot, false);
            SessionState.SetString(SessionKey_Snapshot, "");
        }

        // Manual clear (user pressed Clear button): remove non-compiler logs but KEEP current compiler diagnostics
        // This mirrors Unity Console: compile errors & warnings persist until they are actually fixed.
        internal static void ClearPreserveCompileErrors()
        {
            if (s_all.Count == 0)
            {
                Cleared?.Invoke();
                return;
            }

            // Collect existing compiler entries (already in storage). We reuse them; no need to query pipeline again.
            var preserved = new List<Entry>();
            for (int i = 0; i < s_all.Count; i++)
            {
                var e = s_all[i];
                if (e.tag == "Compiler") preserved.Add(e);
            }

            // Rebuild lists keeping only preserved compiler entries.
            s_all.Clear();
            s_collapsed.Clear();
            s_collapseIndex.Clear();
            // IMPORTANT: do NOT clear s_compilerKeys; those keys still valid so future Sync doesn't duplicate.

            foreach (var e in preserved)
            {
                s_all.Add(e);
                AddCollapsed(e);
            }

            Cleared?.Invoke();

            // Clearing snapshot of regular logs; compiler snapshot will be recreated on next sync if needed.
            SessionState.SetBool(SessionKey_HasSnapshot, false);
            SessionState.SetString(SessionKey_Snapshot, string.Empty);
        }

        internal static void SaveSnapshot()
        {
            try
            {
                var dto = new SnapshotDTO { all = new List<EntryDTO>(s_all.Count) };
                foreach (var e in s_all)
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

                s_all.Clear();
                s_collapsed.Clear();
                s_collapseIndex.Clear();

                foreach (var d in dto.all)
                {
                    var logType = (LogType)d.type;
                    if (logType == LogType.Exception) logType = LogType.Error;
                    var e = new Entry
                    {
                        type = logType,
                        tag = d.tag,
                        rich = d.rich,
                        font = null,
                        stack = d.stack,
                        count = d.count <= 0 ? 1 : d.count
                    };
                    s_all.Add(e);
                }
                RebuildCollapsed();
                return true;
            }
            catch { return false; }
        }

    internal static void AddCompilerMessages(IEnumerable<CompilerMessage> msgs)
        {
            if (msgs == null) return;
            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            bool addedAny = false;
            foreach (var cm in msgs)
            {
                LogType lt;
                if (cm.type == CompilerMessageType.Error) lt = LogType.Error;
                else if (cm.type == CompilerMessageType.Warning) lt = LogType.Warning;
                else continue;
                string file = cm.file?.Replace('\\', '/') ?? string.Empty;
                if (!string.IsNullOrEmpty(file) && file.StartsWith(projectRoot)) file = file.Substring(projectRoot.Length + 1);
                string key = $"{(int)lt}|{file}|{cm.line}|{cm.column}|{cm.message}";
                if (s_compilerKeys.Contains(key)) continue;
                s_compilerKeys.Add(key);
                string formatted = string.IsNullOrEmpty(file)
                    ? cm.message
                    : $"{file}({cm.line},{cm.column}): {(lt == LogType.Error ? "error" : "warning")}: {cm.message}";
                string syntheticStack = string.IsNullOrEmpty(file) ? string.Empty : $"Compiler (at {file}:{cm.line})";
                var e = new Entry { type = lt, tag = "Compiler", rich = formatted, font = null, stack = syntheticStack, count = 1 };
                s_all.Add(e);
                AddCollapsed(e);
                addedAny = true;
            }
            if (addedAny)
            {
                SaveCompilerDiagnosticsSnapshot();
                Changed?.Invoke();
            }
        }

        internal static void ClearCompilerMessages()
        {
            if (s_all.Count == 0) return;
            bool removed = false;
            for (int i = s_all.Count - 1; i >= 0; i--)
            {
                if (s_all[i].tag == "Compiler") { s_all.RemoveAt(i); removed = true; }
            }
            if (removed)
            {
                RebuildCollapsed();
                s_compilerKeys.Clear();
                Cleared?.Invoke();
                // Also clear persisted snapshot
                SessionState.SetString(SessionKey_CompilerSnapshot, string.Empty);
            }
        }

        private static void SaveCompilerDiagnosticsSnapshot()
        {
            try
            {
                var dto = new CompilerMsgListDTO();
                for (int i = 0; i < s_all.Count; i++)
                {
                    var e = s_all[i];
                    if (e.tag != "Compiler") continue;
                    // Attempt to extract file/line/col from rich message pattern: file(line,col): error/warning: message
                    string file = string.Empty; int line = 0; int col = 0; string msg = e.rich;
                    int paren = e.rich.IndexOf('(');
                    int close = e.rich.IndexOf(')');
                    int colon = e.rich.IndexOf(':');
                    if (paren > 0 && close > paren && colon > close)
                    {
                        file = e.rich.Substring(0, paren);
                        var inside = e.rich.Substring(paren + 1, close - paren - 1);
                        var parts = inside.Split(',');
                        if (parts.Length >= 2) { int.TryParse(parts[0], out line); int.TryParse(parts[1], out col); }
                        msg = e.rich.Substring(colon + 1).Trim();
                    }
                    dto.list.Add(new CompilerMsgDTO { type = e.type == LogType.Error ? 2 : (e.type == LogType.Warning ? 1 : 0), file = file, line = line, col = col, message = msg });
                }
                var json = JsonUtility.ToJson(dto);
                SessionState.SetString(SessionKey_CompilerSnapshot, json ?? string.Empty);
            }
            catch { /* ignore */ }
        }

    internal static void LoadCompilerDiagnosticsSnapshot()
        {
            try
            {
                var json = SessionState.GetString(SessionKey_CompilerSnapshot, string.Empty);
                if (string.IsNullOrEmpty(json)) return;
                var dto = JsonUtility.FromJson<CompilerMsgListDTO>(json);
                if (dto?.list == null || dto.list.Count == 0) return;
                bool addedAny = false;
                foreach (var m in dto.list)
                {
                    LogType lt = m.type == 2 ? LogType.Error : LogType.Warning; // we only stored errors & warnings
                    string key = $"{(int)lt}|{m.file}|{m.line}|{m.col}|{m.message}";
                    if (s_compilerKeys.Contains(key)) continue;
                    s_compilerKeys.Add(key);
                    string formatted = string.IsNullOrEmpty(m.file)
                        ? m.message
                        : $"{m.file}({m.line},{m.col}): {(lt == LogType.Error ? "error" : "warning")}: {m.message}";
                    string syntheticStack = string.IsNullOrEmpty(m.file) ? string.Empty : $"Compiler (at {m.file}:{m.line})";
                    var e = new Entry { type = lt, tag = "Compiler", rich = formatted, font = null, stack = syntheticStack, count = 1 };
                    s_all.Add(e);
                    AddCollapsed(e);
                    addedAny = true;
                }
                if (addedAny) Changed?.Invoke();
            }
            catch { /* ignore */ }
        }

        internal static void ComputeCounts(out int logs, out int warns, out int errors)
        {
            int l = 0, w = 0, e = 0;
            for (int i = 0; i < s_all.Count; i++)
            {
                var t = s_all[i].type;
                if (t == LogType.Log) l++;
                else if (t == LogType.Warning) w++;
                else if (t == LogType.Error) e++;
            }
            logs = l; warns = w; errors = e;
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // Live compiler polling (redundant safety if some events miss per user's environment)
        private static double s_nextCompilerPollTime; // EditorApplication.timeSinceStartup timestamp
        internal static void LiveCompilerUpdate()
        {
            if (!s_liveCompilerSync) return;
            // Only poll while compiling OR shortly (2s) after finish to catch stragglers
            bool compiling = UnityEditor.EditorApplication.isCompiling;
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (!compiling && now > s_nextCompilerPollTime + 2.0) return;
            if (now < s_nextCompilerPollTime) return;
            s_nextCompilerPollTime = now + 0.5; // poll every 0.5s
            SyncCompilerMessages();
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // Per-window view state and input handling

        internal bool ShowLog = true, ShowWarn = true, ShowError = true;
        internal bool Collapse = false;
        internal string Search = string.Empty;
        internal int SelectedIndex = -1;
        // Tag filter persistence and semantics
        // - TagEverything: when true and no explicit selections, treat all tags as enabled (including future tags)
        // - When false and no explicit selections, treat all tags as disabled
        // - When explicit selections exist, use them regardless of TagEverything
        internal bool TagEverything = true;
        // Tag filter set: when non-empty, only rows whose tag is in this set are visible
        private readonly HashSet<string> _enabledTags = new HashSet<string>();
        internal bool HasExplicitTagSelection => _enabledTags.Count > 0;
        internal void SetTagEnabled(string tag, bool enabled)
        {
            if (string.IsNullOrEmpty(tag)) return;
            if (enabled) _enabledTags.Add(tag); else _enabledTags.Remove(tag);
        }
        internal bool GetTagEnabled(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return true;
            if (_enabledTags.Count == 0) return TagEverything;
            return _enabledTags.Contains(tag);
        }
        internal void EnableAllTags(IEnumerable<string> tags)
        {
            _enabledTags.Clear();
            foreach (var t in tags) if (!string.IsNullOrEmpty(t)) _enabledTags.Add(t);
        }
        internal void ClearTagFilters() => _enabledTags.Clear();

        // Persistence for tag prefs (per project)
        [System.Serializable]
        private class TagPrefsDTO { public bool everything = true; public List<string> enabled = new List<string>(); }
        private static string GetTagPrefsKey() => "StyledConsole.TagPrefs." + Application.dataPath;
        internal void LoadTagPrefs()
        {
            try
            {
                var json = EditorPrefs.GetString(GetTagPrefsKey(), string.Empty);
                if (string.IsNullOrEmpty(json)) { TagEverything = true; _enabledTags.Clear(); return; }
                var dto = JsonUtility.FromJson<TagPrefsDTO>(json);
                if (dto == null) { TagEverything = true; _enabledTags.Clear(); return; }
                TagEverything = dto.everything;
                _enabledTags.Clear();
                if (dto.enabled != null)
                {
                    foreach (var t in dto.enabled) if (!string.IsNullOrEmpty(t)) _enabledTags.Add(t);
                }
            }
            catch { TagEverything = true; _enabledTags.Clear(); }
        }
        internal void SaveTagPrefs()
        {
            try
            {
                var dto = new TagPrefsDTO { everything = TagEverything, enabled = new List<string>(_enabledTags) };
                var json = JsonUtility.ToJson(dto);
                EditorPrefs.SetString(GetTagPrefsKey(), json ?? string.Empty);
            }
            catch { /* ignore */ }
        }
        internal void SetEverything(bool on)
        {
            TagEverything = on;
            _enabledTags.Clear();
        }

        private readonly List<Entry> _visible = new();

        internal void BuildVisible()
        {
            _visible.Clear();
            var list = Collapse ? s_collapsed : s_all;

            string s = Search?.Trim();
            bool hasSearch = !string.IsNullOrEmpty(s);
            if (hasSearch) s = s.ToLowerInvariant();

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                // Tag filter:
                if (_enabledTags.Count == 0)
                {
                    if (!TagEverything) continue; // all off
                }
                else
                {
                    if (!string.IsNullOrEmpty(e.tag) && !_enabledTags.Contains(e.tag)) continue;
                }
                if ((e.type == LogType.Log && !ShowLog) ||
                    (e.type == LogType.Warning && !ShowWarn) ||
                    (e.type == LogType.Error && !ShowError))
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

            if (_visible.Count > 0 && (SelectedIndex < 0 || SelectedIndex >= _visible.Count))
                SelectedIndex = 0;
        }

        internal int GetVisibleCount() => _visible.Count;

        internal void GetVisibleRow(int index, out LogType type, out string tag, out string rich, out Font font, out int count, out string stack)
        {
            var e = _visible[index];
            type = e.type; tag = e.tag; rich = e.rich; font = e.font; count = e.count; stack = e.stack;
        }

        private Entry SelectedEntry()
        {
            if (SelectedIndex < 0 || SelectedIndex >= _visible.Count) return null;
            return _visible[SelectedIndex];
        }

        internal string SelectedStack() => SelectedEntry()?.stack ?? string.Empty;

        internal void HandleRowMouseDown(int index, int clickCount)
        {
            SelectedIndex = index;
            if (clickCount == 2)
            {
                var e = SelectedEntry();
                if (e != null) StyledConsoleEditorGUI.OpenFirstUserFrame(e.stack);
            }
        }

        internal void HandleRowContextMenu(int index)
        {
            if (index < 0 || index >= _visible.Count) return;
            var e = _visible[index];
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Open Callsite"), false, () => StyledConsoleEditorGUI.OpenFirstUserFrame(e.stack));
            menu.AddItem(new GUIContent("Copy Message"), false, () => EditorGUIUtility.systemCopyBuffer = e.rich ?? "");
            menu.AddItem(new GUIContent("Copy Stacktrace"), false, () => EditorGUIUtility.systemCopyBuffer = e.stack ?? "");
            menu.ShowAsContext();
        }

        // ───────────────────────────────────────────────────────────────────────────────
        // Stack parsing and navigation utilities

        private static readonly Regex s_rxIn = new Regex(@"^\s*at\s+(.*?)\s+in\s+(.+?):line\s+(\d+)", RegexOptions.Compiled);
        private static readonly Regex s_rxIn2 = new Regex(@"^\s*at\s+(.*?)\s+(?:\[[^\]]*\]\s+)?in\s+(.+?):(\d+)", RegexOptions.Compiled);
        private static readonly Regex s_rxAt = new Regex(@"^\s*(.*?)\s+\(at\s+(.+?):(\d+)\)", RegexOptions.Compiled);

        internal static List<Frame> ParseStackFrames(string stack)
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
            var m = s_rxIn.Match(line);
            if (m.Success)
            {
                frame = new Frame { raw = line, method = m.Groups[1].Value, path = m.Groups[2].Value, line = SafeParseInt(m.Groups[3].Value) };
                return true;
            }
            m = s_rxIn2.Match(line);
            if (m.Success)
            {
                frame = new Frame { raw = line, method = m.Groups[1].Value, path = m.Groups[2].Value, line = SafeParseInt(m.Groups[3].Value) };
                return true;
            }
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
            var p = path.Replace('\\', '/');
            if (p.Contains("/Assets/")) return true;
            if (p.Contains("/Packages/") && !p.Contains("/Packages/com.unity.")) return true;
            return !(p.Contains("/Editor/Data/") || p.Contains("/Editor/"));
        }

        private static int SafeParseInt(string s) => int.TryParse(s, out var v) ? v : 0;

        private static string MakeDisplay(string path, int line)
        {
            if (string.IsNullOrEmpty(path)) return "<unknown>";
            var p = path.Replace('\\', '/');
            string shortP = p;
            int idx = p.IndexOf("/Assets/");
            if (idx >= 0) shortP = p.Substring(idx + 1);
            else
            {
                idx = p.IndexOf("/Packages/");
                if (idx >= 0) shortP = p.Substring(idx + 1);
                else
                {
                    int slash = p.LastIndexOf('/');
                    if (slash >= 0) shortP = p.Substring(slash + 1);
                }
            }
            return line > 0 ? $"{shortP}:{line}" : shortP;
        }

        internal static Frame GetFirstUserFrame(List<Frame> frames)
        {
            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i].isUser && frames[i].line > 0 && !string.IsNullOrEmpty(frames[i].path))
                    return frames[i];
            }
            return null;
        }

        internal static void OpenFrame(Frame f)
        {
            if (f == null || string.IsNullOrEmpty(f.path)) return;
            var abs = NormalizeToAbsolutePath(f.path);
            var rel = AbsoluteToUnityPath(abs);
            if (!string.IsNullOrEmpty(rel))
            {
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rel);
                if (obj != null) { AssetDatabase.OpenAsset(obj, Mathf.Max(1, f.line)); return; }
            }
            InternalEditorUtility.OpenFileAtLineExternal(abs, Mathf.Max(1, f.line));
        }

        internal static void OpenAbsolutePath(string absOrRel, int line)
        {
            if (string.IsNullOrEmpty(absOrRel)) return;
            var abs = NormalizeToAbsolutePath(absOrRel);
            var rel = AbsoluteToUnityPath(abs);
            if (!string.IsNullOrEmpty(rel))
            {
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rel);
                if (obj != null) { AssetDatabase.OpenAsset(obj, Mathf.Max(1, line)); return; }
            }
            InternalEditorUtility.OpenFileAtLineExternal(abs, Mathf.Max(1, line));
        }

        internal static string NormalizeToAbsolutePath(string path)
        {
            var p = path.Replace('\\', '/');
            if (System.IO.Path.IsPathRooted(p)) return p;
            var proj = System.IO.Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(proj, p)).Replace('\\', '/');
        }

        private static string AbsoluteToUnityPath(string abs)
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
