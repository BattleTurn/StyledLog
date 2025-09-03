using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace BattleTurn.StyledLog.Editor
{
    /// <summary>
    /// Controller for Styled Console: holds global log storage and per-window view state, handles input actions.
    /// </summary>
    internal sealed class StyledConsoleController
    {
        private static readonly List<Entry> s_all = new();
        private static readonly List<Entry> s_collapsed = new();
        private static readonly Dictionary<string, Entry> s_collapseIndex = new();

        // prefs keys
        private const string PrefKey_ClearOnPlay = "StyledConsole.ClearOnPlay";
        private const string PrefKey_ClearOnBuild = "StyledConsole.ClearOnBuild";
        private const string PrefKey_ClearOnRecompile = "StyledConsole.ClearOnRecompile";
        private static bool s_clearOnPlay = true;
        private static bool s_clearOnBuild = true;
        private static bool s_clearOnRecompile = false;
        private static bool s_prefsLoaded;

        internal static bool ClearOnPlay => s_clearOnPlay;
        internal static bool ClearOnBuild => s_clearOnBuild;
        internal static bool ClearOnRecompile => s_clearOnRecompile;

        internal static event System.Action Cleared;
        internal static void RaiseCleared() => Cleared?.Invoke();

        // snapshot keys
        private const string SessionKey_Snapshot = "StyledConsole.Snapshot";
        private const string SessionKey_HasSnapshot = "StyledConsole.HasSnapshot";

        internal static void EnsurePrefsLoaded()
        {
            if (s_prefsLoaded) return;
            s_clearOnPlay = EditorPrefs.GetBool(PrefKey_ClearOnPlay, true);
            s_clearOnBuild = EditorPrefs.GetBool(PrefKey_ClearOnBuild, true);
            s_clearOnRecompile = EditorPrefs.GetBool(PrefKey_ClearOnRecompile, false);
            s_prefsLoaded = true;
        }

        internal static void TogglePref_ClearOnPlay() { s_clearOnPlay = !s_clearOnPlay; EditorPrefs.SetBool(PrefKey_ClearOnPlay, s_clearOnPlay); }
        internal static void TogglePref_ClearOnBuild() { s_clearOnBuild = !s_clearOnBuild; EditorPrefs.SetBool(PrefKey_ClearOnBuild, s_clearOnBuild); }
        internal static void TogglePref_ClearOnRecompile() { s_clearOnRecompile = !s_clearOnRecompile; EditorPrefs.SetBool(PrefKey_ClearOnRecompile, s_clearOnRecompile); }

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
                    // Normalize Exception to Error on load as well
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
        // Per-window view state and input handling

        internal bool ShowLog = true, ShowWarn = true, ShowError = true;
        internal bool Collapse = false;
        internal string Search = string.Empty;
        internal int SelectedIndex = -1;

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
                var obj = AssetDatabase.LoadAssetAtPath<Object>(rel);
                if (obj != null) { AssetDatabase.OpenAsset(obj, Mathf.Max(1, f.line)); return; }
            }
            InternalEditorUtility.OpenFileAtLineExternal(abs, Mathf.Max(1, f.line));
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
