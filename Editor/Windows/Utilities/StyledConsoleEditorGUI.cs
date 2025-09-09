using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.Callbacks;
using UnityEngine;
using System.Collections.Generic;

namespace BattleTurn.StyledLog.Editor
{
    internal static class StyledConsoleEditorGUI
    {
        // Strip <font="..."> tags; IMGUI does not render them and we set Font via GUIStyle.
        private static readonly Regex _fontOpenTagRx = new Regex(@"<font.*?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _fileLineRx = new Regex(@"\(at\s+(.+):(\d+)\)", RegexOptions.Compiled);

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
        internal static GUIContent CompilerIcon; // optional external assignment

        // ─────────────────────────────────────────────────────────────────────────────
        // High-level window section drawers (migrated from StyledConsoleWindow)
        // ─────────────────────────────────────────────────────────────────────────────

        private static GUIStyle _headerLabelStyle;
        private static GUIStyle _badgeStyle;

        #region Toolbar
        public static void DrawToolbar(EditorWindow owner, StyledConsoleController controller, ref bool autoScroll)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool oldCollapse = controller.Collapse;
                bool oldAuto = autoScroll;
                controller.Collapse = GUILayout.Toggle(controller.Collapse, "Collapse", EditorStyles.toolbarButton);
                autoScroll = GUILayout.Toggle(autoScroll, "Auto-scroll", EditorStyles.toolbarButton);

                GUILayout.FlexibleSpace();
                controller.Search = ToolbarSearch(controller.Search, 180f);

                GUILayout.Space(2f);
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton)) StyledConsoleController.ClearPreserveCompileErrors();

