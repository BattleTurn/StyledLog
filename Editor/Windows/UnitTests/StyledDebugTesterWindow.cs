using UnityEditor;
using UnityEngine;
using TMPro;

namespace BattleTurn.StyledLog.Editor
{
    public class StyledDebugTesterWindow : EditorWindow
    {
        private const string MENU_PATH = "Tools/StyledDebug/Tester";

        // test state (editor-only, not serialized to asset)
        private StyleSetting _style;
        private string _message = "Hello StyledDebug!";
        private bool _foldoutFonts = true;
        private Vector2 _scroll;

        // preview style (IMGUI rich text)
        private GUIStyle _previewStyle;

        [MenuItem(MENU_PATH)]
        public static void Open()
        {
            var w = GetWindow<StyledDebugTesterWindow>(true, "StyledDebug Tester", true);
            w.minSize = new Vector2(420, 360);
            w.Show();
        }

        private void OnEnable()
        {
            _previewStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = true
            };
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Style Under Test", EditorStyles.boldLabel);
            _style = (StyleSetting)EditorGUILayout.ObjectField("StyleSetting", _style, typeof(StyleSetting), false);

            using (new EditorGUI.DisabledScope(_style == null))
            {
                if (_style != null)
                {
                    EditorGUILayout.Space(4);
                    DrawStyleSummaryBox(_style);
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Message", EditorStyles.boldLabel);
                _message = EditorGUILayout.TextField("Sample Text", _message);

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                DrawPreview(_style, _message);

                EditorGUILayout.Space(8);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Log"))
                        RunAndReport(() => StyledDebug.Log(_style.Tag, new StyledText(_message, _style)));
                    if (GUILayout.Button("Warning"))
                        RunAndReport(() => StyledDebug.LogWarning(_style.Tag, new StyledText(_message, _style)));
                    if (GUILayout.Button("Error"))
                        RunAndReport(() => StyledDebug.LogError(_style.Tag, new StyledText(_message, _style)));
                }

                EditorGUILayout.Space(6);
                if (GUILayout.Button("Benchmark x1000"))
                {
                    var r = StyledDebugBenchmark.RunMany(_style.Tag, 1000, new StyledText(_message, _style));
                    Debug.Log($"[StyledDebug Tester] 1000x avg: time={r.milliseconds:F4} ms, memDelta={r.bytes} bytes, outLen={r.outputLength}");
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Compiler Log Tests", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Các nút dưới sẽ phát sinh log với tag 'Compiler' mô phỏng output của trình biên dịch để test icon compiler + badge severity, trimming message và link path.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Compiler Info"))
                    {
                        EmitCompilerLike("info", LogType.Log, "This is a simulated compiler info message");
                    }
                    if (GUILayout.Button("Compiler Warning"))
                    {
                        EmitCompilerLike("warning", LogType.Warning, "Simulated potential issue (unused variable)");
                    }
                    if (GUILayout.Button("Compiler Error"))
                    {
                        EmitCompilerLike("error", LogType.Error, "Simulated syntax error: unexpected token");
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawStyleSummaryBox(StyleSetting s)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Resolved Style", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Enabled", s.Enabled ? "True" : "False");
                EditorGUILayout.LabelField("Tag", s.Tag);

                _foldoutFonts = EditorGUILayout.Foldout(_foldoutFonts, "Fonts");
                if (_foldoutFonts)
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.ObjectField("Unity Font", s.Font, typeof(Font), false);
                        EditorGUILayout.ObjectField("TMP Font", s.TmpFont, typeof(TMP_FontAsset), false);
                    }
                }

                // show a big color swatch and hex
                var color = Color.white;
                ColorUtility.TryParseHtmlString(s.HexColor, out color);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
                var r = GUILayoutUtility.GetRect(1, 22);
                EditorGUI.DrawRect(r, color);
                EditorGUI.LabelField(r, GUIContent.none);
                EditorGUILayout.LabelField("Hex", s.HexColor);

                EditorGUILayout.LabelField("TextStyle", s.Style.ToString());
            }
        }

        private void DrawPreview(StyleSetting s, string msg)
        {
            // Build a StyledText and render its rich text
            var st = new StyledText(msg, s);
            var rich = st.ToString();

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("RichText Output", EditorStyles.miniBoldLabel);
                EditorGUILayout.TextArea(rich);
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Preview (Editor Label, richText)", EditorStyles.miniBoldLabel);
                GUILayout.Label(rich, _previewStyle, GUILayout.MinHeight(24));
                EditorGUILayout.HelpBox("Unity's Editor Label supports <color>, <b>, <i>, <size>, <u>, <s>. <font> tag is for your TMP in-game UI (not previewed here).", MessageType.Info);
            }
        }

        private void RunAndReport(System.Action action)
        {
            // Measure time and memory for a single call
            long beforeMem = System.GC.GetTotalMemory(false);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            action();
            sw.Stop();
            long afterMem = System.GC.GetTotalMemory(false);

            Debug.Log($"[StyledDebug Tester] once: time={sw.Elapsed.TotalMilliseconds:F4} ms, memDelta={afterMem - beforeMem} bytes");
        }

        private void EmitCompilerLike(string severityToken, LogType type, string msg)
        {
            // Pattern: Assets/Path/File.cs(line,col): severity: message
            string fakePath = "Assets/Tests/CompilerTest.cs";
            int line = Random.Range(5, 120);
            int col = Random.Range(1, 40);
            string full = $"{fakePath}({line},{col}): {severityToken}: {msg}";
            var styled = new StyledText(full, _style); // style optional
            switch (type)
            {
                case LogType.Error:
                    StyledDebug.LogError("Compiler", styled);
                    break;
                case LogType.Warning:
                    StyledDebug.LogWarning("Compiler", styled);
                    break;
                default:
                    StyledDebug.Log("Compiler", styled);
                    break;
            }
        }
    }
}
