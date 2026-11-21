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
        private const string ObstacleFolder = "Assets/Art/Sprites/Obstacles";
        private const string CountMaterialPath = "Assets/Art/Materials/BombCount.mat";
        // 9-slice borders of the shutter art (left, bottom, right, top).
        private static readonly Vector4 ShutterBorder = new(62f, 0f, 62f, 0f);
        private static readonly Vector4 HousingBorder = new(40f, 0f, 40f, 0f);
        // The padlock's swing pivot, the top of its shackle, above its center.
        private const float ShacklePx = 114f;
        // Bomb counter: white with a dark navy outline, about 70% of the bomb screen's height.
        private const float CountFontSize = 2.4f;
        private static readonly Color CountOutline = new(0.08f, 0.07f, 0.3f, 1f);

        [MenuItem("Tools/Project/Create Board Prefabs")]
        public static void Create()
        {
            var skins = new OrbSkin[OrbColors.Length];
            for (var i = 0; i < OrbColors.Length; i++) skins[i] = LoadOrbSkin(OrbColors[i].name, OrbColors[i].color);
            if (System.Array.Exists(skins, IsIncomplete)) return;
            var back = ImportOrbSprite($"{OrbFolder}/Orb_Back.png");
            var front = ImportOrbSprite($"{OrbFolder}/Orb_Front.png", sharp: true);
            if (back == null || front == null) return;
            var art = ImportObstacleArt();
            if (art == null) return;
            BuildPalette(skins);

            var bottle = SavePrefab(CreateBottle(), "Bottle");
            var ball = SavePrefab(CreateBall(ItemType.Ball, back, front, skins[0], art), "Ball");
            var question = SavePrefab(CreateBall(ItemType.QuestionMark, back, front, skins[0], art), "BallQuestionMark");
            var completeFx = CompleteFxBuilder.Build();
            if (completeFx == null) return;
            var resultFx = ResultFxBuilder.Build();
            if (resultFx == null) return;
            var frozen = SavePrefab(CreateFrozenTube(art, back, front, skins[0]), "FrozenTube");
            var shutter = SavePrefab(CreateShutter(art), "Shutter");
            var blast = SavePrefab(CreateBombBlast(art), "BombBlast");

            BuildRegistry(new List<(ItemType, Item, int, int)>
            {
                (ItemType.Bottle, bottle.GetComponent<Item>(), 12, 16),
                (ItemType.Ball, ball.GetComponent<Item>(), 40, 64),
                (ItemType.QuestionMark, question.GetComponent<Item>(), 24, 64),
                (ItemType.CompleteFx, completeFx, 2, 6),
                (ItemType.ResultFx, resultFx, 0, 2),
                (ItemType.FrozenTube, frozen.GetComponent<Item>(), 0, 8),
                (ItemType.Shutter, shutter.GetComponent<Item>(), 0, 8),
                (ItemType.BombBlast, blast.GetComponent<Item>(), 0, 4)
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[BoardPrefabBuilder] Bottle, Ball, BallQuestionMark, CompleteFx, ResultFx, FrozenTube, Shutter, BombBlast prefabs, LevelPalette and ItemRegistry are ready.");
        }

        // Indexed by the color index levels store, so the order must not change.
        private static readonly (string name, Color color)[] OrbColors =
        {
            ("Red", Hex("CB1610")), ("DarkBlue", Hex("1927C8")), ("LightGreen", Hex("7BAD12")), ("Yellow", Hex("FCC620")),
            ("Violet", Hex("6C25A0")), ("Orange", Hex("E56109")), ("LightBlue", Hex("1BA3EF")), ("Pink", Hex("FC7DD8")),
            ("Wine", Hex("730456")), ("White", Hex("656CAB")), ("DarkGreen", Hex("154404")), ("Brown", Hex("7C362D"))
        };
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
            importer.spritePixelsPerUnit = TubeArt.OrbPixelsPerUnit;
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
                var table = OrbColorTable.IndexOf(OrbColors[i].name);
                entry.FindPropertyRelative("Surface").colorValue =
                    table >= 0 ? OrbColorTable.Entries[table].Surface : OrbColors[i].color;
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

        private static GameObject CreateBottle()
        {
            var glassBorder = new Vector4(0f, TubeArt.GlassBottomBorderPx, 0f, TubeArt.GlassTopBorderPx);
            var back = ImportTubeSprite("Tube_Back_Glass", glassBorder);
            var front = ImportTubeSprite("Tube_Front_Glass", glassBorder);
            var glow = ImportTubeSprite("Tube_Glow", glassBorder);
            var endPiece = ImportTubeSprite("Tube_End_Piece", Vector4.zero);
            var emptyCap = ImportTubeSprite("Tube_Empty_Cap", Vector4.zero);
            var rimFront = ImportTubeSprite("Tube_RimFront", Vector4.zero);
            var completedCap = ImportTubeSprite("Tube_Completed_Cap", Vector4.zero);
            var gem = ImportTubeSprite("Tube_Completed_Gem", Vector4.zero);
            BuildGameplayAtlas(back, front, glow, endPiece, emptyCap, rimFront, completedCap, gem);

            var root = new GameObject("Bottle");
            var item = root.AddComponent<BottleItem>();
            var collider = root.GetComponent<BoxCollider2D>();
            var group = root.GetComponent<SortingGroup>();

            var container = new GameObject("Container").transform;
            container.SetParent(root.transform, false);
            container.localPosition = new Vector3(0f, TubeArt.ContainerLocalY, 0f);

            var additive = EnsureAdditiveMaterial();
            var backRenderer = AddSprite(container, "BackGlass", back, TubeArt.BackOrder, null);
            AddSprite(container, "EndPiece", endPiece, TubeArt.EndPieceOrder, null);
            var frontBehind = AddSprite(container, "FrontGlassBehind", front, TubeArt.GlassBehindOrder, additive);
            var frontRenderer = AddSprite(container, "FrontGlass", front, TubeArt.FrontOrder, additive);
            var glowRenderer = AddSprite(container, "Glow", glow, TubeArt.GlowOrder, null);
            var glowFlash = AddSprite(container, "GlowFlash", glow, TubeArt.GlowFlashOrder, additive);
            glowFlash.enabled = false;
            var emptyCapRenderer = AddSprite(container, "EmptyCap", emptyCap, TubeArt.EmptyCapOrder, null);
            var rimFrontRenderer = AddSprite(container, "RimFront", rimFront, TubeArt.RimFrontOrder, null);

            var completedCapRenderer = AddSprite(container, "CompletedCap", completedCap, TubeArt.CompletedCapOrder, null);
            completedCapRenderer.transform.localPosition = new Vector3(0f, TubeArt.CapLocalY, 0f);
            completedCapRenderer.enabled = false;
            var gemRenderer = AddSprite(completedCapRenderer.transform, "Gem", gem, TubeArt.GemOrder, null);
            gemRenderer.enabled = false;

            var so = new SerializedObject(item);
            so.FindProperty("_back").objectReferenceValue = backRenderer;
            so.FindProperty("_front").objectReferenceValue = frontRenderer;
            so.FindProperty("_frontBehind").objectReferenceValue = frontBehind;
            so.FindProperty("_glow").objectReferenceValue = glowRenderer;
            so.FindProperty("_glowFlash").objectReferenceValue = glowFlash;
            so.FindProperty("_emptyCap").objectReferenceValue = emptyCapRenderer;
            so.FindProperty("_rimFront").objectReferenceValue = rimFrontRenderer;
            so.FindProperty("_completedCap").objectReferenceValue = completedCapRenderer;
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

        // Pixels per unit must match BottleItem's slot math; border is left, bottom, right, top in pixels.
        private static Sprite ImportTubeSprite(string name, Vector4 border)
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
            settings.spriteBorder = border;
            // A 9-slice sprite is drawn sliced for long tubes, which needs a full-rect mesh.
            settings.spriteMeshType = border != Vector4.zero ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
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

        // Orb layers 1-5 under a scaled Visual child, then obstacle overlays by depth: shell, key, bomb, counter.
        private static GameObject CreateBall(ItemType type, Sprite back, Sprite front, OrbSkin sample, ObstacleSprites art)
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

            SpriteRenderer shell = null, shellBreak = null;
            if (type == ItemType.QuestionMark)
            {
                shell = AddOverlay(visual, "Shell", art.Shell, 1, normal);
                shellBreak = AddOverlay(root.transform, "Break", art.Break, 9, normal);
            }
            var keyRoot = new GameObject("Key").transform;
            keyRoot.SetParent(visual, false);
            var keyGlow = AddOverlay(keyRoot, "KeyGlow", art.KeyGlow, 2, normal);
            var key = AddOverlay(keyRoot, "KeyMark", art.Key, 3, normal);
            var bombBase = AddOverlay(visual, "BombBase", art.BombBase, 4, normal);
            var bombRing = AddOverlay(visual, "BombRing", art.BombRing, 5, normal);
            var bombFx = AddOverlay(visual, "BombFx", art.BombFx, 6, normal);
            var defuse = AddOverlay(root.transform, "Defuse", art.Defuse, 8, normal);
            var count = AddCount(root.transform, 7);

            var so = new SerializedObject(item);
            so.FindProperty("_visual").objectReferenceValue = visual;
            SetArray(so.FindProperty("_layers"), layers);
            so.FindProperty("_outerGlow").objectReferenceValue = outerGlow;
            so.FindProperty("_core").objectReferenceValue = core;
            so.FindProperty("_innerGlow").objectReferenceValue = innerGlow;
            so.FindProperty("_shell").objectReferenceValue = shell;
            so.FindProperty("_break").objectReferenceValue = shellBreak;
            so.FindProperty("_keyRoot").objectReferenceValue = keyRoot;
            so.FindProperty("_keyGlow").objectReferenceValue = keyGlow;
            so.FindProperty("_key").objectReferenceValue = key;
            so.FindProperty("_bombBase").objectReferenceValue = bombBase;
            so.FindProperty("_bombRing").objectReferenceValue = bombRing;
            so.FindProperty("_bombFx").objectReferenceValue = bombFx;
            so.FindProperty("_defuse").objectReferenceValue = defuse;
            so.FindProperty("_bombCount").objectReferenceValue = count;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // Hidden until used; step orders it among the overlays.
        private static SpriteRenderer AddOverlay(Transform parent, string name, Sprite sprite, int step, Material material)
        {
            var renderer = AddSprite(parent, name, sprite, TubeArt.BallOverlayOrder, material);
            renderer.transform.localPosition = new Vector3(0f, 0f, -step * TubeArt.BallOverlayDepth);
            renderer.enabled = false;
            return renderer;
        }

        // Centered on the bomb's screen.
        private static TextMeshPro AddCount(Transform parent, int step)
        {
            var go = new GameObject("BombCount");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 1f / TubeArt.PixelsPerUnit, -step * TubeArt.BallOverlayDepth);
            var text = go.AddComponent<TextMeshPro>();
            text.text = "0";
            text.fontSize = CountFontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(1f, 0.5f);
            text.fontSharedMaterial = CountMaterial(text.font);
            text.sortingOrder = TubeArt.BallOverlayOrder;
            text.GetComponent<MeshRenderer>().enabled = false;
            return text;
        }

        private static Material CountMaterial(TMP_FontAsset font)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(CountMaterialPath);
            if (material == null)
            {
                material = new Material(font.material) { name = "BombCount" };
                AssetDatabase.CreateAsset(material, CountMaterialPath);
            }
            material.CopyPropertiesFromMaterial(font.material);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, CountOutline);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.15f);
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Ice over the tube art with the needed color's orb (all its layers) on the badge, and the melt art over both.
        private static GameObject CreateFrozenTube(ObstacleSprites art, Sprite back, Sprite front, OrbSkin sample)
        {
            var root = new GameObject("FrozenTube");
            var item = root.AddComponent<FrozenTubeItem>();
            var ice = AddSprite(root.transform, "Ice", art.Frozen, TubeArt.FrozenOrder, null);
            var badge = new GameObject("Badge").transform;
            badge.SetParent(root.transform, false);
            var normal = EnsureOrbMaterial(OrbLayerPath, BlendMode.OneMinusSrcAlpha);
            var additive = EnsureOrbMaterial(OrbAdditivePath, BlendMode.One);
            var order = TubeArt.FrozenBadgeOrder;
            var outerGlow = AddSprite(badge, "OuterGlow", sample.OuterGlow, order, normal);
            var backRenderer = AddSprite(badge, "Back", back, order + 1, normal);
            var core = AddSprite(badge, "Core", sample.Core, order + 2, normal);
            var frontRenderer = AddSprite(badge, "Front", front, order + 3, additive);
            var innerGlow = AddSprite(badge, "InnerGlow", sample.InnerGlow, order + 4, normal);
            var melt = AddSprite(root.transform, "Melt", art.Melt, TubeArt.MeltOrder, null);
            melt.enabled = false;

            var so = new SerializedObject(item);
            so.FindProperty("_frozen").objectReferenceValue = ice;
            so.FindProperty("_melt").objectReferenceValue = melt;
            so.FindProperty("_badge").objectReferenceValue = badge;
            so.FindProperty("_badgeOuterGlow").objectReferenceValue = outerGlow;
            so.FindProperty("_badgeCore").objectReferenceValue = core;
            so.FindProperty("_badgeInnerGlow").objectReferenceValue = innerGlow;
            SetArray(so.FindProperty("_badgeLayers"),
                new List<SpriteRenderer> { outerGlow, backRenderer, core, frontRenderer, innerGlow });
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // Curtain, frame and housing sliced to any width, then the padlock; the curtain shows only below the housing line.
        private static GameObject CreateShutter(ObstacleSprites art)
        {
            var root = new GameObject("Shutter");
            root.AddComponent<SortingGroup>().sortingOrder = TubeArt.ShutterGroupOrder;
            var item = root.AddComponent<ShutterItem>();
            var curtain = AddSliced(root.transform, "Curtain", art.Curtain, 0);
            curtain.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            var frame = AddSliced(root.transform, "Frame", art.Frame, 1);
            var housing = AddSliced(root.transform, "Housing", art.Housing, 2);
            var padlock = new GameObject("Padlock").transform;
            padlock.SetParent(root.transform, false);
            var lockOffset = new Vector3(0f, -ShacklePx / TubeArt.PixelsPerUnit, 0f);
            var padlockBase = AddSprite(padlock, "Base", art.PadlockBase, 3, null);
            padlockBase.transform.localPosition = lockOffset;
            var padlockBody = AddSprite(padlock, "Body", art.PadlockBody, 4, null);
            padlockBody.transform.localPosition = lockOffset;

            var mask = new GameObject("Mask").AddComponent<SpriteMask>();
            mask.transform.SetParent(root.transform, false);
            mask.isCustomRangeActive = true;
            mask.backSortingOrder = -1;
            mask.frontSortingOrder = 0;

            var so = new SerializedObject(item);
            so.FindProperty("_curtain").objectReferenceValue = curtain;
            so.FindProperty("_frame").objectReferenceValue = frame;
            so.FindProperty("_housing").objectReferenceValue = housing;
            so.FindProperty("_padlock").objectReferenceValue = padlock;
            so.FindProperty("_padlockBase").objectReferenceValue = padlockBase;
            so.FindProperty("_padlockBody").objectReferenceValue = padlockBody;
            so.FindProperty("_mask").objectReferenceValue = mask;
            ShutterFx(root.transform, so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // The open beat, in the shutter's own sorting group: ring halves round the lock, seam, spill, rim glow and sparks.
        private static void ShutterFx(Transform root, SerializedObject so)
        {
            var lineNear = CompleteFxBuilder.ImportSprite("TouchLineNear", 128f);
            var lineFar = CompleteFxBuilder.ImportSprite("TouchLineFar", 128f);
            var glowNear = CompleteFxBuilder.ImportSprite("TouchGlowNear", 128f);
            var glowFar = CompleteFxBuilder.ImportSprite("TouchGlowFar", 128f);
            var halo = CompleteFxBuilder.ImportSprite("Halo", 128f);
            var dot = CompleteFxBuilder.ImportSprite("Dot", 64f);
            var additive = CompleteFxBuilder.AdditiveMaterial("FxShutterSprite", null);
            var fx = new GameObject("OpenFx").transform;
            fx.SetParent(root, false);
            so.FindProperty("_spill").objectReferenceValue = CompleteFxBuilder.AddSprite(fx, "Spill", halo, 1, additive);
            so.FindProperty("_seam").objectReferenceValue = CompleteFxBuilder.AddSprite(fx, "Seam", dot, 2, additive);
            so.FindProperty("_glowFar").objectReferenceValue = CompleteFxBuilder.AddSprite(fx, "GlowFar", glowFar, 2, additive);
            so.FindProperty("_ringFar").objectReferenceValue = CompleteFxBuilder.AddSprite(fx, "RingFar", lineFar, 2, additive);
            so.FindProperty("_rimGlow").objectReferenceValue = CompleteFxBuilder.AddSprite(fx, "RimGlow", halo, 3, additive);
            so.FindProperty("_glowNear").objectReferenceValue = CompleteFxBuilder.AddSprite(fx, "GlowNear", glowNear, 5, additive);
            so.FindProperty("_ringNear").objectReferenceValue = CompleteFxBuilder.AddSprite(fx, "RingNear", lineNear, 6, additive);
            var sparks = CompleteFxBuilder.Particles(fx, "Sparks", 16, CompleteFxBuilder.ParticleMaterial("FxShutterSpark", dot.texture), 7);
            var main = sparks.main;
            main.gravityModifier = 0.6f;
            var color = sparks.colorOverLifetime;
            color.enabled = true;
            color.color = CompleteFxBuilder.Fade((0f, 1f), (0.6f, 0.8f), (1f, 0f));
            var size = sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));
            so.FindProperty("_sparks").objectReferenceValue = sparks;
        }

        private static SpriteRenderer AddSliced(Transform parent, string name, Sprite sprite, int order)
        {
            var renderer = AddSprite(parent, name, sprite, order, null);
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = sprite.bounds.size;
            return renderer;
        }

        // Drawn over everything on the board.
        private static GameObject CreateBombBlast(ObstacleSprites art)
        {
            var root = new GameObject("BombBlast");
            root.AddComponent<SortingGroup>().sortingOrder = TubeArt.BlastGroupOrder;
            var item = root.AddComponent<BombBlastFx>();
            var blast = AddSprite(root.transform, "Blast", art.Explode, 0, null);
            blast.enabled = false;
            var so = new SerializedObject(item);
            so.FindProperty("_blast").objectReferenceValue = blast;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private sealed class ObstacleSprites
        {
            public Sprite Shell, Break, Key, KeyGlow, BombBase, BombRing, BombFx, Defuse, Explode;
            public Sprite Frozen, Melt, Curtain, Frame, Housing, PadlockBase, PadlockBody;
        }

        private static ObstacleSprites ImportObstacleArt()
        {
            var art = new ObstacleSprites
            {
                Shell = ImportObstacleSprite("Orb_Hidden_Shell"),
                Break = ImportObstacleSprite("Orb_Hidden_Break"),
                Key = ImportObstacleSprite("Orb_Key"),
                KeyGlow = ImportObstacleSprite("Orb_Key_Glow"),
                BombBase = ImportObstacleSprite("Orb_Bomb_Base"),
                BombRing = ImportObstacleSprite("Orb_Bomb_ColorRing"),
                BombFx = ImportObstacleSprite("Orb_Bomb_ActiveFX"),
                Defuse = ImportObstacleSprite("Orb_Bomb_Diffuse"),
                Explode = ImportObstacleSprite("Orb_Bomb_Explode"),
                Frozen = ImportObstacleSprite("Tube_Frozen"),
                Melt = ImportObstacleSprite("Tube_Melt"),
                Curtain = ImportObstacleSprite("Shutter_Curtain", ShutterBorder),
                Frame = ImportObstacleSprite("Shutter_Frame", ShutterBorder),
                Housing = ImportObstacleSprite("Shutter_Housing", HousingBorder),
                PadlockBase = ImportObstacleSprite("Padlock_Base"),
                PadlockBody = ImportObstacleSprite("Padlock_ColorBody")
            };
            foreach (var field in typeof(ObstacleSprites).GetFields())
                if (field.GetValue(art) == null) return null;
            return art;
        }

        // Tube PPU: orb art drawn for a 200 px orb then matches TubeArt.BallDiameter; compressed, outside the atlas.
        private static Sprite ImportObstacleSprite(string name, Vector4 border = default)
        {
            var path = $"{ObstacleFolder}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogError($"[BoardPrefabBuilder] Missing {path}.");
                return null;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TubeArt.PixelsPerUnit;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteBorder = border;
            // Sliced sprites need a full-rect mesh.
            settings.spriteMeshType = border != Vector4.zero ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
                // The enum's value, not its index: the values have gaps.
                element.FindPropertyRelative("Type").intValue = (int)entries[i].type;
                element.FindPropertyRelative("Prefab").objectReferenceValue = entries[i].prefab;
                element.FindPropertyRelative("WarmCount").intValue = entries[i].warm;
                element.FindPropertyRelative("MaxCount").intValue = entries[i].max;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
        }
    }
}
