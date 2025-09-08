using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace BattleTurn.StyledLog.Editor
{
    public class StyledConsoleWindow : EditorWindow
    {
        private const string PrefKey_AutoScroll = "StyledConsole.AutoScroll";
        private const string PrefKey_Collapse = "StyledConsole.Collapse";
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
        // (Removed old per-column drag state variables after refactor)

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
            // Load persisted simple UI prefs
            _autoScroll = EditorPrefs.GetBool(PrefKey_AutoScroll, true);
            _controller.Collapse = EditorPrefs.GetBool(PrefKey_Collapse, false);
            _iconInfo = EditorGUIUtility.IconContent("console.infoicon");
            _iconWarn = EditorGUIUtility.IconContent("console.warnicon");
            _iconError = EditorGUIUtility.IconContent("console.erroricon");
            // Load custom compiler icon from Resources (Packages/StyleLog/Runtime/Resources/compiler_icon.png expected)
            var compilerTex = Resources.Load<Texture2D>("compiler_icon");
            if (compilerTex == null)
            {
                // Fallback: attempt direct asset path (package path variants)
                compilerTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/StyleLog/Runtime/Resources/compiler_icon.png");
                if (compilerTex == null)
                    compilerTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/styled-log/Runtime/Resources/compiler_icon.png");
            }
            if (compilerTex != null)
            {
                _iconCompiler = new GUIContent(compilerTex);
            }
            else
            {
                _iconCompiler = EditorGUIUtility.IconContent("cs Script Icon");
                if (_iconCompiler == null || _iconCompiler.image == null)
                    _iconCompiler = _iconWarn; // absolute fallback
            }

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
            StyledConsoleEditorGUI.DrawToolbar(this, _controller, ref _autoScroll);
            StyledConsoleEditorGUI.DrawHeader(this, _controller, ref _colIconW, ref _colTypeW, ref _colTagW);
            _controller.BuildVisible();

            // Chiều cao còn lại của cửa sổ
            float availH = position.height;

            // Ước lượng phần header + toolbar + status (đã vẽ bằng layout ở trên & dưới)
            const float topChrome = 20f /*header*/ + 22f /*toolbar*/ + 6f;
            const float bottomChrome = 22f /*status*/ + 6f;

            float contentH = Mathf.Max(100f, availH - topChrome - bottomChrome);

            // Nếu có stack (kể cả synthetic frame) mà pane dưới quá nhỏ (user đã kéo gần như ẩn), tự mở ra tối thiểu
            var selStack = _controller.SelectedStack();
            if (!string.IsNullOrEmpty(selStack) && selStack.Contains("(at "))
            {
                float minFracForUsable = 0.25f; // 25% chiều cao cho khung stack
                if (_stackFrac < minFracForUsable)
                    _stackFrac = minFracForUsable;
            }
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
                StyledConsoleEditorGUI.DrawStackPane(
                    stackRect,
                    _controller,
                    ref _scrollMessage,
                    ref _scrollStack,
                    ref _stackMessageFrac,
                    ref _ttHoverPath,
                    ref _ttHoverLine,
                    ref _ttHoverStart,
                    ref _ttShowing,
                    ref _ttLastShowTime,
                    this);
            }
            StyledConsoleEditorGUI.DrawStatusBar(this, _controller, _iconInfo, _iconWarn, _iconError);

            if (_autoScrollRequest && Event.current.type == EventType.Repaint)
            {
                _scrollList.y = float.MaxValue;
                _autoScrollRequest = false;
            }
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
                ref _scrollList,
                OnRowMouseDown,
                OnRowContextMenu
            );
        }

        private void OnRowMouseDown(int index, int clickCount)
        {
            _controller.SelectedIndex = index; // selection state still inside controller for now
            if (clickCount == 2)
            {
                StyledConsoleEditorGUI.OpenFirstUserFrame(_controller.SelectedStack());
            }
            Repaint();
        }

        private void OnRowContextMenu(int index)
        {
            if (index < 0 || index >= _controller.GetVisibleCount()) return;
            _controller.GetVisibleRow(index, out var type, out var tag, out var rich, out var font, out var count, out var stack);
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Open Callsite"), false, () => StyledConsoleEditorGUI.OpenFirstUserFrame(stack));
            menu.AddItem(new GUIContent("Copy Message"), false, () => EditorGUIUtility.systemCopyBuffer = rich ?? string.Empty);
            menu.AddItem(new GUIContent("Copy Stacktrace"), false, () => EditorGUIUtility.systemCopyBuffer = stack ?? string.Empty);
            menu.ShowAsContext();
        }

        // Removed inline GUI logic (moved to StyledConsoleEditorGUI)
    }
}
