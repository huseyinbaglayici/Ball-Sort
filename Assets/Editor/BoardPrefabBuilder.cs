using System.Collections.Generic;
using Project.Contexts.Game.Items;
using Project.Contexts.Game.Levels;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEditor.U2D;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace Project.Editor
{
    /// <summary>
    /// Builds the tube prefab from the art in Art/Sprites/Tube, the ball prefabs and the <see cref="LevelPalette"/> from
    /// the orb art in Art/Sprites/Orb, and the <see cref="ItemRegistry"/> that maps item types to them. Safe to run
    /// again: assets are overwritten in place.
    /// </summary>
    public static class BoardPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs/Items";
        private const string RegistryPath = "Assets/Configs/ItemRegistry.asset";
        private const string PalettePath = "Assets/Configs/LevelPalette.asset";
        private const string OrbFolder = "Assets/Art/Sprites/Orb";
        private const string OrbGeneratedFolder = OrbFolder + "/Generated";
        private const string OrbShaderName = "Project/Orb Layer";
        private const string OrbLayerPath = "Assets/Art/Materials/OrbLayer.mat";
        private const string OrbAdditivePath = "Assets/Art/Materials/OrbLayerAdditive.mat";
        private const string TubeFolder = "Assets/Art/Sprites/Tube";
        private const string AdditivePath = "Assets/Art/Materials/SpriteAdditive.mat";
        private const string AtlasPath = "Assets/Art/Atlases/GameplayAtlas.spriteatlasv2";

        [MenuItem("Tools/Project/Create Board Prefabs")]
        public static void Create()
        {
            var skins = new OrbSkin[OrbColors.Length];
            for (var i = 0; i < OrbColors.Length; i++) skins[i] = LoadOrbSkin(OrbColors[i].name, OrbColors[i].color);
            var hidden = LoadOrbSkin(HiddenOrb.name, HiddenOrb.color);
            if (System.Array.Exists(skins, IsIncomplete) || IsIncomplete(hidden)) return;
            var back = ImportOrbSprite($"{OrbFolder}/Orb_Back.png");
            var front = ImportOrbSprite($"{OrbFolder}/Orb_Front.png", sharp: true);
            if (back == null || front == null) return;
            BuildPalette(skins);

            var bottle = SavePrefab(CreateBottle(), "Bottle");
            var ball = SavePrefab(CreateBall(ItemType.Ball, back, front, skins[0], hidden), "Ball");
            var question = SavePrefab(CreateBall(ItemType.QuestionMark, back, front, skins[0], hidden), "BallQuestionMark");

            BuildRegistry(new List<(ItemType, Item, int, int)>
            {
                (ItemType.Bottle, bottle.GetComponent<Item>(), 12, 16),
                (ItemType.Ball, ball.GetComponent<Item>(), 40, 64),
                (ItemType.QuestionMark, question.GetComponent<Item>(), 24, 64)
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[BoardPrefabBuilder] Bottle, Ball, BallQuestionMark prefabs, LevelPalette and ItemRegistry are ready.");
        }

        // The ball colors, by level color index. 0-7 are the art team's scale: each has its own OuterGlow, Core and
        // InnerGlow (Art/Sprites/Orb/Orb_<Layer>_<Name>.png; their spec is ArtSource/Orb/README_Orbs.txt). Levels use all
        // twelve, so a color without art yet (8-11, and the hidden ball) is made from the Cyan art recolored to it, into
        // Orb/Generated. Put the art team's files in Art/Sprites/Orb and run again to replace a stand-in.
        private static readonly (string name, Color color)[] OrbColors =
        {
            ("Red", Hex("FF120D")), ("Blue", Hex("1C1FFF")), ("Green", Hex("B3FF0D")), ("Yellow", Hex("FFC40D")),
            ("Purple", Hex("A836FF")), ("Orange", Hex("FF6B0D")), ("Cyan", Hex("00FFEB")), ("Pink", Hex("FF70D9")),
            ("Fuchsia", Hex("FF0D8A")), ("White", Hex("F2F2FF")), ("Emerald", Hex("0DFF5E")), ("SkyBlue", Hex("0DA6FF"))
        };
        private static readonly (string name, Color color) HiddenOrb = ("Hidden", Hex("525769"));
        // Fully saturated and bright, so its saturation and value scale straight onto any color.
        private const string RecolorSource = "Cyan";
        private static readonly string[] OrbColorLayers = { "OuterGlow", "Core", "InnerGlow" };

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.magenta;

        private static bool IsIncomplete(OrbSkin skin) => skin.OuterGlow == null || skin.Core == null || skin.InnerGlow == null;

        private static OrbSkin LoadOrbSkin(string name, Color color)
        {
            var sprites = new Sprite[OrbColorLayers.Length];
            for (var i = 0; i < OrbColorLayers.Length; i++)
            {
                var file = $"Orb_{OrbColorLayers[i]}_{name}.png";
                var path = $"{OrbFolder}/{file}";
                if (!System.IO.File.Exists(path))
                {
                    path = $"{OrbGeneratedFolder}/{file}";
                    if (!WriteRecolor($"{OrbFolder}/Orb_{OrbColorLayers[i]}_{RecolorSource}.png", path, color)) return default;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
                sprites[i] = ImportOrbSprite(path);
            }
            return new OrbSkin { OuterGlow = sprites[0], Core = sprites[1], InnerGlow = sprites[2] };
        }

        /// <summary>The source layer with each pixel's hue set to the color's, its saturation and value scaled by the color's.</summary>
        private static bool WriteRecolor(string source, string target, Color color)
        {
            if (!System.IO.File.Exists(source))
            {
                Debug.LogError($"[BoardPrefabBuilder] Missing {source}.");
                return false;
            }
            if (!AssetDatabase.IsValidFolder(OrbGeneratedFolder)) AssetDatabase.CreateFolder(OrbFolder, "Generated");
            Color.RGBToHSV(color, out var hue, out var saturation, out var value);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(System.IO.File.ReadAllBytes(source));
            var pixels = texture.GetPixels();
            for (var i = 0; i < pixels.Length; i++)
            {
                Color.RGBToHSV(pixels[i], out _, out var s, out var v);
                var rgb = Color.HSVToRGB(hue, s * saturation, v * value);
                pixels[i] = new Color(rgb.r, rgb.g, rgb.b, pixels[i].a);
            }
            texture.SetPixels(pixels);
            System.IO.File.WriteAllBytes(target, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return true;
        }

        /// <summary>
        /// Imports an orb layer at the tube's pixels per unit, as the art is drawn (pivot at the center). The soft layers
        /// are compressed; a <paramref name="sharp"/> one (the glass reflection's thin highlights) stays uncompressed so
        /// compression never makes it blocky.
        /// </summary>
        private static Sprite ImportOrbSprite(string path, bool sharp = false)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogError($"[BoardPrefabBuilder] Missing {path}.");
                return null;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TubeArt.PixelsPerUnit;
            // Drawn at about half its size on a phone: mipmapped and trilinear against shimmer.
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = sharp ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>The orb layer material: normal (premultiplied) blending, or additive for the glass.</summary>
        private static Material EnsureOrbMaterial(string path, BlendMode destination)
        {
            var shader = Shader.Find(OrbShaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetFloat("_DstBlend", (float)destination);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildPalette(OrbSkin[] skins)
        {
            var palette = AssetDatabase.LoadAssetAtPath<LevelPalette>(PalettePath);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<LevelPalette>();
                AssetDatabase.CreateAsset(palette, PalettePath);
            }
            var so = new SerializedObject(palette);
            var entries = so.FindProperty("_entries");
            entries.arraySize = OrbColors.Length;
            for (var i = 0; i < OrbColors.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Name").stringValue = OrbColors[i].name;
                entry.FindPropertyRelative("Color").colorValue = OrbColors[i].color;
                SetOrbSkin(entry.FindPropertyRelative("Orb"), skins[i]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(palette);
        }

        private static void SetOrbSkin(SerializedProperty property, OrbSkin skin)
        {
            property.FindPropertyRelative(nameof(OrbSkin.OuterGlow)).objectReferenceValue = skin.OuterGlow;
            property.FindPropertyRelative(nameof(OrbSkin.Core)).objectReferenceValue = skin.Core;
            property.FindPropertyRelative(nameof(OrbSkin.InnerGlow)).objectReferenceValue = skin.InnerGlow;
        }

        /// <summary>
        /// The tube: a SortingGroup on the root, a Container with
        /// Back (normal: glass and base), Mouth (the whole open mouth), the balls in between, Front (additive glass),
        /// Outline (normal), RimFront (the front half of the mouth, sliced from the Mouth by ArtSource/Tube/slice_mouth.py
        /// and drawn over the balls so a ball going through the mouth is half inside) and a Stopper with a Gem for the
        /// completed cap. The tube layers share the art's canvas, so they sit at the Container origin; the cap has its
        /// own canvas and sits at TubeArt.CapLocalY. The root is the bottom of the tube.
        /// </summary>
        private static GameObject CreateBottle()
        {
            // The tube body is packed in the gameplay atlas (compressed there), while the thin glowing outline stays a
            // separate, uncompressed texture so compression never makes it blocky.
            var back = ImportTubeSprite("Tube_Back");
            var front = ImportTubeSprite("Tube_Front");
            var outline = ImportTubeSprite("Tube_Outline");
            var mouth = ImportTubeSprite("Tube_Mouth");
            var rimFront = ImportTubeSprite("Tube_RimFront");
            var cap = ImportTubeSprite("Tube_Stopper");
            var gem = ImportTubeSprite("Tube_Gem");
            BuildGameplayAtlas(back, front, mouth, rimFront, cap, gem);

            var root = new GameObject("Bottle");
            var item = root.AddComponent<BottleItem>();
            var collider = root.GetComponent<BoxCollider2D>();
            var group = root.GetComponent<SortingGroup>();

            var container = new GameObject("Container").transform;
            container.SetParent(root.transform, false);
            container.localPosition = new Vector3(0f, TubeArt.ContainerLocalY, 0f);

            var backRenderer = AddSprite(container, "Back", back, TubeArt.BackOrder, null);
            var mouthRenderer = AddSprite(container, "Mouth", mouth, TubeArt.MouthOrder, null);
            var frontRenderer = AddSprite(container, "Front", front, TubeArt.FrontOrder, EnsureAdditiveMaterial());
            var outlineRenderer = AddSprite(container, "Outline", outline, TubeArt.OutlineOrder, null);
            var rimFrontRenderer = AddSprite(container, "RimFront", rimFront, TubeArt.RimFrontOrder, null);

            var stopper = AddSprite(container, "Stopper", cap, TubeArt.StopperOrder, null);
            stopper.transform.localPosition = new Vector3(0f, TubeArt.CapLocalY, 0f);
            stopper.enabled = false;
            var gemRenderer = AddSprite(stopper.transform, "Gem", gem, TubeArt.GemOrder, null);
            gemRenderer.enabled = false;

            var so = new SerializedObject(item);
            so.FindProperty("_back").objectReferenceValue = backRenderer;
            so.FindProperty("_front").objectReferenceValue = frontRenderer;
            so.FindProperty("_outline").objectReferenceValue = outlineRenderer;
            so.FindProperty("_mouth").objectReferenceValue = mouthRenderer;
            so.FindProperty("_rimFront").objectReferenceValue = rimFrontRenderer;
            so.FindProperty("_stopper").objectReferenceValue = stopper;
            so.FindProperty("_gem").objectReferenceValue = gemRenderer;
            so.FindProperty("_collider").objectReferenceValue = collider;
            so.FindProperty("_sortingGroup").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, int order, Material material)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            if (material != null) renderer.sharedMaterial = material;
            return renderer;
        }

        /// <summary>Imports a tube sprite with the pixels per unit the slot math in BottleItem is written for.</summary>
        private static Sprite ImportTubeSprite(string name)
        {
            var path = $"{TubeFolder}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogError($"[BoardPrefabBuilder] Missing {path}.");
                return null;
            }

            // The art is drawn well below its source size on a phone and smaller still in the editor Game view, so it is
            // mipmapped and trilinear; without mips its thin edges and the outline alias into jagged pixels. The source
            // stays uncompressed: atlas members are compressed by the atlas, and the outline is used as it is.
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TubeArt.PixelsPerUnit;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 1;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// Packs the tube body: 2048 pages, mipmapped and trilinear (8 px padding so smaller mips do not bleed), ASTC 6x6 on
        /// phones. Standalone uses BC7 so the editor shows the same quality as a device.
        /// </summary>
        private static void BuildGameplayAtlas(params Sprite[] sprites)
        {
            // Rewritten on every run, so its contents are exactly the given sprites.
            var asset = new SpriteAtlasAsset();
            asset.Add(System.Array.ConvertAll(sprites, s => (Object)s));
            SpriteAtlasAsset.Save(asset, AtlasPath);
            AssetDatabase.ImportAsset(AtlasPath);

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                padding = 8, enableRotation = false, enableTightPacking = false, enableAlphaDilation = true
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                generateMipMaps = true, filterMode = FilterMode.Trilinear, anisoLevel = 1, sRGB = true, readable = false
            };
            importer.SetPlatformSettings(AtlasPlatform("DefaultTexturePlatform", TextureImporterFormat.Automatic, false));
            importer.SetPlatformSettings(AtlasPlatform("Android", TextureImporterFormat.ASTC_6x6, true));
            importer.SetPlatformSettings(AtlasPlatform("iPhone", TextureImporterFormat.ASTC_6x6, true));
            importer.SetPlatformSettings(AtlasPlatform("Standalone", TextureImporterFormat.BC7, true));
            importer.includeInBuild = true;
            importer.SaveAndReimport();
        }

        private static TextureImporterPlatformSettings AtlasPlatform(string platform, TextureImporterFormat format, bool overridden) =>
            new()
            {
                name = platform,
                overridden = overridden,
                maxTextureSize = 2048,
                format = format,
                textureCompression = TextureImporterCompression.CompressedHQ
            };

        /// <summary>Additive sprite material for the glass.</summary>
        private static Material EnsureAdditiveMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AdditivePath);
            if (material != null) return material;
            material = new Material(Shader.Find("Mobile/Particles/Additive")) { name = "SpriteAdditive" };
            AssetDatabase.CreateAsset(material, AdditivePath);
            return material;
        }

        /// <summary>
        /// A ball: the five orb layers, all at the same spot (bottom to top OuterGlow, Back, Core, Front additive,
        /// InnerGlow, at sorting orders 1-5 inside the tube) under a Visual child (the transform squash and smear scale),
        /// and for a QuestionMark unit a "?" above them. The prefab shows <paramref name="sample"/>; each ball sets its own.
        /// </summary>
        private static GameObject CreateBall(ItemType type, Sprite back, Sprite front, OrbSkin sample, OrbSkin hidden)
        {
            var root = new GameObject(type == ItemType.Ball ? "Ball" : "BallQuestionMark");
            var item = root.AddComponent<BallItem>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var normal = EnsureOrbMaterial(OrbLayerPath, BlendMode.OneMinusSrcAlpha);
            var additive = EnsureOrbMaterial(OrbAdditivePath, BlendMode.One);
            var outerGlow = AddSprite(visual, "OuterGlow", sample.OuterGlow, 1, normal);
            var backRenderer = AddSprite(visual, "Back", back, 2, normal);
            var core = AddSprite(visual, "Core", sample.Core, 3, normal);
            var frontRenderer = AddSprite(visual, "Front", front, 4, additive);
            var innerGlow = AddSprite(visual, "InnerGlow", sample.InnerGlow, 5, normal);
            var layers = new List<SpriteRenderer> { outerGlow, backRenderer, core, frontRenderer, innerGlow };

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
                text.sortingOrder = 1 + layers.Count;
                text.rectTransform.sizeDelta = new Vector2(1f, 1f);
            }

            var so = new SerializedObject(item);
            so.FindProperty("_visual").objectReferenceValue = visual;
            SetArray(so.FindProperty("_layers"), layers);
            so.FindProperty("_outerGlow").objectReferenceValue = outerGlow;
            so.FindProperty("_core").objectReferenceValue = core;
            so.FindProperty("_innerGlow").objectReferenceValue = innerGlow;
            so.FindProperty("_questionMark").objectReferenceValue = mark;
            SetOrbSkin(so.FindProperty("_hiddenOrb"), hidden);
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static void SetArray(SerializedProperty property, List<SpriteRenderer> values)
        {
            property.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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
