
// Runtime part - needs to be outside the Editor namespace
using UnityEngine;

namespace Colorful.ScriptableObjects
{
    [CreateAssetMenu(fileName = "DebugSetting", menuName = "Colorful/DebugSetting", order = 1)]
    public sealed class Setting : ScriptableObject
    {
        [SerializeField]
        private bool _enableDebugLog = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnRuntimeStart()
        {
            Debug.Log("UnitTest initialized at runtime. This runs when entering play mode.");
        }
    }
}