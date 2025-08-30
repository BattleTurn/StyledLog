using UnityEditor;
using UnityEngine;

namespace BattleTurn.StyledLog.Editor
{
    [CustomEditor(typeof(StyleSetting))]
    public class StyleSettingEditor : UnityEditor.Editor
    {
        // Serialized field names (must match private fields in StyleSetting)
        private const string PROP_TAG       = "_tag";
        private const string PROP_ENABLED   = "_enabled";
        private const string PROP_FONT      = "_font";     // UnityEngine.Font
        private const string PROP_TMP_FONT  = "_tmpFont";  // TMPro.TMP_FontAsset
        private const string PROP_HEX_COLOR = "_hexColor"; // #RRGGBB / #RRGGBBAA
        private const string PROP_STYLE     = "_style";    // TextStyle flags

        private SerializedProperty _tag;
        private SerializedProperty _enabled;
        private SerializedProperty _font;
        private SerializedProperty _tmpFont;
        private SerializedProperty _hexColor;
        private SerializedProperty _style;

        private void OnEnable()
        {
            // Cache properties for performance
            _tag      = serializedObject.FindProperty(PROP_TAG);
            _enabled  = serializedObject.FindProperty(PROP_ENABLED);
            _font     = serializedObject.FindProperty(PROP_FONT);
            _tmpFont  = serializedObject.FindProperty(PROP_TMP_FONT);
            _hexColor = serializedObject.FindProperty(PROP_HEX_COLOR);
            _style    = serializedObject.FindProperty(PROP_STYLE);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Basic fields
            EditorGUILayout.PropertyField(_tag,     new GUIContent("Tag"));
            EditorGUILayout.PropertyField(_enabled, new GUIContent("Enabled"));

            // Font references group
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Fonts", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_font,    new GUIContent("Unity Font", "Legacy UnityEngine.Font asset"));
                EditorGUILayout.PropertyField(_tmpFont, new GUIContent("TMP Font Asset", "TMPro TMP_FontAsset"));
            }

            EditorGUILayout.PropertyField(_style, new GUIContent("Default Style"));

            // --- Hex field + color picker in the same row ---
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Default Color", EditorStyles.boldLabel);

            // Parse current hex into a Color
            var currentColor = Color.white;
            if (_hexColor != null && !string.IsNullOrEmpty(_hexColor.stringValue))
                ColorUtility.TryParseHtmlString(_hexColor.stringValue, out currentColor);

            using (new EditorGUILayout.HorizontalScope())
            {
                // Square color picker next to hex
                EditorGUI.BeginChangeCheck();
                var newColor = EditorGUILayout.ColorField(
                    GUIContent.none,
                    currentColor,
                    true,  // eyedropper
                    true,  // alpha
                    false,  // HDR
                    GUILayout.Width(32+5),
                    GUILayout.Height(18)
                );
                if (EditorGUI.EndChangeCheck())
                {
                    _hexColor.stringValue = "#" + ColorUtility.ToHtmlStringRGBA(newColor);
                    currentColor = newColor; // update hex when picker changes
                }

                // Hex input field
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_hexColor, new GUIContent("Hex"));
                if (EditorGUI.EndChangeCheck())
                {
                    if (ColorUtility.TryParseHtmlString(_hexColor.stringValue, out var parsed))
                        currentColor = parsed; // update picker when hex changes
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
