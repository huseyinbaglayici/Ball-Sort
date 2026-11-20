using UnityEditor;
using UnityEditor.SceneManagement;

namespace Project.Editor
{
    // Play in any scene runs Boot first, so services exist before Main opens.
    [InitializeOnLoad]
    public static class BootPlayModeStarter
    {
        private const string MenuPath = "Tools/Project/Always Start From Boot";
        private const string PrefKey = "Project.AlwaysStartFromBoot";
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        // Debug scenes bring what they need and play on their own.
        private const string DebugSceneFolder = "Assets/Scenes/Debug/";

        static BootPlayModeStarter()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += Apply;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void Apply()
        {
            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
            var debug = EditorSceneManager.GetActiveScene().path.StartsWith(DebugSceneFolder);
            EditorSceneManager.playModeStartScene = Enabled && !debug ? boot : null;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) Apply();
        }
    }
}
