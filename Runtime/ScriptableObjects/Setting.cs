
// Runtime part - needs to be outside the Editor namespace
using UnityEngine;

namespace Colorful.ScriptableObjects
{
    [CreateAssetMenu(fileName = "DebugSetting", menuName = "Colorful/DebugSetting", order = 1)]
    public sealed class Setting : ScriptableObject
    {
        private static Setting _instance;

        [Header("Debug Log Settings")]
        [SerializeField]
        [Tooltip("Enable debug log on developer mode.")]
        private bool _isDebugLogEnableOnDevMode = true;

        [SerializeField]
        [Tooltip("Enable debug log on product mode.")]
        private bool _isDebugLogEnableOnProductMode = false;

        [SerializeField]
        [Tooltip("ScriptableObject for format settings.")]
        private FormatSO _formatSO = null;

        [SerializeField]
        [Tooltip("ScriptableObject for string builder settings.")]
        private StringBuilderSO _stringBuilderSO = null;


        public static Setting Instance
        {
            get
            {
                return _instance;
            }
        }

        public static bool IsDebugLogOnDevMode
        {
            get
            {
                if (_instance == null)
                {
                    return true; // Default to true if instance is not found
                }

                return Instance._isDebugLogEnableOnDevMode;
            }
        }

        public static bool IsDebugLogEnableOnProductMode
        {
            get
            {
                if (_instance == null)
                {
                    return false; // Default to false if instance is not found
                }

                return Instance._isDebugLogEnableOnProductMode;
            }
        }

        private void Awake()
        {
            _instance = this;
        }

        private void OnEnable()
        {
            _instance = this;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnRuntimeStart()
        {
            Debug.onFormatEvent += _instance._formatSO.GetFormat;
            Debug.stringBuilderAppendEvent += _instance._stringBuilderSO.GetStringBuilderAppends;
        }
    }
}