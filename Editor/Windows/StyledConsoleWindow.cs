using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace BattleTurn.StyledLog.Editor
{
    public class StyledConsoleWindow : EditorWindow
    {
        // Per-window controller (global storage is static inside controller)
        private readonly StyledConsoleController _controller = new();

        // Scroll positions
        private Vector2 _scrollList, _scrollStack, _scrollMessage;
        // Autoscroll
        private bool _autoScroll = true;
        private bool _autoScrollRequest;

        // Icons
        private GUIContent _iconInfo, _iconWarn, _iconError, _iconCompiler;

        // Column widths & splitter state
        private float _colIconW = 24f;
        private float _colTypeW = 80f;
        private float _colTagW = 160f;
        private const float MinColW = 60f;
        private const float SplitW = 4f;
        private bool _dragTypeSplit, _dragTagSplit;

        // Vertical split (row list vs stack pane)
        private float _stackFrac = 0.38f; // bottom pane fraction
        private const float MinStackH = 80f;
        private const float MinListH = 80f;

        // Inside stack pane: message vs frames splitter
        private float _stackMessageFrac = 0.4f;
        private const float MinMessageH = 40f;
        private const float MinFramesH = 80f;

        // Tooltip state
        private string _ttAbsPath;
        private int _ttLine;
    // Debounce state for tooltip
    private string _ttHoverPath;
    private int _ttHoverLine;
    private double _ttHoverStart;
    private bool _ttShowing;
    private double _ttLastShowTime;

        [MenuItem("Tools/StyledDebug/Styled Console")]
        public static void Open()
        {
            var w = GetWindow<StyledConsoleWindow>("Styled Console");
            w.minSize = new Vector2(760, 380);
            w.Show();
        }

        private void OnEnable()
        {
            StyledConsoleController.EnsurePrefsLoaded();
            _iconInfo = EditorGUIUtility.IconContent("console.infoicon");
            _iconWarn = EditorGUIUtility.IconContent("console.warnicon");
            _iconError = EditorGUIUtility.IconContent("console.erroricon");
            _iconCompiler = EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow");
            if (_iconCompiler == null || _iconCompiler.image == null) _iconCompiler = _iconWarn;

            StyledDebug.onEmit -= OnEmit; StyledDebug.onEmit += OnEmit;
            StyledConsoleController.Cleared -= HandleCleared; StyledConsoleController.Cleared += HandleCleared;
            StyledConsoleController.Changed -= HandleChanged; StyledConsoleController.Changed += HandleChanged;

            if (!StyledConsoleController.ClearOnRecompile && StyledConsoleController.LoadSnapshot())
                StyledConsoleController.RaiseCleared();

            // Ensure any existing compiler diagnostics are visible immediately
            StyledConsoleController.SyncCompilerMessages();
        }

        private void OnDisable()
        {
            StyledDebug.onEmit -= OnEmit;
            StyledConsoleController.Cleared -= HandleCleared;
            StyledConsoleController.Changed -= HandleChanged;
        }

        private void OnEmit(string tag, string richWithFont, LogType type, string stack)
        {
            StyledConsoleController.AddLog(tag, richWithFont, type, stack);
            _autoScrollRequest = _autoScroll;
            if (!StyledConsoleController.ClearOnRecompile) StyledConsoleController.SaveSnapshot();
            Repaint();
        }
        private void HandleCleared()
        {
            _scrollList = Vector2.zero;
            _autoScrollRequest = false;
            Repaint();
        }
        private void HandleChanged()
        {
            if (_autoScroll) _autoScrollRequest = true;
            Repaint();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // UI

        private void OnGUI()
        {
            DrawToolbar();
            DrawHeaderWithSplitters();
            _controller.BuildVisible();

            // Chiều cao còn lại của cửa sổ
            float availH = position.height;

            // Ước lượng phần header + toolbar + status (đã vẽ bằng layout ở trên & dưới)
            const float topChrome = 20f /*header*/ + 22f /*toolbar*/ + 6f;
            const float bottomChrome = 22f /*status*/ + 6f;

            float contentH = Mathf.Max(100f, availH - topChrome - bottomChrome);
            float listH = Mathf.Clamp(contentH * (1f - _stackFrac), MinListH, contentH - MinStackH);
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
                        float newStackH = Mathf.Clamp(stackH - dy, MinStackH, total - MinListH);
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
                _controller.ShowLog = GUILayout.Toggle(_controller.ShowLog, "Log", EditorStyles.toolbarButton);
                _controller.ShowWarn = GUILayout.Toggle(_controller.ShowWarn, "Warning", EditorStyles.toolbarButton);
                _controller.ShowError = GUILayout.Toggle(_controller.ShowError, "Error", EditorStyles.toolbarButton);

                GUILayout.Space(6);
                _controller.Collapse = GUILayout.Toggle(_controller.Collapse, "Collapse", EditorStyles.toolbarButton);
                _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-scroll", EditorStyles.toolbarButton);

                GUILayout.FlexibleSpace();

                _controller.Search = StyledConsoleEditorGUI.ToolbarSearch(_controller.Search, 180f);

                // Clear
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton)) StyledConsoleController.ClearPreserveCompileErrors();

                // (Sync Compiler button removed – auto-sync active)

                // Options dropdown (Clear on: Play/Build/Recompile)
                if (EditorGUILayout.DropdownButton(new GUIContent(""), FocusType.Passive, EditorStyles.toolbarDropDown))
                {
                    StyledConsoleController.EnsurePrefsLoaded();
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Clear on Play"), StyledConsoleController.ClearOnPlay, () => StyledConsoleController.TogglePref_ClearOnPlay());
                    menu.AddItem(new GUIContent("Clear on Build"), StyledConsoleController.ClearOnBuild, () => StyledConsoleController.TogglePref_ClearOnBuild());
                    menu.AddItem(new GUIContent("Clear on Recompile"), StyledConsoleController.ClearOnRecompile, () => StyledConsoleController.TogglePref_ClearOnRecompile());
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Live Compiler Sync"), StyledConsoleController.LiveCompilerSync, () => StyledConsoleController.TogglePref_LiveCompilerSync());
                    menu.ShowAsContext();
                }
            }

            // Rebuild collapsed view when toggled
            if (Event.current.type == EventType.Repaint && GUI.changed)
            {
                if (_controller.Collapse) StyledConsoleController.RebuildCollapsed();
                if (_controller.SelectedIndex >= _controller.GetVisibleCount()) _controller.SelectedIndex = _controller.GetVisibleCount() - 1;
            }
        }

        private void DrawHeaderWithSplitters()
        {
            var headerRect = EditorGUILayout.BeginHorizontal("box", GUILayout.Height(20));
            {
                GUILayout.Label("", GUILayout.Width(_colIconW)); // icon spacer
                GUILayout.Label("Type", GUILayout.Width(_colTypeW - _colIconW));
                // Tag header and dropdown arrow drawn inside the same column width
                var tagHeaderRect = GUILayoutUtility.GetRect(_colTagW, 20f, GUILayout.Width(_colTagW - 10));
                DrawTagDropdown(tagHeaderRect);
                GUILayout.Label("Message");

                // vertical splitters aligned to window coordinates
                var rTypeRight = new Rect(_colTypeW, headerRect.y, SplitW, headerRect.height);
                var rTagRight = new Rect(_colTypeW + _colTagW + SplitW, headerRect.y, SplitW, headerRect.height);

                // type|tag splitter
                StyledConsoleEditorGUI.DrawVSplitter(
                    rTypeRight,
                    dx => { _dragTypeSplit = true; _colTypeW = Mathf.Max(MinColW, _colTypeW + dx); Repaint(); },
                    () => { _dragTypeSplit = false; }
                );

                // tag|message splitter
                StyledConsoleEditorGUI.DrawVSplitter(
                    rTagRight,
                    dx => { _dragTagSplit = true; _colTagW = Mathf.Max(MinColW, _colTagW + dx); Repaint(); },
                    () => { _dragTagSplit = false; }
                );

                // bottom hairline rule
                Handles.color = new Color(0, 0, 0, 0.2f);
                Handles.DrawLine(new Vector2(headerRect.x, headerRect.yMax - 1),
                                 new Vector2(headerRect.xMax, headerRect.yMax - 1));
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTagDropdown(Rect rect)
        {
            // Draw label and a small dropdown arrow inside the same header cell
            const float arrowW = 20f;
            var labelRect = rect; labelRect.xMax = rect.xMax - arrowW;
            GUI.Label(labelRect, "Tag");

            var arrowRect = new Rect(rect.xMin + arrowW + 8f, rect.y + 2f, arrowW - 2f, rect.height);
            bool open = false;
            if (GUI.Button(arrowRect, GUIContent.none, EditorStyles.toolbarDropDown)) open = true;
            else if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition)) open = true;

            if (!open) return;

            Event.current.Use();

            // Ensure persisted tag selections are loaded at the moment the user opens the dropdown
            _controller.LoadTagPrefs();

            var menu = new GenericMenu();
            var mgr = StyledDebug.StyledLogManager;
            var tags = new List<string>();
            if (mgr != null)
            {
                try { tags = mgr.GetAllTags(); } catch { }
            }
            // Always include the special tags we may emit
            if (!tags.Contains("Unity")) tags.Add("Unity");
            if (!tags.Contains("default")) tags.Add("default");
            if (!tags.Contains("Compiler")) tags.Add("Compiler");

            // Everything toggle semantics:
            // - When Everything is ON and there are no explicit selections, all tags are visible (future tags too)
            // - Turning OFF Everything sets a mode where no tags are visible until the user selects some
            bool everythingOn = _controller.HasExplicitTagSelection ? false : _controller.TagEverything;
            menu.AddItem(new GUIContent("Everything"), everythingOn, () =>
            {
                // Toggle Everything
                _controller.SetEverything(!everythingOn);
                // Persist
                _controller.SaveTagPrefs();
                Repaint();
            });
            menu.AddSeparator("");

            // Per-tag flags
            foreach (var tag in tags)
            {
                bool enabled = _controller.GetTagEnabled(tag);
                menu.AddItem(new GUIContent(tag), enabled, () =>
                {
                    // Toggle rules:
                    // - If we already have explicit selections, just toggle this tag.
                    // - If Everything is ON and no explicit selections, seed explicit set with all tags and then untick this one.
                    // - If Everything is OFF and no explicit selections, ticking a tag enables only this tag.
                    if (_controller.HasExplicitTagSelection)
                    {
                        _controller.SetTagEnabled(tag, !enabled);
                    }
                    else
                    {
                        if (_controller.TagEverything)
                        {
                            _controller.EnableAllTags(tags);
                            _controller.SetTagEnabled(tag, false);
                        }
                        else
                        {
                            _controller.SetTagEnabled(tag, true);
                        }
                    }
                    _controller.SaveTagPrefs();
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }

        private void DrawRowsAreaLayout(Rect rect)
        {
            // Use shared drawer and delegate input to controller
            StyledConsoleEditorGUI.CompilerIcon = _iconCompiler;
            StyledConsoleEditorGUI.DrawRows(
                rect,
                _controller,
                _iconInfo,
                _iconWarn,
                _iconError,
                _colIconW,
                _colTypeW,
                _colTagW,
                _controller.Collapse,
                ref _scrollList
            );
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
                var stackText = _controller.SelectedStack();
                EditorGUIUtility.systemCopyBuffer = stackText ?? string.Empty;
            }

            // Message view (above stacktrace) with its own scroll when long
            // Compute a local split within the stack pane between message and frames
            float contentTop = toolbarRect.yMax + 4f;
            float contentBottom = rect.yMax - 4f;
            float totalH = Mathf.Max(0f, contentBottom - contentTop);
            float desiredMsgH = Mathf.Clamp(totalH * _stackMessageFrac, MinMessageH, Mathf.Max(MinMessageH, totalH - MinFramesH));
            var messageRect = new Rect(rect.x + 6f, contentTop, rect.width - 12f, desiredMsgH);
            GUI.Box(messageRect, GUIContent.none, EditorStyles.helpBox);

            string messageText = string.Empty;
            if (_controller.GetVisibleCount() > 0 && _controller.SelectedIndex >= 0)
            {
                try
                {
                    _controller.GetVisibleRow(_controller.SelectedIndex, out _, out _, out var rich, out _, out _, out _);
                    messageText = rich ?? string.Empty;
                }
                catch { /* ignore */ }
            }
            // Shared hover accumulation for both message inline link and stack frames (debounced later)
            bool hoveringAny = false; string hoveringPath = null; int hoveringLine = 0; Rect hoveringScreenAnchor = default;
            var msgInner = new Rect(messageRect.x + 6f, messageRect.y + 4f, messageRect.width - 12f, messageRect.height - 8f);
            var msgStyle = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
            float msgContentH = Mathf.Max(18f, msgStyle.CalcHeight(new GUIContent(messageText), msgInner.width - 16f));
            var msgView = new Rect(msgInner.x, msgInner.y, msgInner.width, msgInner.height);
            var msgContent = new Rect(0, 0, msgInner.width - 16f, msgContentH + 4f);
            _scrollMessage = GUI.BeginScrollView(msgView, _scrollMessage, msgContent);
            // Render message with basic clickable file path detection when no stacktrace frames
            var msgLabelRect = new Rect(0, 0, msgContent.width, msgContentH);
            // We'll break into segments if we detect path
            bool drewCustom = false;
            string detectedPath = null; int detectedLine = 0; Rect detectedLinkRect = default;
            Color msgLinkColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.85f, 0.5f) : new Color(0.05f, 0.6f, 0.25f);
            var msgLinkStyle = new GUIStyle(msgStyle); msgLinkStyle.normal.textColor = msgLinkColor;
            GUIContent tempGc;
            if (!string.IsNullOrEmpty(messageText))
            {
                // Only parse if there is no stack (otherwise stack frames already provide link)
                if (string.IsNullOrEmpty(_controller.SelectedStack()))
                {
                    var rxPathInline = new Regex(@"(?<path>(?:Assets|Packages)/[^\n\r:<>]+?\.cs)(?:[:(](?<line>\d+)[,)]?)?", RegexOptions.IgnoreCase);
                    var mInline = rxPathInline.Match(messageText);
                    if (mInline.Success)
                    {
                        drewCustom = true;
                        detectedPath = mInline.Groups["path"].Value;
                        int.TryParse(mInline.Groups["line"].Value, out detectedLine);
                        string pre = messageText.Substring(0, mInline.Index);
                        string hit = messageText.Substring(mInline.Index, mInline.Length);
                        string post = messageText.Substring(mInline.Index + mInline.Length);
                        // Draw pre
                        float x = msgLabelRect.x; float yLine = msgLabelRect.y;
                        tempGc = new GUIContent(pre);
                        float preW = msgStyle.CalcSize(tempGc).x;
                        GUI.Label(new Rect(x, yLine, preW, EditorGUIUtility.singleLineHeight), tempGc, msgStyle);
                        x += preW;
                        // Draw link (single line assumption). If multi-line wrap, fallback to simple label.
                        tempGc = new GUIContent(hit);
                        float linkW = msgLinkStyle.CalcSize(tempGc).x;
                        var linkRect = new Rect(x, yLine, linkW, EditorGUIUtility.singleLineHeight);
                        GUI.Label(linkRect, tempGc, msgLinkStyle);
                        // underline
                        var ul = new Rect(linkRect.x, linkRect.yMax - 1f, linkRect.width, 1f);
                        EditorGUI.DrawRect(ul, msgLinkColor);
                        x += linkW;
                        // Draw post
                        tempGc = new GUIContent(post);
                        float postW = msgStyle.CalcSize(tempGc).x;
                        GUI.Label(new Rect(x, yLine, postW, EditorGUIUtility.singleLineHeight), tempGc, msgStyle);

                        detectedLinkRect = linkRect;
                        EditorGUIUtility.AddCursorRect(linkRect, MouseCursor.Link);
                    }
                }
            }
            if (!drewCustom)
            {
                GUI.Label(msgLabelRect, messageText, msgStyle);
            }

            // If stacktrace empty (no synthetic frame), attempt regex detection for path:line
            if (drewCustom && detectedPath != null && detectedLinkRect.width > 0f)
            {
                bool hover = detectedLinkRect.Contains(Event.current.mousePosition);
                if (hover)
                {
                    string abs = StyledConsoleController.NormalizeToAbsolutePath(detectedPath);
                    hoveringAny = true;
                    hoveringPath = abs;
                    hoveringLine = detectedLine > 0 ? detectedLine : 1;
                    // Anchor identical to stacktrace frames (left bias and vertical center adjustment)
                    Vector2 rectPos = new Vector2(detectedLinkRect.x - 1f, detectedLinkRect.y);
                    Vector2 linkScreenPoint = GUIUtility.GUIToScreenPoint(rectPos);
                    linkScreenPoint.y -= ConsoleCodeTooltip.MaxHeight / 2f;
                    hoveringScreenAnchor = new Rect(linkScreenPoint, new Vector2(1,1));
                    if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 2)
                    {
                        StyledConsoleController.OpenAbsolutePath(abs, hoveringLine);
                        Event.current.Use();
                    }
                }
            }
            GUI.EndScrollView();

            // Local splitter between message and stacktrace
            var innerSplitRect = new Rect(rect.x + 2f, messageRect.yMax + 2f, rect.width - 4f, 4f);
            StyledConsoleEditorGUI.DrawHSplitter(
                innerSplitRect,
                dy =>
                {
                    // Increase message height when dragging down
                    float newMsgH = Mathf.Clamp(desiredMsgH + dy, MinMessageH, Mathf.Max(MinMessageH, totalH - MinFramesH));
                    _stackMessageFrac = totalH > 0 ? newMsgH / totalH : _stackMessageFrac;
                    Repaint();
                },
                null
            );

            // parse frames
            var stack = _controller.SelectedStack();
            var frames = StyledConsoleController.ParseStackFrames(stack);

            // callsite summary
            y = innerSplitRect.yMax + 4f;
            var callsite = StyledConsoleController.GetFirstUserFrame(frames);
            var callRect = new Rect(rect.x + 6f, y, rect.width - 12f, 18f);
            if (callsite != null)
            {
                GUI.Label(callRect, $"Callsite: {callsite.display}", EditorStyles.miniLabel);
                var openRect = new Rect(callRect.xMax - 52f, callRect.y - 1f, 48f, callRect.height);
                if (GUI.Button(openRect, "Open", EditorStyles.miniButton))
                    StyledConsoleController.OpenFrame(callsite);
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
            // (Removed duplicate local hover declarations; now shared with message section above)

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
                            hoveringPath = StyledConsoleController.NormalizeToAbsolutePath(f.path);
                            hoveringLine = f.line;
                            Vector2 rectPos = postRect.position; rectPos.x -= 1; // left bias
                            Vector2 linkScreenPoint = GUIUtility.GUIToScreenPoint(rectPos);
                            linkScreenPoint.y -= ConsoleCodeTooltip.MaxHeight / 2;
                            hoveringScreenAnchor = new Rect(linkScreenPoint, new Vector2(1,1));
                        }
                        if (Event.current.type == EventType.MouseDown && hover) { StyledConsoleController.OpenFrame(f); Event.current.Use(); }
                    }
                    else
                    {
                        // fallback: draw raw line (no link)
                        GUI.Label(row, f.raw, baseStyle);
                    }
                }
            }
            GUI.EndScrollView();
            // Debounce tooltip show/hide to avoid blinking
            if (Event.current.type == EventType.Repaint)
            {
                double now = EditorApplication.timeSinceStartup;
                if (hoveringAny && !string.IsNullOrEmpty(hoveringPath))
                {
                    bool changed = _ttHoverPath != hoveringPath || _ttHoverLine != hoveringLine;
                    if (changed)
                    {
                        _ttHoverPath = hoveringPath;
                        _ttHoverLine = hoveringLine;
                        _ttHoverStart = now; // restart dwell timer
                    }
                    // show after small dwell (120ms) to prevent rapid flicker when moving between close links
                    if (!_ttShowing && now - _ttHoverStart > 0.12f)
                    {
                        ConsoleCodeTooltip.ShowAtScreenRect(new Vector2(hoveringScreenAnchor.x, hoveringScreenAnchor.y), this, _ttHoverPath, Mathf.Max(1, _ttHoverLine));
                        _ttShowing = true;
                        _ttLastShowTime = now;
                    }
                    else if (_ttShowing && changed)
                    {
                        // update immediately on change once already visible
                        ConsoleCodeTooltip.ShowAtScreenRect(new Vector2(hoveringScreenAnchor.x, hoveringScreenAnchor.y), this, _ttHoverPath, Mathf.Max(1, _ttHoverLine));
                        _ttLastShowTime = now;
                    }
                }
                else
                {
                    // not hovering: hide after small grace (150ms) to avoid blink when crossing gaps
                    if (_ttShowing && now - _ttLastShowTime > 0.15f)
                    {
                        ConsoleCodeTooltip.HideIfOwner(this);
                        _ttShowing = false;
                        _ttHoverPath = null; _ttHoverLine = 0;
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Stack parsing and navigation

        private void DrawStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // compute counts from controller storage
                StyledConsoleController.ComputeCounts(out var cLog, out var cWarn, out var cErr);
                GUILayout.Label($"Log: {cLog}", EditorStyles.miniLabel);
                GUILayout.Space(10);
                GUILayout.Label($"Warning: {cWarn}", EditorStyles.miniLabel);
                GUILayout.Space(10);
                GUILayout.Label($"Error: {cErr}", EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();
                GUILayout.Label(_controller.Collapse ? "Collapsed view" : "Full view", EditorStyles.miniLabel);
            }
        }
    }
}
