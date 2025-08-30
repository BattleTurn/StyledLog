// StyleSetting.cs
using UnityEngine;
using TMPro;

namespace BattleTurn.StyledLog
{
    [CreateAssetMenu(fileName = "StyledStyle", menuName = "StyledDebug/Style", order = 1)]
    public class StyleSetting : ScriptableObject
    {
        [SerializeField] private string _tag = "default";
        [SerializeField] private bool _enabled = true;
        [SerializeField] private Font _font;              // Unity legacy Font asset
        [SerializeField] private TMP_FontAsset _tmpFont;  // TextMeshPro font asset
        [SerializeField] private string _hexColor = "#FFFFFF"; // #RRGGBB or #RRGGBBAA
        [SerializeField] private TextStyle _style = TextStyle.None;

        // Properties (PascalCase) for external read access
        public string Tag => _tag;
        public bool Enabled => _enabled;
        public Font Font => _font;
        public TMP_FontAsset TmpFont => _tmpFont;
        public string HexColor => _hexColor;
        public TextStyle Style => _style;
    }
}
