using UnityEditor;
using UnityEditor.SceneManagement;

namespace Project.Editor
{
    /// <summary>
    /// "Always start from Boot": pressing Play in any scene still runs the Boot scene first, so services exist
    /// before Main opens.
    /// </summary>
    [InitializeOnLoad]
    public static class BootPlayModeStarter
    {
        private const string MenuPath = "Tools/Project/Always Start From Boot";
        private const string PrefKey = "Project.AlwaysStartFromBoot";
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

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
            EditorSceneManager.playModeStartScene = Enabled ? boot : null;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) Apply();
        }
    }
}