                if (EditorGUILayout.DropdownButton(new GUIContent(""), FocusType.Passive, EditorStyles.toolbarDropDown))
                {
                    StyledConsoleController.EnsurePrefsLoaded();
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Clear on Play"), StyledConsoleController.ClearOnPlay, () => StyledConsoleController.TogglePref_ClearOnPlay());
                    menu.AddItem(new GUIContent("Clear on Build"), StyledConsoleController.ClearOnBuild, () => StyledConsoleController.TogglePref_ClearOnBuild());
                    menu.AddItem(new GUIContent("Clear on Recompile"), StyledConsoleController.ClearOnRecompile, () => StyledConsoleController.TogglePref_ClearOnRecompile());
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Live Compiler Sync"), StyledConsoleController.LiveCompilerSync, () => StyledConsoleController.TogglePref_LiveCompilerSync());
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Rescan Inline Paths"), false, () =>
                    {
                        StyledConsoleController.ReinjectInlineFramesForAll();
                        owner.Repaint();
                    });
                    menu.ShowAsContext();
                }

                if (controller.Collapse != oldCollapse)
                {
                    EditorPrefs.SetBool("StyledConsole.Collapse", controller.Collapse);
                    if (controller.Collapse) StyledConsoleController.RebuildCollapsed();
                }
                if (autoScroll != oldAuto)
                {
                    EditorPrefs.SetBool("StyledConsole.AutoScroll", autoScroll);
                }
            }
        }
        #endregion

        #region Header
        public static void DrawHeader(EditorWindow owner, StyledConsoleController controller,
            ref float colIconW, ref float colTypeW, ref float colTagW)
        {
            var outer = GUILayoutUtility.GetRect(0, 20f, GUILayout.ExpandWidth(true));
            GUI.Box(outer, GUIContent.none, "box");

            var iconRect = new Rect(outer.x, outer.y, colIconW, outer.height);
            var typeRect = new Rect(iconRect.xMax, outer.y, colTypeW - colIconW, outer.height);
                var tagRect = new Rect(colTypeW, outer.y, colTagW, outer.height);
                var msgRect = new Rect(colTypeW + colTagW, outer.y, outer.width - (colTypeW + colTagW), outer.height);

                float newIconW = colIconW;
                float newTypeW = colTypeW;
                float newTagW = colTagW;

                DrawHeaderCell(iconRect, () => DrawHeaderLabel(iconRect, "Icon"), true,
                    dx => { float minIcon = 16f; float maxIcon = Mathf.Max(minIcon, newTypeW - 40f); newIconW = Mathf.Clamp(newIconW + dx, minIcon, maxIcon); owner.Repaint(); }, null);
                DrawHeaderCell(typeRect, () => DrawHeaderLabel(typeRect, "Type"), true,
                    dx => { newTypeW = Mathf.Max(60f, newTypeW + dx); owner.Repaint(); }, null);
                DrawHeaderCell(tagRect, () => DrawHeaderCellTag(owner, controller, tagRect), true,
                    dx => { newTagW = Mathf.Max(60f, newTagW + dx); owner.Repaint(); }, null);
                DrawHeaderCell(msgRect, () => DrawHeaderLabel(msgRect, "Message"), false, null, null);

                // Draw vertical column dividers (table style)
                {
                    Color vLineColor = new Color(0.7f, 0.7f, 0.7f, 0.35f);
                    float vLineY0 = outer.y;
                    float vLineY1 = outer.yMax;
                // Icon/Type divider (use colIconW for perfect alignment)
                EditorGUI.DrawRect(new Rect(colIconW - 1f, vLineY0, 1f, vLineY1 - vLineY0), vLineColor);
                    // Type/Tag divider
                    EditorGUI.DrawRect(new Rect(typeRect.xMax - 1f, vLineY0, 1f, vLineY1 - vLineY0), vLineColor);
                    // Tag/Message divider
                    EditorGUI.DrawRect(new Rect(tagRect.xMax - 1f, vLineY0, 1f, vLineY1 - vLineY0), vLineColor);
                }

                // Draw a clear horizontal splitter line between header and log rows
                // Unity Console style: 1-pixel, light gray line
                var splitterLine = new Rect(outer.x, outer.yMax - 1f, outer.width, 1f);
                EditorGUI.DrawRect(splitterLine, new Color(0.7f, 0.7f, 0.7f, 0.35f));

                // Apply after interactions
                colIconW = newIconW;
                colTypeW = newTypeW;
                colTagW = newTagW;
            }

        private static float DrawHeaderLabel(Rect rect, string text)
        {
            if (_headerLabelStyle == null)
            {
                _headerLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0)
                };
            }
            var gc = new GUIContent(text);
            var size = _headerLabelStyle.CalcSize(gc);
            var r = new Rect(rect.x + 2f, rect.y, Mathf.Min(size.x, rect.width - 2f), rect.height);
            GUI.Label(r, gc, _headerLabelStyle);
            return r.width;
        }

        private static void DrawHeaderCellTag(EditorWindow owner, StyledConsoleController controller, Rect rect)
        {
            const float arrowW = 20f;
            float labelW = DrawHeaderLabel(rect, "Tag");
            var arrowRect = new Rect(rect.x + 5f + labelW, rect.y, arrowW - 4f, rect.height - 4f);
            if (GUI.Button(arrowRect, GUIContent.none, EditorStyles.toolbarDropDown))
            {
                Event.current.Use();
                controller.LoadTagPrefs();
                var menu = new GenericMenu();
                var mgr = StyledDebug.StyledLogManager;
                var tags = new List<string>();
                if (mgr != null) { try { tags = mgr.GetAllTags(); } catch { } }
                if (!tags.Contains("Unity")) tags.Add("Unity");
                if (!tags.Contains("default")) tags.Add("default");
                if (!tags.Contains("Compiler")) tags.Add("Compiler");

                bool everythingOn = controller.HasExplicitTagSelection ? false : controller.TagEverything;
                menu.AddItem(new GUIContent("Everything"), everythingOn, () =>
                {
                    controller.SetEverything(!everythingOn);
                    controller.SaveTagPrefs();
                    owner.Repaint();
                });
                menu.AddSeparator("");
                foreach (var tag in tags)
                {
                    bool enabled = controller.GetTagEnabled(tag);
                    menu.AddItem(new GUIContent(tag), enabled, () =>
                    {
                        if (!controller.HasExplicitTagSelection && controller.TagEverything)
                        {
                            controller.SetEverything(false);
                            controller.SetTagEnabled(tag, true);
                        }
                        else
                        {
                            controller.SetTagEnabled(tag, !enabled);
                        }
                        controller.SaveTagPrefs();
                        owner.Repaint();
                    });
                }
                menu.ShowAsContext();
            }
        }

        private static void DrawHeaderCell(Rect rect, System.Action drawer, bool drawSeparator, System.Action<float> onDrag, System.Action onFinish)
        {
            drawer?.Invoke();
            bool hasDrag = onDrag != null;
            if (drawSeparator && !hasDrag)
            {
                var sepLine = new Rect(rect.xMax - 1f, rect.y + 1f, 1f, rect.height - 2f);
                EditorGUI.DrawRect(sepLine, new Color(0, 0, 0, 0.28f));
            }
            if (hasDrag)
            {
                var hit = new Rect(rect.xMax - 3f, rect.y, 6f, rect.height);
                DrawVSplitter(hit,
                    dx => { onDrag(dx); },
                    () => { onFinish?.Invoke(); });
            }
        }
        #endregion

        #region StatusBar + Badges
        public static void DrawStatusBar(EditorWindow owner, StyledConsoleController controller,
            GUIContent iconInfo, GUIContent iconWarn, GUIContent iconError)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                StyledConsoleController.ComputeCounts(out var cLog, out var cWarn, out var cErr);
                bool changed = false;
                var btnStyle = EditorStyles.toolbarButton;
                GUIContent gcLog = iconInfo != null ? new GUIContent(iconInfo.image, "Show Logs") : new GUIContent("Log");
                GUIContent gcWarn = iconWarn != null ? new GUIContent(iconWarn.image, "Show Warnings") : new GUIContent("Warn");
                GUIContent gcErr = iconError != null ? new GUIContent(iconError.image, "Show Errors") : new GUIContent("Err");
                // Dim icons when their category is currently hidden (off state) but keep them clickable.
                var prevColor = GUI.color;
                if (!controller.ShowLog) GUI.color = new Color(prevColor.r, prevColor.g, prevColor.b, 0.35f);
                bool newLog = GUILayout.Toggle(controller.ShowLog, gcLog, btnStyle, GUILayout.Width(46));
                GUI.color = prevColor;
                var logRect = GUILayoutUtility.GetLastRect();
                if (newLog != controller.ShowLog) { controller.ShowLog = newLog; changed = true; }
                if (!controller.ShowWarn) GUI.color = new Color(prevColor.r, prevColor.g, prevColor.b, 0.35f);
                bool newWarn = GUILayout.Toggle(controller.ShowWarn, gcWarn, btnStyle, GUILayout.Width(46));
                GUI.color = prevColor;
                var warnRect = GUILayoutUtility.GetLastRect();
                if (newWarn != controller.ShowWarn) { controller.ShowWarn = newWarn; changed = true; }
                if (!controller.ShowError) GUI.color = new Color(prevColor.r, prevColor.g, prevColor.b, 0.35f);
                bool newErr = GUILayout.Toggle(controller.ShowError, gcErr, btnStyle, GUILayout.Width(46));
                GUI.color = prevColor;
                var errRect = GUILayoutUtility.GetLastRect();
                if (newErr != controller.ShowError) { controller.ShowError = newErr; changed = true; }

                DrawCountBadge(logRect, cLog, new Color(0.25f, 0.55f, 0.95f, 1f), controller.ShowLog);
                DrawCountBadge(warnRect, cWarn, new Color(0.95f, 0.75f, 0.25f, 1f), controller.ShowWarn);
                DrawCountBadge(errRect, cErr, new Color(0.85f, 0.25f, 0.25f, 1f), controller.ShowError);

                GUILayout.FlexibleSpace();
                if (changed)
                {
                    controller.BuildVisible();
                    owner.Repaint();
                }
            }
        }

        private static void DrawCountBadge(Rect host, int count, Color color, bool enabled)
        {
            if (count <= 0) return;
            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 9, // smaller per request
                    padding = new RectOffset(0, 0, 0, 0),
                    clipping = TextClipping.Clip
                };
            }
            string txt = count > 999 ? "999+" : count.ToString();
            float wBase = txt.Length <= 2 ? 18f : (txt.Length == 3 ? 24f : 34f);
            float w = wBase - 4f;
            float h = 10f;
            var r = new Rect(
                Mathf.Round(host.x + host.width - w - 4f),
                Mathf.Round(host.y + 1f),
                w,
                h);
            float alpha = enabled ? 0.85f : 0.30f;
            var bg = new Color(color.r, color.g, color.b, alpha);
            EditorGUI.DrawRect(r, bg); // no rectangle outline per request
            // Text outline (only text has outline) + fill
            var oldColor = GUI.color;
            var textContent = new GUIContent(txt);
            Rect textRect = r;
            Color outlineCol = new Color(0f, 0f, 0f, enabled ? 0.55f : 0.35f); // lighter alpha for thinner look
            Color fillCol = enabled ? Color.white : new Color(1f, 1f, 1f, 0.6f);
            // Thinner outline: only cardinal directions (N,S,W,E)
            int[,] offsets = new int[,] { { 0, -1 }, { 0, 1 }, { -1, 0 }, { 1, 0 } };
            for (int i = 0; i < offsets.GetLength(0); i++)
            {
                GUI.color = outlineCol;
                GUI.Label(new Rect(textRect.x + offsets[i, 0], textRect.y + offsets[i, 1], textRect.width, textRect.height), textContent, _badgeStyle);
            }
            GUI.color = fillCol;
            GUI.Label(textRect, textContent, _badgeStyle);
            GUI.color = oldColor;
        }
        #endregion

        #region Stack Pane
        public static void DrawStackPane(
            Rect rect,
            StyledConsoleController controller,
            ref Vector2 scrollMessage,
            ref Vector2 scrollStack,
            ref float stackMessageFrac,
            ref string ttHoverPath,
            ref int ttHoverLine,
            ref double ttHoverStart,
            ref bool ttShowing,
            ref double ttLastShowTime,
            EditorWindow owner)
        {
            GUI.Box(rect, GUIContent.none);

            float contentTop = rect.y + 4f;
            float contentBottom = rect.yMax - 4f;
            float totalH = Mathf.Max(0f, contentBottom - contentTop);
            const float MinMessageH = 40f;
            const float MinFramesH = 80f;
            float desiredMsgH = Mathf.Clamp(totalH * stackMessageFrac, MinMessageH, Mathf.Max(MinMessageH, totalH - MinFramesH));
            var messageRect = new Rect(rect.x + 6f, contentTop, rect.width - 12f, desiredMsgH);
            GUI.Box(messageRect, GUIContent.none, EditorStyles.helpBox);

            string messageText = string.Empty;
            string originalFullMessage = string.Empty; // preserve full for path extraction even if we trim (compiler case)
            bool isCompiler = false;
            if (controller.GetVisibleCount() > 0 && controller.SelectedIndex >= 0)
            {
                try
                {
                    controller.GetVisibleRow(controller.SelectedIndex, out var type, out var tag, out var rich, out _, out _, out _);
                    isCompiler = tag == "Compiler";
                    messageText = rich ?? string.Empty;
                    originalFullMessage = messageText;
                }
                catch { }
            }
            if (isCompiler && !string.IsNullOrEmpty(messageText))
            {
                int firstColon = messageText.IndexOf(':');
                if (firstColon > 0)
                {
                    int secondColon = messageText.IndexOf(':', firstColon + 1);
                    if (secondColon > firstColon && secondColon + 1 < messageText.Length)
                    {
                        string tail = messageText.Substring(secondColon + 1).Trim();
                        if (tail.Length > 0) messageText = tail;
                    }
                }
            }

            bool hoveringAny = false; string hoveringPath = null; int hoveringLine = 0; Rect hoveringScreenAnchor = default;
            var msgInner = new Rect(messageRect.x + 6f, messageRect.y + 4f, messageRect.width - 12f, messageRect.height - 8f);
            var msgStyle = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
            var charSize = msgStyle.CalcSize(new GUIContent("W"));
            int dynamicMaxRun = Mathf.Max(8, Mathf.FloorToInt((msgInner.width - 12f) / Mathf.Max(1f, charSize.x)) - 1);
            string displayMessageText = InsertSoftWraps(messageText, dynamicMaxRun);
            float msgContentH = Mathf.Max(18f, msgStyle.CalcHeight(new GUIContent(displayMessageText), msgInner.width - 16f));
            var msgView = new Rect(msgInner.x, msgInner.y, msgInner.width, msgInner.height);
            var msgContent = new Rect(0, 0, msgInner.width - 16f, msgContentH + 4f);
            scrollMessage = GUI.BeginScrollView(msgView, scrollMessage, msgContent);

            var msgLabelRect = new Rect(0, 0, msgContent.width, msgContentH);
            GUI.Label(msgLabelRect, displayMessageText, msgStyle); // inline links removed per request; stack pane frames show links
            GUI.EndScrollView();

            var innerSplitRect = new Rect(rect.x + 2f, messageRect.yMax + 2f, rect.width - 4f, 4f);
            float localStackMessageFrac = stackMessageFrac;
            DrawHSplitter(innerSplitRect,
                dy =>
                {
                    float newMsgH = Mathf.Clamp(desiredMsgH + dy, MinMessageH, Mathf.Max(MinMessageH, totalH - MinFramesH));
                    localStackMessageFrac = totalH > 0 ? newMsgH / totalH : localStackMessageFrac;
                    owner.Repaint();
                }, null);
            stackMessageFrac = localStackMessageFrac;

            var stack = controller.SelectedStack();
            var frames = StyledConsoleController.ParseStackFrames(stack);
            // List starts immediately – only clickable frame rows (no extra callsite label/button per request).
            float listTop = innerSplitRect.yMax + 4f;
            float listH = Mathf.Max(0f, rect.yMax - listTop - 4f);
            var viewRect = new Rect(rect.x + 2f, listTop, rect.width - 4f, listH);
            int lineH = 18;
            var contentRect = new Rect(0, 0, viewRect.width - 16f, Mathf.Max(listH, frames.Count * lineH));
            scrollStack = GUI.BeginScrollView(viewRect, scrollStack, contentRect);
            if (frames.Count == 0)
            { GUI.Label(new Rect(6f, 4f, contentRect.width - 12f, 18f), "<no stacktrace>", EditorStyles.helpBox); }
            else
            {
                var baseStyle = new GUIStyle(EditorStyles.label) { richText = false, wordWrap = false };
                var linkColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.85f, 0.5f) : new Color(0.05f, 0.6f, 0.25f);
                var linkStyle = new GUIStyle(baseStyle); linkStyle.normal.textColor = linkColor;
                for (int i = 0; i < frames.Count; i++)
                {
                    var f = frames[i]; var row = new Rect(6f, i * lineH, contentRect.width - 12f, lineH);
                    if (!string.IsNullOrEmpty(f.path) && f.line > 0)
                    {
                        string prefix = (string.IsNullOrEmpty(f.method) ? "" : f.method) + " (at "; Rect linkRect;
                        DrawInlineFileLink(row, prefix, f.display, ")", baseStyle, linkStyle, linkColor, out linkRect);
                        bool hover = linkRect.width > 0 && linkRect.Contains(Event.current.mousePosition);
                        if (hover)
                        {
                            hoveringAny = true; hoveringPath = StyledConsoleController.NormalizeToAbsolutePath(f.path); hoveringLine = f.line;
                            Vector2 rectPos = new Vector2(linkRect.xMax + 1f, linkRect.y - 2f);
                            Vector2 linkScreenPoint = GUIUtility.GUIToScreenPoint(rectPos);
                            hoveringScreenAnchor = new Rect(linkScreenPoint, new Vector2(1, 1));
                        }
                        if (Event.current.type == EventType.MouseDown && hover) { StyledConsoleController.OpenFrame(f); Event.current.Use(); }
                    }
                    else { GUI.Label(row, f.raw, baseStyle); }
                }
            }
            GUI.EndScrollView();

            if (Event.current.type == EventType.Repaint)
            {
                double now = EditorApplication.timeSinceStartup;
                if (hoveringAny && !string.IsNullOrEmpty(hoveringPath))
                {
                    bool changed = ttHoverPath != hoveringPath || ttHoverLine != hoveringLine;
                    if (changed)
                    { ttHoverPath = hoveringPath; ttHoverLine = hoveringLine; ttHoverStart = now; }
                    if (!ttShowing && now - ttHoverStart > 0.12f)
                    { ConsoleCodeTooltip.ShowAtScreenRect(new Vector2(hoveringScreenAnchor.x, hoveringScreenAnchor.y), owner, ttHoverPath, Mathf.Max(1, ttHoverLine)); ttShowing = true; ttLastShowTime = now; }
                    else if (ttShowing && changed)
                    { ConsoleCodeTooltip.ShowAtScreenRect(new Vector2(hoveringScreenAnchor.x, hoveringScreenAnchor.y), owner, ttHoverPath, Mathf.Max(1, ttHoverLine)); ttLastShowTime = now; }
                }
                else
                {
                    if (ttShowing && now - ttLastShowTime > 0.15f)
                    { ConsoleCodeTooltip.HideIfOwner(owner); ttShowing = false; ttHoverPath = null; ttHoverLine = 0; }
                }
            }
        }

        private static string InsertSoftWraps(string input, int maxRun = 80)
        {
            if (string.IsNullOrEmpty(input) || maxRun < 8) return input;
            return Regex.Replace(input, @"([^\s<]{" + maxRun + @",})", m =>
            {
                var s = m.Value; System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length + s.Length / maxRun + 4);
                for (int i = 0; i < s.Length; i++) { sb.Append(s[i]); if ((i + 1) % maxRun == 0 && i < s.Length - 1) sb.Append('\n'); }
                return sb.ToString();
            });
        }
        #endregion

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
            ref Vector2 scroll,
            System.Action<int, int> onRowMouseDown,
            System.Action<int> onRowContextMenu)
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
                bool isCompilerTag = !string.IsNullOrEmpty(tag) && tag == "Compiler" && CompilerIcon != null && CompilerIcon.image != null;
                if (isCompilerTag)
                {
                    // Draw base compiler icon first
                    GUI.DrawTexture(iconRect, CompilerIcon.image, ScaleMode.ScaleToFit);
                    // Overlay small severity badge (bottom-right)
                    Texture severityTex = null;
                    if (type == LogType.Error && iconError != null) severityTex = iconError.image;
                    else if (type == LogType.Warning && iconWarn != null) severityTex = iconWarn.image;
                    else if (iconInfo != null) severityTex = iconInfo.image;
                    if (severityTex != null)
                    {
                        const float badgeSize = 9f; // small badge
                        var badgeRect = new Rect(iconRect.xMax - badgeSize, iconRect.yMax - badgeSize, badgeSize, badgeSize);
                        GUI.DrawTexture(badgeRect, severityTex, ScaleMode.ScaleToFit);
                    }
                }
                else
                {
                    if (icon != null && icon.image != null) GUI.DrawTexture(iconRect, icon.image, ScaleMode.ScaleToFit);
                }

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

                // Draw vertical column dividers for each row
                {
                    Color vLineColor = new Color(0.7f, 0.7f, 0.7f, 0.35f);
                    float vLineY0 = rowRect.y;
                    float vLineY1 = rowRect.yMax;
                    // Icon/Type divider (use colIconW for perfect alignment)
                    EditorGUI.DrawRect(new Rect(colIconW - 1f, vLineY0, 1f, vLineY1 - vLineY0), vLineColor);
                    EditorGUI.DrawRect(new Rect(typeRect.xMax - 1f, vLineY0, 1f, vLineY1 - vLineY0), vLineColor);
                    EditorGUI.DrawRect(new Rect(tagRect.xMax - 1f, vLineY0, 1f, vLineY1 - vLineY0), vLineColor);
                }

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    onRowMouseDown?.Invoke(i, Event.current.clickCount);
                    Event.current.Use();
                }
                if (Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
                {
                    onRowContextMenu?.Invoke(i);
                    Event.current.Use();
                }
            }

            GUI.EndScrollView();
        }

        // Shared inline file link renderer (prefix + clickable link + optional suffix)
        // Returns true if link drawn; outputs linkRect for hover/click handling.
        public static bool DrawInlineFileLink(Rect row, string prefix, string linkText, string suffix, GUIStyle baseStyle, GUIStyle linkStyle, Color underlineColor, out Rect linkRect)
        {
            linkRect = default;
            if (row.width <= 4f) return false;
            float x = row.x;
            float y = row.y;
            if (!string.IsNullOrEmpty(prefix))
            {
                var preSize = baseStyle.CalcSize(new GUIContent(prefix));
                GUI.Label(new Rect(x, y, preSize.x, row.height), prefix, baseStyle);
                x += preSize.x;
            }
            if (string.IsNullOrEmpty(linkText)) return false;
            var lSize = linkStyle.CalcSize(new GUIContent(linkText));
            linkRect = new Rect(x, y, lSize.x, row.height);
            EditorGUIUtility.AddCursorRect(linkRect, MouseCursor.Link);
            GUI.Label(linkRect, linkText, linkStyle);
            var ul = new Rect(linkRect.x, linkRect.yMax - 1f, Mathf.Min(linkRect.width, row.xMax - linkRect.x), 1f);
            EditorGUI.DrawRect(ul, underlineColor);
            x += lSize.x;
            if (!string.IsNullOrEmpty(suffix) && x < row.xMax)
            {
                var sufSize = baseStyle.CalcSize(new GUIContent(suffix));
                GUI.Label(new Rect(x, y, Mathf.Min(sufSize.x, row.xMax - x), row.height), suffix, baseStyle);
            }
            return true;
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

        // Toolbar search field: clear 'x' rendered inside field, only visible & clickable when non-empty.
        public static string ToolbarSearch(string value, float minWidth = 180f)
        {
#if UNITY_2021_1_OR_NEWER
            var textStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarSearchField;
            var cancelStyle = GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? GUI.skin.FindStyle("ToolbarCancelButton");
#else
            var textStyle = GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.toolbarSearchField;
            var cancelStyle = GUI.skin.FindStyle("ToolbarSeachCancelButton") ?? GUI.skin.FindStyle("ToolbarCancelButton");
#endif
            float height = Mathf.Max(18f, textStyle.fixedHeight > 0 ? textStyle.fixedHeight : 18f);
            Rect r = GUILayoutUtility.GetRect(minWidth, height, GUILayout.MinWidth(minWidth));
            // Center vertically within toolbar height (usually 22)
            float toolbarH = EditorStyles.toolbar.fixedHeight > 0 ? EditorStyles.toolbar.fixedHeight : height;
            if (toolbarH > height)
            {
                // Center then nudge up 1px for better optical alignment with other toolbar controls
                float yOff = (toolbarH - height) * 0.5f;
                r.y += Mathf.Round(yOff); // moved down 1px (removed previous -1f nudge)
            }
            bool hasText = !string.IsNullOrEmpty(value);
            float btnSize = Mathf.Clamp(height - 4f, 12f, 20f);
            // Vertically center cancel button; subtract 1px to compensate style baseline so it doesn't look too low
            float btnY = r.y + (r.height - btnSize) * 0.5f - 1f;
            var btnRect = new Rect(r.xMax - btnSize - 2f, Mathf.Round(btnY), btnSize, btnSize);

            // Intercept click for clear BEFORE drawing text field so TextField doesn't eat it
            var e = Event.current;
            if (hasText && e.type == EventType.MouseDown && btnRect.Contains(e.mousePosition))
            {
                value = string.Empty;
                GUI.FocusControl(null);
                e.Use();
            }

            // Temporarily pad right side so text doesn't overlap button area
            int oldRight = textStyle.padding.right;
            if (hasText) textStyle.padding.right = oldRight + (int)(btnSize + 6f);
            value = GUI.TextField(r, value, textStyle);
            textStyle.padding.right = oldRight;

            if (hasText)
            {
                // Draw button (visual + hover), it's purely cosmetic because click already intercepted
                if (cancelStyle != null)
                {
                    GUI.Button(btnRect, GUIContent.none, cancelStyle);
                }
                else
                {
                    // Fallback simple X
                    var oldColor = GUI.color;
                    GUI.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                    GUI.Label(btnRect, "x", EditorStyles.centeredGreyMiniLabel);
                    GUI.color = oldColor;
                }
                EditorGUIUtility.AddCursorRect(btnRect, MouseCursor.Arrow);
            }
            return value;
        }
    }
}
