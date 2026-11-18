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
    // Re-running overwrites the assets in place.
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
            var coreGlow = ImportOrbSprite($"{OrbFolder}/Orb_CoreGlow.png");
            if (back == null || front == null || coreGlow == null) return;
            for (var i = 0; i < skins.Length; i++) skins[i].CoreGlow = coreGlow;
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

        // Indexed by the color index levels store, so the order must not change.
        private static readonly (string name, Color color)[] OrbColors =
        {
            ("Red", Hex("CB1610")), ("DarkBlue", Hex("1927C8")), ("LightGreen", Hex("7BAD12")), ("Yellow", Hex("FCC620")),
            ("Violet", Hex("6C25A0")), ("Orange", Hex("E56109")), ("LightBlue", Hex("1BA3EF")), ("Pink", Hex("FC7DD8")),
            ("Wine", Hex("730456")), ("White", Hex("656CAB")), ("DarkGreen", Hex("154404")), ("Brown", Hex("7C362D"))
        };
        // No CoreGlow: the "?" stays readable on it.
        private static readonly (string name, Color color) HiddenOrb = ("Hidden", Hex("525769"));
        // Saturated and bright, so its saturation and value scale onto any color.
        private const string RecolorSource = "LightBlue";
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

        // Hue from the color; saturation and value scaled by the color's.
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

        // Sharp layers (thin glass highlights) stay uncompressed so they never turn blocky.
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
            importer.spritePixelsPerUnit = TubeArt.OrbSpherePx / TubeArt.BallDiameter;
            // Drawn at about half size on a phone: mipmapped and trilinear against shimmer.
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

        // Premultiplied blending, or additive for the glass.
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
            property.FindPropertyRelative(nameof(OrbSkin.CoreGlow)).objectReferenceValue = skin.CoreGlow;
            property.FindPropertyRelative(nameof(OrbSkin.InnerGlow)).objectReferenceValue = skin.InnerGlow;
        }

        // RimFront (the mouth's front half) draws over the balls, so a ball in the mouth looks half inside.
        private static GameObject CreateBottle()
        {
            // The thin outline stays out of the compressed atlas so it never turns blocky.
            var back = ImportTubeSprite("Tube_Back");
            var front = ImportTubeSprite("Tube_Front");
            var outline = ImportTubeSprite("Tube_Outline");
            var mouth = ImportTubeSprite("Tube_Mouth");
            var rimFront = ImportTubeSprite("Tube_RimFront");
            var cap = ImportTubeSprite("Tube_Stopper");
            var gem = ImportTubeSprite("Tube_Gem");
            // Soft light survives compression, so the glows can go in the atlas.
            WriteOutlineGlow();
            WriteCapGlow();
            var outlineGlow = ImportTubeSprite(OutlineGlowName, GlowResolution);
            var capGlow = ImportTubeSprite(CapGlowName, CapGlowPx / CapGlowWidthPx);
            BuildGameplayAtlas(back, front, mouth, rimFront, cap, gem, outlineGlow, capGlow);

            var root = new GameObject("Bottle");
            var item = root.AddComponent<BottleItem>();
            var collider = root.GetComponent<BoxCollider2D>();
            var group = root.GetComponent<SortingGroup>();

            var container = new GameObject("Container").transform;
            container.SetParent(root.transform, false);
            container.localPosition = new Vector3(0f, TubeArt.ContainerLocalY, 0f);

            var backRenderer = AddSprite(container, "Back", back, TubeArt.BackOrder, null);
            var mouthRenderer = AddSprite(container, "Mouth", mouth, TubeArt.MouthOrder, null);
            var frontBehind = AddSprite(container, "FrontBehind", front, TubeArt.GlassBehindOrder, EnsureAdditiveMaterial());
            frontBehind.color = new Color(1f, 1f, 1f, 1f - TubeArt.GlassOverBalls);
            var frontRenderer = AddSprite(container, "Front", front, TubeArt.FrontOrder, EnsureAdditiveMaterial());
            frontRenderer.color = new Color(1f, 1f, 1f, TubeArt.GlassOverBalls);
            var outlineRenderer = AddSprite(container, "Outline", outline, TubeArt.OutlineOrder, null);
            var rimFrontRenderer = AddSprite(container, "RimFront", rimFront, TubeArt.RimFrontOrder, null);

            var stopper = AddSprite(container, "Stopper", cap, TubeArt.StopperOrder, null);
            stopper.transform.localPosition = new Vector3(0f, TubeArt.CapLocalY, 0f);
            stopper.enabled = false;
            var gemRenderer = AddSprite(stopper.transform, "Gem", gem, TubeArt.GemOrder, null);
            gemRenderer.enabled = false;

            var additive = EnsureAdditiveMaterial();
            var outlineGlowRenderer = AddSprite(container, "OutlineGlow", outlineGlow, TubeArt.OutlineGlowOrder, additive);
            outlineGlowRenderer.enabled = false;
            var capGlowRenderer = AddSprite(stopper.transform, "CapGlow", capGlow, TubeArt.CapGlowOrder, additive);
            capGlowRenderer.enabled = false;

            var so = new SerializedObject(item);
            so.FindProperty("_back").objectReferenceValue = backRenderer;
            so.FindProperty("_front").objectReferenceValue = frontRenderer;
            so.FindProperty("_outline").objectReferenceValue = outlineRenderer;
            so.FindProperty("_mouth").objectReferenceValue = mouthRenderer;
            so.FindProperty("_rimFront").objectReferenceValue = rimFrontRenderer;
            so.FindProperty("_stopper").objectReferenceValue = stopper;
            so.FindProperty("_gem").objectReferenceValue = gemRenderer;
            so.FindProperty("_outlineGlow").objectReferenceValue = outlineGlowRenderer;
            so.FindProperty("_capGlow").objectReferenceValue = capGlowRenderer;
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

        // Padded by GlowPadPx so the blurred outline's center matches the art's.
        private const string OutlineGlowName = "Tube_OutlineGlow";
        private const float GlowResolution = 0.5f;
        private const int GlowPadPx = 32;
        private const int GlowBlurRadius = 6;
        private const float GlowBoost = 1.8f;
        private const string CapGlowName = "Tube_CapGlow";
        private const int CapGlowPx = 128;
        private const float CapGlowWidthPx = 430f;

        private static void WriteOutlineGlow()
        {
            var source = $"{TubeFolder}/Tube_Outline.png";
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(System.IO.File.ReadAllBytes(source));
            var step = Mathf.RoundToInt(1f / GlowResolution);
            var width = texture.width / step + GlowPadPx * 2;
            var height = texture.height / step + GlowPadPx * 2;
            var pixels = texture.GetPixels();
            var alpha = new float[width * height];
            for (var y = 0; y < texture.height / step; y++)
            for (var x = 0; x < texture.width / step; x++)
            {
                var sum = 0f;
                for (var dy = 0; dy < step; dy++)
                for (var dx = 0; dx < step; dx++)
                    sum += pixels[(y * step + dy) * texture.width + x * step + dx].a;
                alpha[(y + GlowPadPx) * width + x + GlowPadPx] = sum / (step * step);
            }
            // Three box blurs each way approximate a gaussian.
            for (var pass = 0; pass < 3; pass++)
            {
                BoxBlur(alpha, width, height, 1, width, GlowBlurRadius);
                BoxBlur(alpha, height, width, width, 1, GlowBlurRadius);
            }
            var max = 0f;
            foreach (var a in alpha) max = Mathf.Max(max, a);
            var glow = new Color[alpha.Length];
            for (var i = 0; i < alpha.Length; i++) glow[i] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha[i] / max * GlowBoost));
            WriteTexture($"{TubeFolder}/{OutlineGlowName}.png", width, height, glow);
            Object.DestroyImmediate(texture);
        }

        private static void BoxBlur(float[] values, int length, int lines, int stride, int lineStride, int radius)
        {
            var line = new float[length];
            for (var l = 0; l < lines; l++)
            {
                var start = l * lineStride;
                for (var i = 0; i < length; i++) line[i] = values[start + i * stride];
                var sum = 0f;
                for (var i = -radius; i <= radius; i++) sum += line[Mathf.Clamp(i, 0, length - 1)];
                for (var i = 0; i < length; i++)
                {
                    values[start + i * stride] = sum / (radius * 2 + 1);
                    sum += line[Mathf.Min(i + radius + 1, length - 1)] - line[Mathf.Max(i - radius, 0)];
                }
            }
        }

        private static void WriteCapGlow()
        {
            var glow = new Color[CapGlowPx * CapGlowPx];
            var half = CapGlowPx * 0.5f;
            for (var y = 0; y < CapGlowPx; y++)
            for (var x = 0; x < CapGlowPx; x++)
            {
                var dx = (x + 0.5f - half) / half;
                var dy = (y + 0.5f - half) / half;
                var d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                // Soft falloff that reaches zero exactly at the edge.
                glow[y * CapGlowPx + x] = new Color(1f, 1f, 1f, Mathf.Exp(-3f * d * d) * (1f - d * d));
            }
            WriteTexture($"{TubeFolder}/{CapGlowName}.png", CapGlowPx, CapGlowPx, glow);
        }

        private static void WriteTexture(string path, int width, int height, Color[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        // Pixels per unit must match BottleItem's slot math, scaled for art made at another resolution.
        private static Sprite ImportTubeSprite(string name, float resolution = 1f)
        {
            var path = $"{TubeFolder}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogError($"[BoardPrefabBuilder] Missing {path}.");
                return null;
            }

            // Drawn well below source size, so mips stop thin edges aliasing; the atlas does the compressing.
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TubeArt.PixelsPerUnit * resolution;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 1;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            // A 9-slice sprite is drawn sliced for long tubes, which needs a full-rect mesh.
            if (settings.spriteBorder != Vector4.zero) settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // 8 px padding so smaller mips do not bleed; BC7 on Standalone so the editor matches device quality.
        private static void BuildGameplayAtlas(params Sprite[] sprites)
        {
            // Rewritten on every run, so it holds exactly the given sprites.
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

        private static Material EnsureAdditiveMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AdditivePath);
            if (material != null) return material;
            material = new Material(Shader.Find("Mobile/Particles/Additive")) { name = "SpriteAdditive" };
            AssetDatabase.CreateAsset(material, AdditivePath);
            return material;
        }

        // Six layers at sorting orders 1-6 under a Visual child that squash and smear scale.
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
            var coreGlow = AddSprite(visual, "CoreGlow", sample.CoreGlow, 4, normal);
            var frontRenderer = AddSprite(visual, "Front", front, 5, additive);
            var innerGlow = AddSprite(visual, "InnerGlow", sample.InnerGlow, 6, normal);
            var layers = new List<SpriteRenderer> { outerGlow, backRenderer, core, coreGlow, frontRenderer, innerGlow };

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
            so.FindProperty("_coreGlow").objectReferenceValue = coreGlow;
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
