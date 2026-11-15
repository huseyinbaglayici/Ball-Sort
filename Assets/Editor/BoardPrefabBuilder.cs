using System.Collections.Generic;
using Project.Contexts.Game.Items;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Project.Editor
{
    /// <summary>
    /// Builds the graybox bottle and ball prefabs (built-in rounded sprite, tinted by code) and the
    /// <see cref="ItemRegistry"/> that maps item types to them. Safe to run again: assets are overwritten in place.
    /// </summary>
    public static class BoardPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs/Items";
        private const string RegistryPath = "Assets/Configs/ItemRegistry.asset";
        private const string CirclePath = "Assets/Art/Sprites/Circle.png";

        [MenuItem("Tools/Project/Create Board Prefabs")]
        public static void Create()
        {
            var glassSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var circle = EnsureCircleSprite();
            var bottle = SavePrefab(CreateBottle(glassSprite), "Bottle");
            var ball = SavePrefab(CreateBall(circle, ItemType.Ball), "Ball");
            var question = SavePrefab(CreateBall(circle, ItemType.QuestionMark), "BallQuestionMark");

            BuildRegistry(new List<(ItemType, Item, int, int)>
            {
                (ItemType.Bottle, bottle.GetComponent<Item>(), 12, 16),
                (ItemType.Ball, ball.GetComponent<Item>(), 40, 64),
                (ItemType.QuestionMark, question.GetComponent<Item>(), 24, 64)
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[BoardPrefabBuilder] Bottle, Ball, BallQuestionMark prefabs and ItemRegistry are ready.");
        }

        /// <summary>Uses Assets/Art/Sprites/Circle.png; creates a plain white 128 px circle only when it does not exist.</summary>
        private static Sprite EnsureCircleSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
            if (existing != null) return existing;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var radius = size * 0.5f - 1f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var alpha = Mathf.Clamp01(radius - Vector2.Distance(new Vector2(x, y), center) + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            System.IO.File.WriteAllBytes(CirclePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(CirclePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(CirclePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
        }

        private static GameObject CreateBottle(Sprite sprite)
        {
            var root = new GameObject("Bottle");
            var item = root.AddComponent<BottleItem>();
            var collider = root.GetComponent<BoxCollider2D>();

            var glass = new GameObject("Glass").AddComponent<SpriteRenderer>();
            glass.transform.SetParent(root.transform, false);
            glass.sprite = sprite;
            glass.drawMode = SpriteDrawMode.Sliced;
            glass.color = BottleItem.NormalGlassColor;
            glass.sortingOrder = 0;

            var so = new SerializedObject(item);
            so.FindProperty("_glass").objectReferenceValue = glass;
            so.FindProperty("_collider").objectReferenceValue = collider;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject CreateBall(Sprite circle, ItemType type)
        {
            var root = new GameObject(type == ItemType.Ball ? "Ball" : "BallQuestionMark");
            var item = root.AddComponent<BallItem>();

            var renderer = new GameObject("Ball").AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(root.transform, false);
            renderer.sprite = circle;
            renderer.sortingOrder = 1;

            GameObject mark = null;
            if (type == ItemType.QuestionMark)
            {
                mark = new GameObject("QuestionMark");
                mark.transform.SetParent(root.transform, false);
                var text = mark.AddComponent<TextMeshPro>();
                text.text = "?";
                text.fontSize = 5f;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.sortingOrder = 2;
                text.rectTransform.sizeDelta = new Vector2(1f, 1f);
            }

            var so = new SerializedObject(item);
            so.FindProperty("_type").enumValueIndex = (int)type;
            so.FindProperty("_renderer").objectReferenceValue = renderer;
            so.FindProperty("_questionMark").objectReferenceValue = mark;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject SavePrefab(GameObject instance, string name)
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Items");
            var path = $"{PrefabFolder}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static void BuildRegistry(List<(ItemType type, Item prefab, int warm, int max)> entries)
        {
            var registry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<ItemRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }

            var so = new SerializedObject(registry);
            var list = so.FindProperty("_entries");
            list.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Type").enumValueIndex = (int)entries[i].type;
                element.FindPropertyRelative("Prefab").objectReferenceValue = entries[i].prefab;
                element.FindPropertyRelative("WarmCount").intValue = entries[i].warm;
                element.FindPropertyRelative("MaxCount").intValue = entries[i].max;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
        }
    }
}
