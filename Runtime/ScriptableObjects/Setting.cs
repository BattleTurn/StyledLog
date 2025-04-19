
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
        [Tooltip("Enable debug log on editor mode.")]
        private bool _isTestingDebugMode = false;


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

        public static bool IsTestingDebugMode
        {
            get
            {
                if (_instance == null)
                {
                    return false; // Default to false if instance is not found
                }

                return Instance._isTestingDebugMode;
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
    }
}