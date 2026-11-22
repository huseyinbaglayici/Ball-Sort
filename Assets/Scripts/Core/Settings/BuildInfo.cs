using UnityEngine;

namespace Project.Core.Settings
{
    // Package version and build number, written from the player settings before each build.
    [CreateAssetMenu(menuName = "Project/Build Info", fileName = "BuildInfo")]
    public sealed class BuildInfo : ScriptableObject
    {
        [SerializeField] private string _version = "";
        [SerializeField] private string _build = "";

        public string Version => string.IsNullOrEmpty(_version) ? Application.version : _version;
        public string Build => _build;

        // "1.0 (12)", or the version alone without a build number.
        public string Label => string.IsNullOrEmpty(_build) ? Version : $"{Version} ({_build})";

        public void Set(string version, string build)
        {
            _version = version;
            _build = build;
        }
    }
}
