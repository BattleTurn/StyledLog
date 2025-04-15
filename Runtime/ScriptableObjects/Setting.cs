
// Runtime part - needs to be outside the Editor namespace
using UnityEngine;

namespace Colorful.ScriptableObjects
{
    [CreateAssetMenu(fileName = "DebugSetting", menuName = "Colorful/DebugSetting", order = 1)]
    public sealed class Setting : ScriptableObject
    {
        private static Setting _instance;

        [SerializeField]
        private bool _isDebugLogEnable = true;

        public static Setting Instance
        {
            get
            {
                return _instance;
            }
        }

        public static bool IsDebugLogEnable
        {
            get
            {
                if (_instance == null)
                {
                    return true; // Default to true if instance is not found
                }

                return Instance._isDebugLogEnable;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnRuntimeStart()
        {
            Debug.LogHex("UnitTest initialized at runtime. This runs when entering play mode.");
        }
    }
}