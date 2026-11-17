using System.Collections.Generic;
using Project.Contexts.Game.Items;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEditor.U2D;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace Project.Editor
{
    /// <summary>
    /// Builds the tube prefab from the art in Art/Sprites/Tube, the ball prefabs from the orb art in ArtSource/Orb and the
    /// <see cref="ItemRegistry"/> that maps item types to them. Safe to run again: assets are overwritten in place.
    /// </summary>
    public static class BoardPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs/Items";
        private const string RegistryPath = "Assets/Configs/ItemRegistry.asset";
        private const string OrbSourceFolder = "ArtSource/Orb";
        private const string OrbFolder = "Assets/Art/Sprites/Orb";
        private const string TintShaderName = "Project/Sprite Tint Mask";
        private const string OrbTintPath = "Assets/Art/Materials/OrbTint.mat";
        private const string TubeFolder = "Assets/Art/Sprites/Tube";
        private const string AdditivePath = "Assets/Art/Materials/SpriteAdditive.mat";
        private const string AtlasPath = "Assets/Art/Atlases/GameplayAtlas.spriteatlasv2";

        [MenuItem("Tools/Project/Create Board Prefabs")]
        public static void Create()
        {
            var orb = BuildOrb();
            var bottle = SavePrefab(CreateBottle(), "Bottle");
            var ball = SavePrefab(CreateBall(orb, ItemType.Ball), "Ball");
            var question = SavePrefab(CreateBall(orb, ItemType.QuestionMark), "BallQuestionMark");

            BuildRegistry(new List<(ItemType, Item, int, int)>
            {
                (ItemType.Bottle, bottle.GetComponent<Item>(), 12, 16),
                (ItemType.Ball, ball.GetComponent<Item>(), 40, 64),
                (ItemType.QuestionMark, question.GetComponent<Item>(), 24, 64)
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[BoardPrefabBuilder] Bottle, Ball, BallQuestionMark prefabs and ItemRegistry are ready.");
        }

        // The orb is the art team's finished ball, recolored per ball. ArtSource/Orb holds their flattened comp of the
        // turquoise ball on a flat background (Orb_Comp_Cyan) and the five layers it was made from. Rebuilding it from
        // the layers cannot match the comp (their file has masks and effects the layer export does not carry), so the
        // sprite is made from the comp itself, one texture and one draw per ball.
        // Every pixel of the comp is background showing through plus light the ball adds:
        //   comp = bg * u + white * (1,1,1) + amount * base,   base = #00FFEB (the color the art is drawn in)
        // The base color has no red and its blue differs from the background's, so the three channels solve the three
        // unknowns exactly: red gives white, green gives amount, blue gives u. The sprite stores R = white,
        // G = amount, A = 1 - u (premultiplied), and the Sprite Tint Mask shader draws white + amount * ball color.
        // The layers only mark where the lit core is (never see-through) and where nothing is drawn (background only).
        private const string OrbCompName = "Orb_Comp_Cyan";
        private static readonly Color OrbBaseColor = new(0f, 1f, 235f / 255f);
        private static readonly string[] OrbLayerNames = { "OuterGlow", "Back", "Core", "InnerGlow", "Front" };
        // Width of the orb's sphere (the dark frame ring) on the 512 px canvas; the halo reaches past it.
        private const float OrbSpherePx = 328f;
        // Pulls mip selection about 0.7 levels sharper, so the ball reads crisp at its ~110 px size on a phone.
        private const float OrbMipBias = -0.7f;

        private static Sprite BuildOrb()
        {
            if (!AssetDatabase.IsValidFolder(OrbFolder)) AssetDatabase.CreateFolder("Assets/Art/Sprites", "Orb");
            var path = $"{OrbFolder}/Orb.png";
            if (!WriteOrbMask(path)) return null;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return ImportOrbSprite(path);
        }

        private static Color[] LoadPixels(string path, out int width, out int height)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(System.IO.File.ReadAllBytes(path));
            width = texture.width;
            height = texture.height;
            var pixels = texture.GetPixels();
            Object.DestroyImmediate(texture);
            return pixels;
        }

        private static bool WriteOrbMask(string path)
        {
            var compPath = $"{OrbSourceFolder}/{OrbCompName}.png";
            if (!System.IO.File.Exists(compPath))
            {
                Debug.LogError($"[BoardPrefabBuilder] Missing {compPath}.");
                return false;
            }
            var comp = LoadPixels(compPath, out var width, out var height);
            var layers = new Color[OrbLayerNames.Length][];
            var inner = -1;
            for (var i = 0; i < OrbLayerNames.Length; i++)
            {
                var layerPath = $"{OrbSourceFolder}/Orb_{OrbLayerNames[i]}_Cyan.png";
                if (!System.IO.File.Exists(layerPath))
                {
                    Debug.LogError($"[BoardPrefabBuilder] Missing {layerPath}.");
                    return false;
                }
                layers[i] = LoadPixels(layerPath, out var w, out var h);
                if (w != width || h != height)
                {
                    Debug.LogError($"[BoardPrefabBuilder] {layerPath} is {w}x{h}, the comp is {width}x{height}.");
                    return false;
                }
                if (OrbLayerNames[i] == "InnerGlow") inner = i;
            }

            // The comp's flat background, from its four corners.
            var bg = Color.clear;
            var samples = 0;
            const int patch = 12;
            foreach (var (cx, cy) in new[] { (0, 0), (width - patch, 0), (0, height - patch), (width - patch, height - patch) })
            for (var y = cy; y < cy + patch; y++)
            for (var x = cx; x < cx + patch; x++)
            {
                bg += comp[y * width + x];
                samples++;
            }
            bg /= samples;
            var b = OrbBaseColor;
            var denominator = bg.b - bg.r - b.b * (bg.g - bg.r);
            if (Mathf.Abs(denominator) < 0.05f)
            {
                Debug.LogError("[BoardPrefabBuilder] The comp's background must be a color whose blue differs from the " +
                               "ball's base color (e.g. dark navy); it cannot be separated from this one.");
                return false;
            }

            var mask = new Color[comp.Length];
            for (var i = 0; i < comp.Length; i++)
            {
                var c = comp[i];
                var u = Mathf.Clamp01((c.b - c.r - b.b * (c.g - c.r)) / denominator);
                // The lit core (InnerGlow's solid disc) never lets the background through.
                u *= 1f - Mathf.Clamp01((layers[inner][i].a - 0.6f) / 0.3f);
                var empty = 1f;
                foreach (var layer in layers) empty *= 1f - layer[i].a;
                // Nothing is drawn there: background only. Clear it, or the comp's noise would add light over the
                // whole sprite quad and show its edges.
                if (1f - empty < 0.02f)
                {
                    mask[i] = Color.clear;
                    continue;
                }
                var white = Mathf.Clamp01(c.r - bg.r * u);
                var amount = Mathf.Clamp01(c.g - bg.g * u - white);
                mask[i] = new Color(white, amount, 0f, 1f - u);
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(mask);
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return true;
        }

        private static Sprite ImportOrbSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // At scale 1 the sphere is exactly one ball wide.
            importer.spritePixelsPerUnit = OrbSpherePx / TubeArt.BallDiameter;
            // Full 512 px source, mipmapped against shimmer, but with Kaiser mips and a negative bias: plain trilinear
            // blends in the half-size mip at the ball's on-screen size and the ball looks blurry.
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.mipMapBias = OrbMipBias;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            // A premultiplied mask: the halo is light where alpha is 0, so its color must not be replaced there.
            importer.alphaIsTransparency = false;
            importer.sRGBTexture = false;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Material EnsureTintMaterial()
        {
            var shader = Shader.Find(TintShaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(OrbTintPath);
            if (material == null)
            {
                material = new Material(shader) { name = "OrbTint" };
                AssetDatabase.CreateAsset(material, OrbTintPath);
            }
            material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
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
        /// A ball: the orb sprite under a Visual child (the transform squash and smear scale), at sorting order 1 inside
        /// the tube, and for a QuestionMark unit a "?" above it.
        /// </summary>
        private static GameObject CreateBall(Sprite orb, ItemType type)
        {
            var root = new GameObject(type == ItemType.Ball ? "Ball" : "BallQuestionMark");
            var item = root.AddComponent<BallItem>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var renderer = AddSprite(visual, "Orb", orb, 1, EnsureTintMaterial());
            var layers = new List<SpriteRenderer> { renderer };
            var tinted = new List<SpriteRenderer> { renderer };

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
            SetArray(so.FindProperty("_tinted"), tinted);
            so.FindProperty("_questionMark").objectReferenceValue = mark;
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
