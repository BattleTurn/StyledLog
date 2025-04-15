using UnityEngine;
using UnityEditor;

namespace ColorfulLog.Editor
{
    // This part runs in the editor
    [InitializeOnLoad]
    public sealed class SettingEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
            // This code runs when the editor starts or scripts are recompiled
            Debug.Log("UnitTest initialized in editor. This runs whenever the editor starts or scripts are recompiled.");
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            // This code runs every editor frame
        }
    }
}