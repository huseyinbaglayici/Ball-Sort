using Project.Contexts.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Editor
{
    // Writes the feel showcase scene; it stays out of the build list.
    public static class FeelShowcaseSceneBuilder
    {
        private const string Folder = "Assets/Scenes/Debug";
        private const string ScenePath = Folder + "/FeelShowcase.unity";

        [MenuItem("Tools/Project/Create Feel Showcase Scene")]
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scenes", "Debug");

            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            // Only until the game's own scenes load.
            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            new GameObject("FeelShowcase").AddComponent<FeelShowcase>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (active.IsValid()) SceneManager.SetActiveScene(active);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[FeelShowcaseSceneBuilder] {ScenePath} is ready; open it and press Play.");
        }
    }
}
