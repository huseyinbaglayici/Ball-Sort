using Project.Contexts.Game.Items;
using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Preview;
using Project.Contexts.Game.Sort;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Editor
{
    // Writes the completion effect preview scene; it stays out of the build list.
    public static class TubeFxPreviewSceneBuilder
    {
        private const string Folder = "Assets/Scenes/Debug";
        private const string ScenePath = Folder + "/TubeFxPreview.unity";
        // Same clear color as the game camera.
        private static readonly Color Background = new(0.12f, 0.14f, 0.2f, 1f);

        [MenuItem("Tools/Project/Create Tube FX Preview Scene")]
        public static void Create()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ItemRegistry>("Assets/Configs/ItemRegistry.asset");
            var palette = AssetDatabase.LoadAssetAtPath<LevelPalette>("Assets/Configs/LevelPalette.asset");
            var config = AssetDatabase.LoadAssetAtPath<BallAnimationConfig>("Assets/Configs/BallAnimationConfig.asset");
            if (registry == null || palette == null || config == null)
            {
                Debug.LogError("[TubeFxPreviewSceneBuilder] Missing ItemRegistry, LevelPalette or BallAnimationConfig in Assets/Configs.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scenes", "Debug");

            // Built beside the open scene and closed again, so whatever is being edited stays open.
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            // The booster section drives a real board.
            var boardView = new GameObject("BoosterBoard").AddComponent<BoardView>();
            var boardSo = new SerializedObject(boardView);
            boardSo.FindProperty("_animation").objectReferenceValue = config;
            boardSo.ApplyModifiedPropertiesWithoutUndo();

            var preview = new GameObject("TubeFxPreview").AddComponent<TubeFxPreview>();
            var so = new SerializedObject(preview);
            so.FindProperty("_registry").objectReferenceValue = registry;
            so.FindProperty("_palette").objectReferenceValue = palette;
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_camera").objectReferenceValue = camera;
            so.FindProperty("_boardView").objectReferenceValue = boardView;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (active.IsValid()) SceneManager.SetActiveScene(active);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[TubeFxPreviewSceneBuilder] {ScenePath} is ready; open it and press Play.");
        }
    }
}
