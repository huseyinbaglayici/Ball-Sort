using System.Collections.Generic;
using Project.Common.UI;
using Project.Common.UI.PopUps;
using Project.Common.Visual;
using Project.Contexts.Application.Economy;
using Project.Contexts.Game;
using Project.Contexts.Home;
using Project.Contexts.Main;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Editor
{
    // Rebuilds the art-team UI on a 1170x2532 canvas: Home, HUD, popups and the space background.
    public static partial class GameUiBuilder
    {
        private const string MainScenePath = "Assets/Scenes/Main.unity";
        private const string ResultPopupPath = "Assets/Prefabs/Popups/ResultPopup.prefab";
        private const string MessagePopupPath = "Assets/Prefabs/Popups/MessagePopup.prefab";
        private const string SettingsPopupPath = "Assets/Prefabs/Popups/SettingsPopup.prefab";
        private const string UiArtRoot = "Assets/Art/Sprites/UI";
        private const string FontPath = "Assets/Art/Fonts/PeaceSans.ttf";
        private const string FontAssetPath = "Assets/Art/Fonts/PeaceSans SDF.asset";
        // Printable ASCII plus Turkish letters and the few symbols the texts use.
        private const string FontExtraCharacters = "ğüşıöçĞÜŞİÖÇ·×…–—‘’“”•";
        // Made by ArtSource/Background/split_stars.py: the nebula without stars, the cut-out stars and their spots.
        private const string BackgroundPath = "Assets/Art/Sprites/Background/BG_Space_Nebula.png";
        private const string StarSheetPath = "Assets/Art/Sprites/Background/BG_Space_StarSheet.png";
        private const string StarLayoutPath = "Assets/Art/Sprites/Background/BG_Space_Stars.json";
        private static readonly Vector2 Reference = new(1170f, 2532f);

        // 9-slice borders (left, bottom, right, top) from the art notes.
        private static readonly Dictionary<string, Vector4> Borders = new()
        {
            ["UI_Pill_Well_60"] = new Vector4(32, 0, 32, 0),
            ["UI_Pill_Well_96"] = new Vector4(50, 0, 50, 0),
            ["UI_Button_Play"] = new Vector4(145, 0, 145, 0),
            ["UI_InLevel_LevelPill"] = new Vector4(62, 0, 62, 0),
            ["UI_Badge_Hard"] = new Vector4(33, 0, 33, 0),
            ["UI_Badge_SuperHard"] = new Vector4(33, 0, 33, 0),
            ["UI_InLevel_Tray"] = new Vector4(70, 70, 70, 70),
            ["UI_Popup_Panel"] = new Vector4(100, 112, 100, 100),
            ["UI_Button_Wide_Go"] = new Vector4(70, 0, 70, 0),
            ["UI_Button_Wide_Purple"] = new Vector4(70, 0, 70, 0),
            ["UI_Button_Wide_Off"] = new Vector4(70, 0, 70, 0),
            ["UI_Toggle_Track_Off"] = new Vector4(32, 0, 32, 0),
            ["UI_Toggle_Track_On"] = new Vector4(32, 0, 32, 0)
        };

        private static readonly Color White = Color.white;
        private static readonly Color Ink = new(0.11f, 0.09f, 0.25f);
        // #28105A, the art's text outline.
        private static readonly Color Outline = new(0.157f, 0.063f, 0.353f);
        private static readonly Color Dim = new(0.04f, 0.03f, 0.10f, 0.72f);
        private static readonly Color Armed = new(0.45f, 1f, 0.85f, 0.7f);

        private static Dictionary<string, Sprite> _sprites;
        private static TMP_FontAsset _font;

        [MenuItem("Tools/Project/Build Game UI")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[GameUI] Leave Play mode first.");
                return;
            }

            ImportArt();
            _font = BuildFont();

            var message = BuildMessagePopup();
            var result = BuildResultPopup();
            var settings = BuildSettingsPopup();
            var daily = BuildDailyRewardPopup();
            var orbitRush = BuildOrbitRushScreen();
            var intro = BuildIntroPopup();

            var scene = SceneManager.GetSceneByPath(MainScenePath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var canvas in new[] { "Home Canvas", "HUD Canvas", "UI Canvas" })
                {
                    var scaler = Root(scene, canvas).GetComponent<CanvasScaler>();
                    // The HUD keeps its height share at every aspect; other canvases keep the width down to 9:16.
                    scaler.referenceResolution = canvas == "HUD Canvas"
                        ? Reference
                        : new Vector2(Reference.x, Reference.x * 16f / 9f);
                    // Expand fits the whole 1170x2532 design box.
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                    scaler.referencePixelsPerUnit = 100f;
                }
                // Popups live under the UI canvas safe area; give it the same edge minimums as the screens.
                var popupArea = Root(scene, "UI Canvas").GetComponentInChildren<SafeArea>(true);
                if (popupArea != null) SetMinimumInsets(popupArea);
                BuildBackground(scene);
                BuildHome(scene, message, settings);
                BuildLiveOpsHome(scene, daily, orbitRush, message);
                BuildHud(scene, message, result);
                BuildTutorial(scene, intro);
                BuildLevelOpen(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }

            BuildSplash();
            AssetDatabase.SaveAssets();
            Debug.Log("[GameUI] Home, HUD, popups, background and splash built.");
        }

        // ---------------------------------------------------------------- Font

        // Static SDF atlas of the UI face with a dark outline; anything missing falls back to the TMP default.
        private static TMP_FontAsset BuildFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                existing.material.SetColor(ShaderUtilities.ID_OutlineColor, Outline);
                EditorUtility.SetDirty(existing.material);
                return existing;
            }

            var source = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (source == null) throw new System.InvalidOperationException($"[GameUI] Font missing at {FontPath}.");
            var asset = TMP_FontAsset.CreateFontAsset(source, 72, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, false);
            asset.name = "PeaceSans SDF";
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            var atlas = asset.atlasTextures[0];
            atlas.name = "PeaceSans SDF Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
            var material = asset.material;
            material.name = "PeaceSans SDF Material";
            AssetDatabase.AddObjectToAsset(material, asset);

            var characters = new System.Text.StringBuilder();
            for (var c = 32; c < 127; c++) characters.Append((char)c);
            characters.Append(FontExtraCharacters);
            asset.TryAddCharacters(characters.ToString(), out var missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"[GameUI] Not in the UI font (fallback draws them): {missing}");
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            asset.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };

            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Outline);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // ---------------------------------------------------------------- Art

        private static void ImportArt()
        {
            var paths = new List<string> { BackgroundPath, StarSheetPath };
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { UiArtRoot }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));

            foreach (var path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                var border = Borders.TryGetValue(name, out var b) ? b : Vector4.zero;
                // Full-screen art is compressed; small UI stays exact.
                var compression = path.Contains("/Posters/") || path.Contains("/Splash/")
                    ? TextureImporterCompression.CompressedHQ
                    : TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                var changed = importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != border ||
                              importer.mipmapEnabled || importer.maxTextureSize < 4096 ||
                              importer.textureCompression != compression ||
                              settings.spriteMeshType != SpriteMeshType.FullRect;
                if (!changed) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.spritePivot = new Vector2(0.5f, 0.5f);
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 4096;
                importer.textureCompression = compression;
                importer.sRGBTexture = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            _sprites = new Dictionary<string, Sprite>();
            foreach (var path in paths)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) _sprites[System.IO.Path.GetFileNameWithoutExtension(path)] = sprite;
            }
        }

        private static Sprite S(string name)
        {
            if (_sprites.TryGetValue(name, out var sprite)) return sprite;
            throw new System.InvalidOperationException($"[GameUI] Sprite '{name}' is missing under {UiArtRoot}.");
        }

        // ---------------------------------------------------------------- Background

        private static void BuildBackground(Scene scene)
        {
            var camera = Root(scene, "Main Camera").GetComponent<Camera>();
            var old = camera.transform.Find("SpaceBackground");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject("SpaceBackground", typeof(SpriteRenderer));
            go.transform.SetParent(camera.transform, false);
            // Inside the far plane, behind everything by sorting order.
            go.transform.localPosition = new Vector3(0f, 0f, Mathf.Min(50f, camera.farClipPlane * 0.5f));
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            renderer.sortingOrder = -1000;
            var background = go.AddComponent<SpaceBackground>();
            Set(background, "_camera", camera);
            Set(background, "_starSheet", AssetDatabase.LoadAssetAtPath<Texture2D>(StarSheetPath));
            Set(background, "_starLayout", AssetDatabase.LoadAssetAtPath<TextAsset>(StarLayoutPath));
        }

        // ---------------------------------------------------------------- Home

        private static void BuildHome(Scene scene, MessagePopup message, SettingsPopup settingsPopup)
        {
            var canvas = Root(scene, "Home Canvas").transform;
            Clear(canvas);
            // Poster, shade and UI share one full-screen root so the level-open push scales them together.
            var content = Span("Content", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var poster = BuildPoster(content);
            var shade = Span("Shade", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Shade(shade, "TopShade", 520f, 0.6f, true);
            Shade(shade, "BottomShade", 630f, 0.65f, false);
            var (area, topStage, bottomStage) = EdgeStages(content);

            // Top bar: lives and coins in wells, settings on its base.
            var top = Group("Top", topStage, new Vector2(0, 360), 0.4f);
            Pic("LivesBar", top, Center, At(195, 228), new Vector2(250, 96), S("UI_Pill_Well_96")).raycastTarget = false;
            var heart = Pic("Heart", top, Center, At(86, 226), new Vector2(116, 116), S("UI_Icon_Heart"));
            heart.raycastTarget = false;
            var livesCount = Text("Count", top, At(200, 226), new Vector2(120, 70), "5", 56, White);
            Pic("CoinBar", top, Center, At(806, 228), new Vector2(292, 96), S("UI_Pill_Well_96")).raycastTarget = false;
            Pic("Coin", top, Center, At(676, 226), new Vector2(118, 118), S("UI_Icon_Coin")).raycastTarget = false;
            var coins = Text("Amount", top, At(830, 226), new Vector2(180, 70), "0", 54, White);
            var settings = ButtonRect("SettingsButton", top, Center, At(1050, 229), new Vector2(120, 126), S("UI_InLevel_IconBase"));
            Pic("Icon", settings, Center, new Vector2(0, 3), new Vector2(80, 80), S("UI_Icon_Settings")).raycastTarget = false;

            // Side buttons: daily and no-ads on the left, the timed event on the right.
            var side = Group("Side", topStage, new Vector2(0, 0), 0.4f);
            var daily = SideButton(side, "DailyButton", At(90, 880), S("UI_Icon_Daily"), "daily.button");
            NoticeBadge(daily.transform, new Vector2(52, 54));
            var noAds = SideButton(side, "NoAdsButton", At(90, 1090), S("UI_Icon_NoAds"), "home.no_ads");
            var eventButton = SideButton(side, "EventButton", At(1080, 880), S("UI_Icon_Challenge"), "rush.button");
            var timerPill = Pic("TimerPill", eventButton, Center, new Vector2(0, 86), new Vector2(150, 60), S("UI_Pill_Well_60"));
            timerPill.raycastTarget = false;
            Text("Timer", timerPill, new Vector2(0, 2), new Vector2(130, 44), "2d 14h", 32, White);
            // Below the timer pill so the time stays readable.
            NoticeBadge(eventButton.transform, new Vector2(58, 18));

            // Play with the level, its bottom edge at y 2160.
            var bottom = Group("Bottom", bottomStage, new Vector2(0, -520), 0.4f);
            var play = ButtonRect("PlayButton", bottom, Center, At(585, 2015), new Vector2(680, 290), S("UI_Button_Play"));
            var playLabel = Text("Label", play, new Vector2(0, 13), new Vector2(560, 110), "Level 1", 78, White);

            // Home bar on the screen bottom: Shop, Event, Home (selected), Orbit Rush, Puzzle; 2312 is its top edge.
            var bar = Rect("HomeBar", bottom, Center, At(585, 2422), new Vector2(Reference.x, 220));
            // A flat strip, wide enough for any aspect.
            var strip = Pic("Strip", bar, Center, Vector2.zero, new Vector2(Reference.x * 3f, 220), S("UI_HomeBar_BG"));
            strip.preserveAspect = false;
            strip.raycastTarget = false;
            Pic("Divider", bar, Center, new Vector2(210 - 585, 0), new Vector2(8, 150), S("UI_HomeBar_Divider")).raycastTarget = false;
            Pic("Divider", bar, Center, new Vector2(960 - 585, 0), new Vector2(8, 150), S("UI_HomeBar_Divider")).raycastTarget = false;
            var shop = Tab(bar, "ShopTab", 105, S("UI_Icon_Shop"));
            Tab(bar, "EventTab", 315, S("UI_Icon_Challenge"));
            // Selected slot: its bottom edge 40 below the screen, the bigger icon rising above the dome.
            var homeTab = Pic("HomeTab", bar, Center, Vector2.zero, new Vector2(330, 300), S("UI_HomeBar_TabSelected"));
            homeTab.raycastTarget = false;
            Pic("Icon", homeTab, Center, new Vector2(0, 53), new Vector2(200, 200), S("UI_Icon_Home")).raycastTarget = false;
            Text("Label", homeTab, new Vector2(0, -68), new Vector2(300, 56), "Home", 44, White, "nav.home");
            Tab(bar, "OrbitRushTab", 855, S("UI_Icon_OrbitRush"));
            var puzzle = Tab(bar, "PuzzleTab", 1065, S("UI_Icon_DailyPuzzle"));

            var hud = area.gameObject.AddComponent<EconomyHud>();
            Set(hud, "_coins", coins);
            Set(hud, "_livesCount", livesCount);
            Set(hud, "_heart", heart);
            Set(hud, "_heartSprite", S("UI_Icon_Heart"));
            Set(hud, "_unlimitedSprite", S("UI_Icon_HeartInfinite"));

            var screen = Root(scene, "HomeScreen").GetComponent<HomeScreen>();
            Set(screen, "_view", canvas.gameObject);
            Set(screen, "_levelText", playLabel);
            Set(screen, "_poster", poster);
            Set(screen, "_playButton", play.GetComponent<Button>());
            Set(screen, "_starterPackButton", shop);
            Set(screen, "_settingsButton", settings.GetComponent<Button>());
            // Daily, both event buttons and the Orbit Rush tab are wired by the live-ops builder.
            SetArray(screen, "_comingSoon", new Object[] { noAds, puzzle });
            Set(screen, "_settingsPopup", settingsPopup);
            Set(screen, "_messagePopup", message);
            // The level opens out of the orb in the poster Luno's hand; bars and buttons fade, the poster stays.
            Set(screen, "_orb", poster.transform.Find("Art/HandOrb"));
            Set(screen, "_content", content);
            SetArray(screen, "_chrome", new Object[]
            {
                top.gameObject.AddComponent<CanvasGroup>(), side.gameObject.AddComponent<CanvasGroup>(),
                bottom.gameObject.AddComponent<CanvasGroup>()
            });
        }

        private const string ChapterArtPath = "Assets/Configs/ChapterArt.asset";
        private const string PosterFolder = UiArtRoot + "/Home/Posters";

        // Full-bleed chapter poster: two layers for the crossfade and the hand orb, placed per chapter at runtime.
        private static HomePoster BuildPoster(RectTransform content)
        {
            var root = Span("Poster", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var art = Rect("Art", root, Center, Vector2.zero, Reference);
            var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            var chapters = ChapterArtAsset();
            var first = chapters.Count > 0 ? chapters[0].Poster : null;
            if (first != null) fitter.aspectRatio = first.rect.width / first.rect.height;
            var back = Layer(art, "Back", first);
            var front = Layer(art, "Front", first);
            var orb = Span("HandOrb", art, Center, Center, Vector2.zero, Vector2.zero);

            var poster = root.gameObject.AddComponent<HomePoster>();
            Set(poster, "_art", chapters);
            Set(poster, "_fitter", fitter);
            Set(poster, "_back", back);
            Set(poster, "_front", front);
            Set(poster, "_orb", orb);
            return poster;
        }

        private static Image Layer(RectTransform art, string name, Sprite sprite)
        {
            var image = Span(name, art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        // Made once with the default chapters; later edits live in the asset.
        private static ChapterArt ChapterArtAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ChapterArt>(ChapterArtPath);
            if (asset != null) return asset;
            // Poster file, hand-orb centre (share of the poster from its top-left) and orb width (share of the poster width).
            var defaults = new (string File, Vector2 Orb, float Size)[]
            {
                ("Poster_1_StartTheAdventure", new Vector2(0.824f, 0.351f), 0.063f),
                ("Poster_2_Ice", new Vector2(0.707f, 0.235f), 0.081f),
                ("Poster_3_Jungle", new Vector2(0.864f, 0.208f), 0.135f),
                ("Poster_4_Desert", new Vector2(0.761f, 0.592f), 0.038f),
                ("Poster_5_Ocean", new Vector2(0.704f, 0.488f), 0.063f)
            };
            asset = ScriptableObject.CreateInstance<ChapterArt>();
            AssetDatabase.CreateAsset(asset, ChapterArtPath);
            var so = new SerializedObject(asset);
            var list = so.FindProperty("_chapters");
            list.arraySize = defaults.Length;
            for (var i = 0; i < defaults.Length; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Poster").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>($"{PosterFolder}/{defaults[i].File}.png");
                item.FindPropertyRelative("Orb").vector2Value = defaults[i].Orb;
                item.FindPropertyRelative("OrbSize").floatValue = defaults[i].Size;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return asset;
        }

        // A home-bar tab centred in its slot at x, 118 below the bar top.
        private static Button Tab(RectTransform bar, string name, float x, Sprite icon)
        {
            var rect = Rect(name, bar, Center, new Vector2(x - 585, -8), new Vector2(200, 200));
            // Invisible hit area over the slot.
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            rect.gameObject.AddComponent<ButtonFeedback>();
            Pic("Icon", rect, Center, Vector2.zero, new Vector2(150, 150), icon).raycastTarget = false;
            NoticeBadge(rect, new Vector2(55, 60));
            return button;
        }

        // Hidden red badge with a 40 px count at the icon's top-right.
        private static void NoticeBadge(Transform parent, Vector2 at)
        {
            var badge = Pic("Badge", parent, Center, at, new Vector2(72, 72), S("UI_Badge_Notification"));
            badge.raycastTarget = false;
            Text("Count", badge, new Vector2(0, 2), new Vector2(60, 50), "1", 40, White);
            badge.gameObject.SetActive(false);
        }

        // #0E0828 fading out from the screen edge so the bars read over the poster.
        private static void Shade(RectTransform parent, string name, float height, float alpha, bool top)
        {
            var edge = top ? 1f : 0f;
            var rect = Span(name, parent, new Vector2(0, edge), new Vector2(1, edge), new Vector2(0, top ? -height * 0.5f : height * 0.5f),
                new Vector2(0, height));
            // The sprite is darkest at its top; the bottom shade flips it.
            if (!top) rect.localScale = new Vector3(1, -1, 1);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = S("UI_Home_Shade");
            image.color = new Color(0.055f, 0.031f, 0.157f, alpha);
            image.raycastTarget = false;
        }

        // ---------------------------------------------------------------- Splash

        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        // Deep violet of the opening art's lower edge; fills any bars around it.
        private static readonly Color SplashFill = new(0.133f, 0.098f, 0.494f);

        // The Boot splash: the opening art full-bleed, the logo near the top, the load bar near the bottom.
        private static void BuildSplash()
        {
            var scene = SceneManager.GetSceneByPath(BootScenePath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Additive);
            try
            {
                var background = Root(scene, "Splash").transform.Find("Background");
                foreach (var old in new[] { "Art", "Logo" })
                {
                    var child = background.Find(old);
                    if (child != null) Object.DestroyImmediate(child.gameObject);
                }
                background.GetComponent<Image>().color = SplashFill;

                var opening = AssetDatabase.LoadAssetAtPath<Sprite>(UiArtRoot + "/Splash/Splash_OpeningScene.png");
                var art = Rect("Art", background, Center, Vector2.zero, new Vector2(1080, 1920));
                art.SetAsFirstSibling();
                var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = opening.rect.width / opening.rect.height;
                var image = art.gameObject.AddComponent<Image>();
                image.sprite = opening;

                var logo = AssetDatabase.LoadAssetAtPath<Sprite>(UiArtRoot + "/Splash/Splash_Logo.png");
                var logoRect = Rect("Logo", background, Top, new Vector2(0, -150), new Vector2(760, 760 * logo.rect.height / logo.rect.width),
                    new Vector2(0.5f, 1f));
                var logoImage = logoRect.gameObject.AddComponent<Image>();
                logoImage.sprite = logo;
                logoImage.raycastTarget = false;

                // A "Loading..." line replaces the progress bar.
                foreach (var old in new[] { "ProgressBackground", "Loading" })
                {
                    var child = background.Find(old);
                    if (child != null) Object.DestroyImmediate(child.gameObject);
                }
                var loading = Text("Loading", background, Vector2.zero, new Vector2(700, 90), "Loading...", 60, White);
                loading.horizontalAlignment = HorizontalAlignmentOptions.Center;
                Anchor(loading.rectTransform, Bottom, new Vector2(0, 170));
                Set(Root(scene, "Splash").GetComponent<Project.Contexts.Application.Splash>(), "_loading", loading);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ---------------------------------------------------------------- Level open

        private const string LevelOpenShaderPath = "Assets/Art/Shaders/LevelOpenMask.shader";
        private const string LevelCloseShaderPath = "Assets/Art/Shaders/LevelCloseMask.shader";
        private const string LevelOpenSettingsPath = "Assets/Configs/LevelOpenSettings.asset";

        // One full-screen image over every canvas; its material cuts the feathered hole at runtime.
        private static void BuildLevelOpen(Scene scene)
        {
            var canvas = Root(scene, "UI Canvas").transform;
            var old = canvas.Find("LevelOpen");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = Span("LevelOpen", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.SetAsLastSibling();
            root.gameObject.AddComponent<CanvasGroup>();
            // Catches every tap while the view runs.
            root.gameObject.AddComponent<Image>();

            var settings = AssetDatabase.LoadAssetAtPath<LevelOpenSettings>(LevelOpenSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LevelOpenSettings>();
                AssetDatabase.CreateAsset(settings, LevelOpenSettingsPath);
            }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(LevelOpenShaderPath);
            if (shader == null) throw new System.InvalidOperationException($"[GameUI] Shader missing at {LevelOpenShaderPath}.");
            var closeShader = AssetDatabase.LoadAssetAtPath<Shader>(LevelCloseShaderPath);
            if (closeShader == null) throw new System.InvalidOperationException($"[GameUI] Shader missing at {LevelCloseShaderPath}.");

            var view = root.gameObject.AddComponent<LevelOpenView>();
            Set(view, "_settings", settings);
            Set(view, "_shader", shader);
            Set(view, "_closeShader", closeShader);
            root.gameObject.SetActive(false);

            Set(Root(scene, "MainFlow").GetComponent<MainFlow>(), "_levelOpen", view);
        }

        // ---------------------------------------------------------------- Gameplay HUD

        private static void BuildHud(Scene scene, MessagePopup message, ResultPopup result)
        {
            var canvas = Root(scene, "HUD Canvas").transform;
            Clear(canvas);
            var (area, topStage, bottomStage) = EdgeStages(canvas);

            // Booster panel at the design spot for boards of three rows; buttons rise one after another.
            var tray = Pic("BoosterBar", bottomStage, Center, At(585, 2333), new Vector2(743, 278), S("UI_InLevel_Tray"));
            Entrance(tray.rectTransform, new Vector2(0, -300), 0.3f, 0f, PrimeTween.Ease.OutCubic);
            var bar = tray.gameObject.AddComponent<BoosterBar>();
            var boosters = new[]
            {
                (BoosterType.Undo, "UI_Icon_Undo"),
                (BoosterType.FreeMove, "UI_Icon_FreeMove"),
                (BoosterType.ExtraTube, "UI_Icon_ExtraTube")
            };
            var so = new SerializedObject(bar);
            var slots = so.FindProperty("_slots");
            slots.arraySize = boosters.Length;
            for (var i = 0; i < boosters.Length; i++)
            {
                var (type, icon) = boosters[i];
                var x = (i - 1) * 235f;
                var armed = Pic(type + "Armed", tray, Center, new Vector2(x, -10), new Vector2(232, 230), S("UI_InLevel_BoosterBase_Up"));
                armed.color = Armed;
                armed.gameObject.SetActive(false);

                var button = ButtonRect(type + "Button", tray, Center, new Vector2(x, -10), new Vector2(207, 205), S("UI_InLevel_BoosterBase_Up"));
                var buttonComponent = button.GetComponent<Button>();
                buttonComponent.transition = Selectable.Transition.SpriteSwap;
                buttonComponent.spriteState = new SpriteState { pressedSprite = S("UI_InLevel_BoosterBase_Down") };
                Entrance(button, new Vector2(0, -260), 0.2f, 0.1f * i, PrimeTween.Ease.Linear);
                var iconImage = Pic("Icon", button, Center, new Vector2(0, 7), new Vector2(134, 134), S(icon));
                iconImage.raycastTarget = false;

                var lockText = Text("Lock", button, new Vector2(0, -70), new Vector2(170, 44), "Lv 1", 32, White);

                // Count badge at the bottom-right; the coin price box takes the bottom centre when stock is out.
                var count = Pic("CountBadge", button, Center, new Vector2(63, -69), new Vector2(86, 89), S("UI_Badge_Count"));
                count.raycastTarget = false;
                var countText = Text("Count", count, new Vector2(0, 4), new Vector2(66, 56), "0", 42, White);

                var price = Pic("PricePill", button, Center, new Vector2(13, -79), new Vector2(169, 64), S("UI_Pill_Well_60"));
                price.raycastTarget = false;
                Pic("Coin", price, new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(62, 62), S("UI_Icon_Coin")).raycastTarget = false;
                var priceText = Text("Amount", price, new Vector2(22, 2), new Vector2(100, 48), "0", 34, White);

                var slot = slots.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("Type").enumValueIndex = (int)type;
                slot.FindPropertyRelative("Button").objectReferenceValue = buttonComponent;
                slot.FindPropertyRelative("Badge").objectReferenceValue = countText;
                slot.FindPropertyRelative("Lock").objectReferenceValue = lockText.gameObject;
                slot.FindPropertyRelative("LockLevel").objectReferenceValue = lockText;
                slot.FindPropertyRelative("Armed").objectReferenceValue = armed.gameObject;
                slot.FindPropertyRelative("Base").objectReferenceValue = button.GetComponent<Image>();
                slot.FindPropertyRelative("Icon").objectReferenceValue = iconImage;
                slot.FindPropertyRelative("Count").objectReferenceValue = count.gameObject;
                slot.FindPropertyRelative("Price").objectReferenceValue = price.gameObject;
                slot.FindPropertyRelative("PriceText").objectReferenceValue = priceText;
            }
            so.FindProperty("_baseUp").objectReferenceValue = S("UI_InLevel_BoosterBase_Up");
            so.FindProperty("_baseLocked").objectReferenceValue = S("UI_InLevel_BoosterBase_Locked");
            so.ApplyModifiedPropertiesWithoutUndo();

            // Top: coins on the left, the level banner in the middle, settings on the right.
            var top = Group("Top", topStage, new Vector2(0, 300), 0.3f);
            var coinBar = Pic("CoinBar", top, Center, At(201, 262), new Vector2(284, 100), S("UI_Pill_Well_96"));
            var coins = Text("Amount", coinBar, new Vector2(27, -2), new Vector2(162, 66), "0", 46, White);
            var coinIcon = Pic("Coin", top, Center, At(93, 254), new Vector2(93, 93), S("UI_Icon_Coin"));
            var pill = Pic("LevelPill", top, Center, At(585, 446), new Vector2(432, 140), S("UI_InLevel_LevelPill"));
            var levelText = Text("Level", pill, new Vector2(0, 8), new Vector2(360, 80), "Level 1", 56, White);
            var difficulty = DifficultyBadge(top, At(585, 524), new Vector2(300, 66), At(585, 369));

            var hud = area.gameObject.AddComponent<EconomyHud>();
            Set(hud, "_coins", coins);

            // The settings column drops over a dark tint; everything before it in the hierarchy is dimmed.
            var settings = BuildSettingsMenu(top);

            var game = Root(scene, "GameController").GetComponent<GameController>();
            Set(game, "_hud", canvas.gameObject);
            Set(game, "_levelText", levelText);
            Set(game, "_boosters", bar);
            Set(game, "_difficulty", difficulty);
            Set(game, "_settings", settings);
            Set(game, "_messagePopup", message);
            Set(game, "_resultPopup", result);
            Set(game, "_coinHud", hud);
            Set(game, "_coinTarget", coinIcon.rectTransform);
            Set(game, "_coinSprite", S("UI_Icon_Coin"));
        }

        // Four round slots under the settings button, the tint behind them.
        private static SettingsMenu BuildSettingsMenu(RectTransform parent)
        {
            var dim = Span("SettingsDim", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.offsetMin = new Vector2(-1500, -1500);
            dim.offsetMax = new Vector2(1500, 1500);
            var dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0f);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.targetGraphic = dimImage;
            dim.gameObject.SetActive(false);

            var gear = IconButton(parent, "SettingsButton", At(1058, 265), S("UI_Icon_Settings"), 156f, 104f);

            (Button Button, GameObject Off) Slot(string name, float y, string icon)
            {
                var button = IconButton(parent, name, At(1058, y), S(icon), 151f, 96f);
                // The settings column presses a little deeper than other buttons.
                SetFloat(button.GetComponent<ButtonFeedback>(), "_pressScale", 0.95f);
                var off = Pic("Off", button.transform, Center, new Vector2(0, 4), new Vector2(96, 96), S("UI_Icon_OffSlash"));
                off.raycastTarget = false;
                button.gameObject.SetActive(false);
                return (button, off.gameObject);
            }

            var sound = Slot("SoundButton", 441, "UI_Icon_Sound");
            var music = Slot("MusicButton", 621, "UI_Icon_Music");
            var haptics = Slot("VibrationButton", 801, "UI_Icon_Vibration");
            var exit = ButtonRect("ExitButton", parent, Center, At(1058, 981), new Vector2(146, 151), S("UI_Button_Close"));
            exit.gameObject.SetActive(false);

            var menu = gear.gameObject.AddComponent<SettingsMenu>();
            Set(menu, "_gear", gear);
            Set(menu, "_dim", dimButton);
            Set(menu, "_sound", sound.Button);
            Set(menu, "_music", music.Button);
            Set(menu, "_haptics", haptics.Button);
            Set(menu, "_exit", exit.GetComponent<Button>());
            Set(menu, "_soundOff", sound.Off);
            Set(menu, "_musicOff", music.Off);
            Set(menu, "_hapticsOff", haptics.Off);
            return menu;
        }

        // Badge with its label; the comet marks sit at marksAt (one for Hard, two for Super Hard).
        private static DifficultyTag DifficultyBadge(RectTransform parent, Vector2 position, Vector2 size, Vector2? marksAt)
        {
            var root = Rect("Difficulty", parent, Center, Vector2.zero, Vector2.zero);
            var badge = Pic("Badge", root, Center, position, size, S("UI_Badge_SuperHard"));
            var label = Text("Label", badge, new Vector2(0, 2), size - new Vector2(40, 12), "SUPER HARD", size.y * 0.55f, White);
            var marks = new GameObject[marksAt.HasValue ? 2 : 0];
            for (var i = 0; i < marks.Length; i++)
                marks[i] = Pic("Comet" + i, root, Center, marksAt.Value + new Vector2((i - 0.5f) * 56f, 0), new Vector2(52, 52), S("UI_Icon_Comet")).gameObject;
            var tag = root.gameObject.AddComponent<DifficultyTag>();
            Set(tag, "_root", root.gameObject);
            Set(tag, "_label", label);
            SetArray(tag, "_marks", marks);
            Set(tag, "_background", badge);
            SetArray(tag, "_backgrounds", new Object[] { S("UI_Badge_Hard"), S("UI_Badge_SuperHard") });
            return tag;
        }
    }
}
