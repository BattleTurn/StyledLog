
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace BattleTurn.StyledLog.Editor
{
    internal sealed class StyledConsoleBuildHook : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            StyledConsoleController.EnsurePrefsLoaded();
            if (StyledConsoleController.ClearOnBuild)
            {
                StyledConsoleController.ClearAllStorage();
            }
        }
    }
}