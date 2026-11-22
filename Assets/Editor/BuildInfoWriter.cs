using BuildInfo = Project.Core.Settings.BuildInfo;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Project.Editor
{
    // Copies the package version and build number from the player settings into the BuildInfo asset.
    public sealed class BuildInfoWriter : IPreprocessBuildWithReport
    {
        public const string Path = "Assets/Configs/BuildInfo.asset";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Write(report.summary.platform);

        public static BuildInfo Write(BuildTarget target)
        {
            var info = AssetDatabase.LoadAssetAtPath<BuildInfo>(Path);
            if (info == null)
            {
                info = ScriptableObject.CreateInstance<BuildInfo>();
                AssetDatabase.CreateAsset(info, Path);
            }
            var build = target switch
            {
                BuildTarget.Android => PlayerSettings.Android.bundleVersionCode.ToString(),
                BuildTarget.iOS => PlayerSettings.iOS.buildNumber,
                _ => PlayerSettings.Android.bundleVersionCode.ToString()
            };
            if (info.Version == PlayerSettings.bundleVersion && info.Build == build) return info;
            info.Set(PlayerSettings.bundleVersion, build);
            EditorUtility.SetDirty(info);
            AssetDatabase.SaveAssetIfDirty(info);
            return info;
        }
    }
}
