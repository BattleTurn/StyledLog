using System.Collections.Generic;
using UnityEngine;

namespace BattleTurn.StyledLog
{
    public class StyledLogManager : ScriptableObject
    {
        [SerializeField]
        private StyleSetting[] _styles = new StyleSetting[0];

        private Dictionary<string, StyleSetting> _styleMap = new();

        #region PROPERTIES
        public StyleSetting this[string tag]
        {
            get
            {
                if (_styleMap.Count == 0 || _styles.Length != _styleMap.Count)
                {
                    _styleMap = new Dictionary<string, StyleSetting>();
                    foreach (var style in _styles)
                    {
                        if (style != null && !string.IsNullOrEmpty(style.Tag))
                        {
                            _styleMap[style.Tag] = style;
                        }
                    }
                }

                if (_styleMap.TryGetValue(tag, out var styleSetting))
                {
                    return styleSetting;
                }

                return null;
            }
        }

        /// <summary>
        /// Returns all available style tags defined in this manager.
        /// </summary>
        public List<string> GetAllTags()
        {
            // Ensure map is built
            _ = this["__ensure_build__"];
            var list = new List<string>(_styleMap.Keys);
            return list;
        }
        #endregion
    }

}