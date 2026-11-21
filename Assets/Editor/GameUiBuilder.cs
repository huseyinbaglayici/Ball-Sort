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
    public static class GameUiBuilder
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
            ["UI_Pill_Well_60"] = new Vector4(30, 0, 30, 0),
            ["UI_Pill_Well_96"] = new Vector4(48, 0, 48, 0),
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
        private static readonly Color Cream = new(1f, 0.96f, 0.86f);
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
                BuildHud(scene, message, result);
                BuildLevelOpen(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[GameUI] Home, HUD, popups and background built.");
        }

        // ---------------------------------------------------------------- Font

        // Static SDF atlas of the UI face with a dark outline; anything missing falls back to the TMP default.
        private static TMP_FontAsset BuildFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;

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
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.16f, 0.12f, 0.38f));
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
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                var changed = importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != border ||
                              importer.mipmapEnabled || importer.maxTextureSize < 4096 ||
                              importer.textureCompression != TextureImporterCompression.Uncompressed ||
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
                // No compression: gradients and soft edges stay clean.
                importer.textureCompression = TextureImporterCompression.Uncompressed;
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
            var (area, topStage, bottomStage) = EdgeStages(canvas);

            // Top bar: avatar, then coins and lives sharing the rest; settings lives in the tab bar.
            var top = Group("Top", topStage, new Vector2(0, 360), 0.4f);
            Pic("Avatar", top, Center, At(100, 228), new Vector2(150, 150), S("UI_InLevel_IconBase")).raycastTarget = false;
            Pic("Face", top, Center, At(100, 222), new Vector2(124, 124), S("Luno_512")).raycastTarget = false;
            var coinBar = Pic("CoinBar", top, Center, At(458, 229), new Vector2(360, 96), S("UI_Pill_Well_96"));
            var coins = Text("Amount", coinBar, new Vector2(30, 2), new Vector2(240, 70), "0", 48, White);
            Pic("Coin", top, Center, At(270, 229), new Vector2(116, 116), S("UI_Icon_Coin"));
            var livesBar = Pic("LivesBar", top, Center, At(935, 229), new Vector2(360, 96), S("UI_Pill_Well_96"));
            var livesTimer = Text("Timer", livesBar, new Vector2(10, 2), new Vector2(220, 64), "Full", 40, White);
            var heart = Pic("Heart", top, Center, At(748, 223), new Vector2(128, 128), S("UI_Icon_Heart"));
            var livesCount = Text("Count", heart, new Vector2(0, 6), new Vector2(90, 70), "5", 50, White);
            var lifeAd = ButtonRect("RewardedLifeButton", top, Center, At(1110, 229), new Vector2(72, 72), S("UI_Icon_Ad"));

            // Centre piece: the current level on a big orb, the mascot beside it; sits mid-screen at every aspect.
            var centre = Rect("Centre", area, Center, new Vector2(0, 110), new Vector2(Reference.x, 900));
            Entrance(centre, new Vector2(0, -120), 0.4f, 0.05f, PrimeTween.Ease.OutCubic);
            // The level orb and its labels fade together when a level opens; the mascot stays, holding the zoom's orb.
            var levelOrb = Rect("LevelOrb", centre, Center, Vector2.zero, new Vector2(Reference.x, 900));
            levelOrb.gameObject.AddComponent<CanvasGroup>();
            var glow = Pic("Glow", levelOrb, Center, Vector2.zero, new Vector2(760, 760), S("UI_Toggle_Knob"));
            glow.color = new Color(1f, 0.78f, 0.35f, 0.3f);
            glow.raycastTarget = false;
            Pic("Orb", levelOrb, Center, Vector2.zero, new Vector2(560, 560), S("UI_Toggle_Knob")).raycastTarget = false;
            var difficulty = DifficultyBadge(levelOrb, new Vector2(0, 150), new Vector2(300, 66), new Vector2(0, 215));
            var levelText = Text("Level", levelOrb, new Vector2(0, -10), new Vector2(460, 130), "Level 1", 96, Ink);
            var mascot = Pic("Mascot", centre, Center, new Vector2(260, -210), new Vector2(300, 300), S("Luno_512"));
            mascot.raycastTarget = false;
            // The orb in the mascot's hand, measured on the art; the level zooms out of and back into it.
            var handOrb = Rect("HandOrb", mascot, Center, HandOrbAt * mascot.rectTransform.sizeDelta,
                Vector2.one * (HandOrbSize * mascot.rectTransform.sizeDelta.x));

            // Play, with the next road reward chest as its badge.
            var bottom = Group("Bottom", bottomStage, new Vector2(0, -520), 0.4f);
            var play = ButtonRect("PlayButton", bottom, Center, At(585, 2045), new Vector2(640, 170), S("UI_Button_Wide_Go"));
            Text("Label", play, new Vector2(0, 8), new Vector2(500, 100), "Play", 68, White, "home.play");
            var chest = ButtonRect("RoadChest", bottom, Center, At(860, 1955), new Vector2(130, 130), S("UI_Icon_RewardChest"));

            // Tab bar: shop, events, Home (raised, selected), daily puzzle, settings.
            Pic("TabBar", bottom, Center, At(585, 2340), new Vector2(1190, 250), S("UI_InLevel_Tray")).raycastTarget = false;
            var starter = IconButton(bottom, "StarterPackButton", At(117, 2330), S("UI_Icon_Shop"), 170f, 124f);
            var challenge = IconButton(bottom, "ChallengeButton", At(351, 2330), S("UI_Icon_Challenge"), 170f, 124f);
            var homeTab = Pic("HomeTab", bottom, Center, At(585, 2290), new Vector2(240, 238), S("UI_InLevel_BoosterBase_Up"));
            homeTab.raycastTarget = false;
            Pic("Icon", homeTab, Center, new Vector2(0, 22), new Vector2(140, 140), S("UI_Icon_Daily")).raycastTarget = false;
            Text("Label", homeTab, new Vector2(0, -80), new Vector2(200, 44), "Home", 34, White, "nav.home");
            var puzzle = IconButton(bottom, "DailyPuzzleButton", At(819, 2330), S("UI_Icon_DailyPuzzle"), 170f, 124f);
            var settingsTab = IconButton(bottom, "SettingsTab", At(1053, 2330), S("UI_Icon_Settings"), 170f, 116f);

            var hud = area.gameObject.AddComponent<EconomyHud>();
            Set(hud, "_coins", coins);
            Set(hud, "_livesCount", livesCount);
            Set(hud, "_livesTimer", livesTimer);
            Set(hud, "_heart", heart);
            Set(hud, "_heartSprite", S("UI_Icon_Heart"));
            Set(hud, "_unlimitedSprite", S("UI_Icon_HeartInfinite"));

            var screen = Root(scene, "HomeScreen").GetComponent<HomeScreen>();
            Set(screen, "_view", canvas.gameObject);
            Set(screen, "_levelText", levelText);
            Set(screen, "_difficulty", difficulty);
            Set(screen, "_metaText", null);
            Set(screen, "_road", null);
            Set(screen, "_roadButton", chest.GetComponent<Button>());
            Set(screen, "_playButton", play.GetComponent<Button>());
            Set(screen, "_starterPackButton", starter);
            Set(screen, "_challengeButton", challenge);
            Set(screen, "_rewardedLifeButton", lifeAd.GetComponent<Button>());
            Set(screen, "_settingsButton", settingsTab);
            SetArray(screen, "_comingSoon", new Object[] { puzzle });
            Set(screen, "_settingsPopup", settingsPopup);
            Set(screen, "_messagePopup", message);
            // The level-open zoom grows out of the orb while bars and mascot fade.
            Set(screen, "_orb", handOrb);
            SetArray(screen, "_chrome", new Object[]
            {
                top.gameObject.AddComponent<CanvasGroup>(), bottom.gameObject.AddComponent<CanvasGroup>(),
                levelOrb.GetComponent<CanvasGroup>()
            });
        }

        // ---------------------------------------------------------------- Level open

        private const string LevelOpenShaderPath = "Assets/Art/Shaders/LevelOpenMask.shader";
        private const string LevelOpenSettingsPath = "Assets/Configs/LevelOpenSettings.asset";
        // Centre and width of the orb in the mascot's hand, as shares of the Luno_512 art from its middle.
        private static readonly Vector2 HandOrbAt = new(0.371f, 0.133f);
        private const float HandOrbSize = 0.11f;

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

            var view = root.gameObject.AddComponent<LevelOpenView>();
            Set(view, "_settings", settings);
            Set(view, "_shader", shader);
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
            Pic("Coin", top, Center, At(93, 254), new Vector2(93, 93), S("UI_Icon_Coin"));
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
            Set(game, "_movesText", null);
            Set(game, "_homeButton", null);
            Set(game, "_restartButton", null);
            Set(game, "_boosters", bar);
            Set(game, "_difficulty", difficulty);
            Set(game, "_settings", settings);
            Set(game, "_messagePopup", message);
            Set(game, "_resultPopup", result);
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

        // ---------------------------------------------------------------- Popups

        // Shared popup frame of the art mock: 880 wide, panel top 760 on the design screen.
        private const float FrameTop = 760f;

        private static MessagePopup BuildMessagePopup()
        {
            var root = PopupRoot("MessagePopup", new Color(0f, 0f, 0f, 0.894f));
            try
            {
                var (frame, ribbon, title) = Panel(root, 880f, "lives.out_title");
                var close = CloseButton(frame);
                var header = DifficultyBadge(frame, F(585, 650), new Vector2(300, 66), F(585, 590));
                var well = Well(frame, 1150f, new Vector2(720, 400));
                var heart = Pic("Heart", well, Center, new Vector2(0, 62), new Vector2(230, 230), S("UI_Icon_HeartBroken"));
                var body = Text("Body", well, Vector2.zero, Vector2.zero, "Body", 46, White);
                body.textWrappingMode = TextWrappingModes.Normal;
                Stretch(body.rectTransform, 40f, 30f);
                var button = WideButton(frame, "Button", 1500f, S("UI_Button_Wide_Go"), "common.ok");

                var popup = root.AddComponent<MessagePopup>();
                Set(popup, "_content", (RectTransform)frame.parent);
                Set(popup, "_title", title.GetComponent<LocalizedText>());
                Set(popup, "_body", body);
                Set(popup, "_button", button);
                Set(popup, "_buttonLabel", button.GetComponentInChildren<LocalizedText>());
                Set(popup, "_close", close);
                Set(popup, "_header", header);
                Set(popup, "_heart", heart.gameObject);
                Set(popup, "_ribbon", ribbon);
                Set(popup, "_infoRibbon", S("UI_Popup_Ribbon_Info"));
                Set(popup, "_lossRibbon", S("UI_Popup_Ribbon_Lose"));
                PrefabUtility.SaveAsPrefabAsset(root, MessagePopupPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<MessagePopup>(MessagePopupPath);
        }

        private static ResultPopup BuildResultPopup()
        {
            var root = PopupRoot("ResultPopup", new Color(0f, 0f, 0f, 0.85f));
            try
            {
                var (frame, ribbon, title) = Panel(root, 900f, "result.win");
                var close = CloseButton(frame);
                // Content box: picture on top, the reward or the reason under it.
                var well = Well(frame, 1170f, new Vector2(720, 440));
                var mascot = Pic("WinPicture", well, Center, new Vector2(0, 98), new Vector2(190, 190), S("Luno_512"));
                var loss = Pic("LossPicture", well, Center, new Vector2(0, 56), new Vector2(210, 210), S("UI_Icon_HeartBroken"));
                var rewards = Rect("Rewards", well, Center, new Vector2(0, -58), new Vector2(380, 120));
                var coinIcon = Pic("CoinIcon", rewards, Center, new Vector2(-80, 0), new Vector2(116, 116), S("UI_Icon_Reward_CoinSack_CC"));
                var amount = Text("Amount", rewards, new Vector2(75, 2), new Vector2(200, 90), "+20", 64, White);
                var details = Text("Details", well, Vector2.zero, new Vector2(660, 130), "", 42, White);
                details.textWrappingMode = TextWrappingModes.Normal;
                // Sits on the box's bottom edge so a one-line road reward stays clear of the coin row.
                details.alignment = TextAlignmentOptions.Bottom;
                details.rectTransform.pivot = new Vector2(0.5f, 0f);
                Anchor(details.rectTransform, Bottom, new Vector2(0, 24));

                var primary = WideButton(frame, "PrimaryButton", 1510f, S("UI_Button_Wide_Go"), null);
                // Extra choices are purple buttons stacked under the main one.
                var option = WideButton(frame, "OptionButton", 1680f, S("UI_Button_Wide_Purple"), null, 50f);
                var doubleButton = WideButton(frame, "DoubleButton", 1680f, S("UI_Button_Wide_Purple"), "result.double", 50f);
                var adIcon = Pic("Ad", doubleButton.transform, Center, new Vector2(-170, 8), new Vector2(80, 80), S("UI_Icon_Ad"));
                adIcon.raycastTarget = false;
                ((RectTransform)doubleButton.transform.Find("Label")).anchoredPosition = new Vector2(50, 8);

                // Hold-to-peek pill over the tray: fades panel and dim while held, the dim still blocks the board.
                var panelGroup = frame.parent.gameObject.AddComponent<CanvasGroup>();
                // Placed under the frame's bottom edge by the popup; stays visible while the panel fades.
                var peek = Pic("HoldToPeek", root.transform, Center, Vector2.zero, new Vector2(560, 96), S("UI_Pill_Well_96"));
                Text("Label", peek, new Vector2(0, 2), new Vector2(480, 64), "result.peek", 38, White, "result.peek");
                var hold = peek.gameObject.AddComponent<HoldToPeek>();
                Set(hold, "_panel", panelGroup);
                Set(hold, "_dim", root.GetComponent<Image>());

                var popup = root.AddComponent<ResultPopup>();
                Set(popup, "_peek", peek.gameObject);
                Set(popup, "_content", (RectTransform)frame.parent);
                Set(popup, "_frame", frame);
                Set(popup, "_ribbon", ribbon);
                Set(popup, "_winRibbon", S("UI_Popup_Ribbon_Win"));
                Set(popup, "_lossRibbon", S("UI_Popup_Ribbon_Lose"));
                Set(popup, "_title", title.GetComponent<LocalizedText>());
                Set(popup, "_winPicture", mascot.gameObject);
                Set(popup, "_lossPicture", loss.gameObject);
                Set(popup, "_rewards", rewards.gameObject);
                Set(popup, "_reward", amount);
                Set(popup, "_rewardIcon", coinIcon);
                Set(popup, "_coinSack", S("UI_Icon_Reward_CoinSack_CC"));
                Set(popup, "_coinChest", S("UI_Icon_Reward_CoinChest_CC"));
                Set(popup, "_details", details);
                Set(popup, "_double", doubleButton);
                Set(popup, "_close", close);
                Set(popup, "_primary", primary);
                Set(popup, "_optionTemplate", option);
                PrefabUtility.SaveAsPrefabAsset(root, ResultPopupPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<ResultPopup>(ResultPopupPath);
        }

        // Full-screen settings page: title and close on top, three round switches in a row.
        private static SettingsPopup BuildSettingsPopup()
        {
            var root = PopupRoot("SettingsPopup", new Color(0.212f, 0.125f, 0.588f, 1f), coins: false);
            try
            {
                var content = Rect("Content", root.transform, Center, Vector2.zero, Reference);
                // Title row hangs from the safe top like the screens' top bars (the root reaches 400 past it).
                var header = Rect("Header", root.transform, Top, new Vector2(0, DesignTop - 400f), Reference, new Vector2(0.5f, 1f));
                Text("Title", header, At(585, 228), new Vector2(600, 100), "settings.title", 72, White, "settings.title");
                var close = ButtonRect("CloseButton", header, Center, At(1048, 228), new Vector2(124, 128), S("UI_Button_Close"));

                ToggleSwitch Switch(string name, float x, string icon, string key)
                {
                    var button = ButtonRect(name, content, Center, At(x, 896), new Vector2(235, 235), S("UI_InLevel_IconBase"));
                    button.GetComponent<Button>().transition = Selectable.Transition.None;
                    var iconImage = Pic("Icon", button, Center, new Vector2(0, 6), new Vector2(150, 150), S(icon));
                    iconImage.raycastTarget = false;
                    var off = Pic("Off", button, Center, new Vector2(0, 6), new Vector2(150, 150), S("UI_Icon_OffSlash"));
                    off.raycastTarget = false;
                    Text("Label", button, new Vector2(0, -158), new Vector2(260, 54), key, 40, White, key);
                    var toggle = button.gameObject.AddComponent<ToggleSwitch>();
                    Set(toggle, "_track", button.GetComponent<Image>());
                    Set(toggle, "_trackOn", S("UI_InLevel_IconBase"));
                    Set(toggle, "_trackOff", S("UI_InLevel_IconBase"));
                    Set(toggle, "_knob", iconImage.rectTransform);
                    Set(toggle, "_offMark", off.gameObject);
                    var so = new SerializedObject(toggle);
                    so.FindProperty("_knobTravel").floatValue = 0f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return toggle;
                }

                var sound = Switch("Sound", 276, "UI_Icon_Sound", "home.sfx");
                var haptics = Switch("Vibration", 585, "UI_Icon_Vibration", "home.haptics");
                var music = Switch("Music", 894, "UI_Icon_Music", "home.music");

                var popup = root.AddComponent<SettingsPopup>();
                // A full page: shows at once, no panel motion.
                var entrance = new SerializedObject(popup);
                entrance.FindProperty("_entrance").enumValueIndex = (int)PopupEntrance.None;
                entrance.ApplyModifiedPropertiesWithoutUndo();
                Set(popup, "_content", content);
                Set(popup, "_sound", sound);
                Set(popup, "_music", music);
                Set(popup, "_haptics", haptics);
                Set(popup, "_close", close.GetComponent<Button>());
                PrefabUtility.SaveAsPrefabAsset(root, SettingsPopupPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<SettingsPopup>(SettingsPopupPath);
        }

        // ---------------------------------------------------------------- Shared pieces

        private static readonly Vector2 Center = new(0.5f, 0.5f);
        private static readonly Vector2 Top = new(0.5f, 1f);
        private static readonly Vector2 Bottom = new(0.5f, 0f);

        // A point on the 1170x2532 design screen (x from the left, y from the top) relative to its centre.
        private static Vector2 At(float x, float y) => new(x - Reference.x * 0.5f, Reference.y * 0.5f - y);

        // The same point relative to the top centre of a popup frame.
        private static Vector2 F(float x, float y) => new(x - Reference.x * 0.5f, FrameTop - y);

        // Status band and home-bar band of the 1170x2532 design screen.
        private const float DesignTop = 141f;
        private const float DesignBottom = 102f;

        private static void SetMinimumInsets(SafeArea safe)
        {
            var so = new SerializedObject(safe);
            so.FindProperty("_minTop").floatValue = DesignTop / Reference.y;
            so.FindProperty("_minBottom").floatValue = DesignBottom / Reference.y;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Design boxes hung from the safe area's top and stood on its bottom; taller screens add space in between.
        private static (RectTransform Area, RectTransform Top, RectTransform Bottom) EdgeStages(Transform canvas)
        {
            var area = Span("SafeArea", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            SetMinimumInsets(area.gameObject.AddComponent<SafeArea>());
            var top = Rect("TopStage", area, Top, new Vector2(0, DesignTop), Reference, new Vector2(0.5f, 1f));
            var bottom = Rect("BottomStage", area, Bottom, new Vector2(0, -DesignBottom), Reference, new Vector2(0.5f, 0f));
            return (area, top, bottom);
        }

        // A dimmed full-screen root.
        private static GameObject PopupRoot(string name, Color dim, bool coins = true)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0, -400);
            rect.offsetMax = new Vector2(0, 400);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = dim;
            if (!coins) return root;
            // Coin counter above the dim at the in-level spot, hung from the safe top (the root reaches 400 past it).
            var pill = Rect("Coins", rect, Top, new Vector2(0, DesignTop - 400f), Reference, new Vector2(0.5f, 1f));
            var bar = Pic("CoinBar", pill, Center, At(201, 262), new Vector2(284, 100), S("UI_Pill_Well_96"));
            var amount = Text("Amount", bar, new Vector2(27, -2), new Vector2(162, 66), "0", 46, White);
            Pic("Coin", pill, Center, At(93, 254), new Vector2(93, 93), S("UI_Icon_Coin"));
            var hud = pill.gameObject.AddComponent<EconomyHud>();
            Set(hud, "_coins", amount);
            return root;
        }

        // The 999-wide panel hangs from its top edge so it can grow downwards.
        private static (RectTransform Frame, Image Ribbon, TMP_Text Title) Panel(GameObject root, float height, string titleKey)
        {
            // Centred holder so the open scale grows from the middle of the screen.
            var content = Rect("Content", root.transform, Center, Vector2.zero, Reference);
            var frame = Rect("Frame", content, Center, new Vector2(0, Reference.y * 0.5f - FrameTop), new Vector2(880, height), new Vector2(0.5f, 1f));
            Sliced(frame.gameObject.AddComponent<Image>(), S("UI_Popup_Panel"));
            var ribbon = Pic("Ribbon", frame, Top, F(585, 770), new Vector2(760, 214), S("UI_Popup_Ribbon_Info"));
            ribbon.raycastTarget = false;
            var title = Text("Title", ribbon, new Vector2(0, 16), new Vector2(540, 100), titleKey, 64, White, titleKey);
            return (frame, ribbon, title);
        }

        private static RectTransform Well(RectTransform frame, float y, Vector2 size)
        {
            var well = Pic("Well", frame, Top, F(585, y), size, S("UI_InLevel_Tray"));
            well.raycastTarget = false;
            return well.rectTransform;
        }

        private static Button CloseButton(RectTransform frame) =>
            ButtonRect("CloseButton", frame, Top, F(1004, 786), new Vector2(124, 128), S("UI_Button_Close")).GetComponent<Button>();

        // A wide button at y on the design screen, at the sprite's 150 height; a null key leaves the label for code.
        // Hidden extras for code: a coin price chip (left), an owned-count badge (corner), a "-1 life" line (below).
        private static Button WideButton(Component parent, string name, float y, Sprite sprite, string key, float labelSize = 58f)
        {
            var box = new Vector2(630, 150);
            var rect = ButtonRect(name, parent, Top, F(585, y), box, sprite);
            var button = rect.GetComponent<Button>();
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { disabledSprite = S("UI_Button_Wide_Off") };
            Text("Label", rect, new Vector2(0, 8), new Vector2(440, 90), key ?? "Option", labelSize, White, key);

            var price = Rect("Price", rect, Center, new Vector2(-190, 8), new Vector2(200, 90));
            Pic("Coin", price, Center, new Vector2(-50, 0), new Vector2(72, 72), S("UI_Icon_Coin")).raycastTarget = false;
            Text("Amount", price, new Vector2(38, 0), new Vector2(100, 64), "0", 44, White);
            price.gameObject.SetActive(false);
            var badge = Pic("Badge", rect, Center, new Vector2(292, 52), new Vector2(76, 79), S("UI_Badge_Count"));
            badge.raycastTarget = false;
            Text("Count", badge, new Vector2(0, 4), new Vector2(60, 52), "0", 40, White);
            badge.gameObject.SetActive(false);
            Text("Cost", rect, new Vector2(0, -98), new Vector2(300, 48), "game.life_cost", 38, White, "game.life_cost").gameObject.SetActive(false);
            return button;
        }

        // Icon on the round-cornered base: settings and side buttons.
        private static Button IconButton(RectTransform parent, string name, Vector2 position, Sprite icon, float size, float iconSize)
        {
            var rect = ButtonRect(name, parent, Center, position, new Vector2(size, size), S("UI_InLevel_IconBase"));
            Pic("Icon", rect, Center, new Vector2(0, 4), new Vector2(iconSize, iconSize), icon).raycastTarget = false;
            return rect.GetComponent<Button>();
        }

        private static Button SideButton(RectTransform parent, string name, Vector2 position, Sprite icon, string key)
        {
            var button = IconButton(parent, name, position, icon, 150f, 110f);
            Text("Label", button.transform, new Vector2(0, -100), new Vector2(160, 46), key, 32, White, key);
            return button;
        }

        private static RectTransform Group(string name, Component parent, Vector2 offset, float duration)
        {
            var group = Span(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Entrance(group, offset, duration, 0f, PrimeTween.Ease.OutCubic);
            return group;
        }

        private static void Entrance(RectTransform rect, Vector2 offset, float duration, float delay, PrimeTween.Ease ease)
        {
            var entrance = rect.gameObject.AddComponent<UiEntrance>();
            var so = new SerializedObject(entrance);
            so.FindProperty("_offset").vector2Value = offset;
            so.FindProperty("_duration").floatValue = duration;
            so.FindProperty("_delay").floatValue = delay;
            so.FindProperty("_ease").intValue = (int)ease;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Clear(Transform canvas)
        {
            for (var i = canvas.childCount - 1; i >= 0; i--) Object.DestroyImmediate(canvas.GetChild(i).gameObject);
        }

        // ---------------------------------------------------------------- Primitives

        private static RectTransform Span(string name, Component parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent.transform, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Rect(string name, Component parent, Vector2 anchor, Vector2 position, Vector2 size,
            Vector2? pivot = null)
        {
            var rect = Span(name, parent, anchor, anchor, position, size);
            rect.pivot = pivot ?? Center;
            return rect;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect, float x = 0f, float y = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(x, y);
            rect.offsetMax = new Vector2(-x, -y);
        }

        private static Image Pic(string name, Component parent, Vector2 anchor, Vector2 position, Vector2 size, Sprite sprite,
            Vector2? pivot = null)
        {
            var rect = Rect(name, parent, anchor, position, size, pivot);
            var image = rect.gameObject.AddComponent<Image>();
            Sliced(image, sprite);
            return image;
        }

        // Sprites with borders stretch as 9-slices; others keep their shape.
        private static void Sliced(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = image.type == UnityEngine.UI.Image.Type.Simple;
            // Left/right-only slices: scale the caps with the height so a taller pill keeps its rounded ends.
            var height = image.rectTransform.sizeDelta.y;
            if (image.type == UnityEngine.UI.Image.Type.Sliced && sprite.border.y == 0 && sprite.border.w == 0 && height > 0)
                image.pixelsPerUnitMultiplier = sprite.rect.height / height;
        }

        private static RectTransform ButtonRect(string name, Component parent, Vector2 anchor, Vector2 position, Vector2 size, Sprite sprite)
        {
            var image = Pic(name, parent, anchor, position, size, sprite);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            image.gameObject.AddComponent<ButtonFeedback>();
            return image.rectTransform;
        }

        private static TMP_Text Text(string name, Component parent, Vector2 position, Vector2 size, string value,
            float fontSize, Color color, string key = null)
        {
            var rect = Rect(name, parent, Center, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = fontSize;
            // The display face is already heavy; faux bold would smear it.
            text.fontStyle = FontStyles.Normal;
            text.color = color;
            // Midline centres on the drawn glyphs, which reads centred in pills and buttons.
            text.alignment = TextAlignmentOptions.Midline;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            // Auto-size never drops below 70% so a role keeps one look.
            text.fontSizeMin = Mathf.Round(fontSize * 0.7f);
            text.fontSizeMax = fontSize;
            text.raycastTarget = false;
            if (key != null)
            {
                var localized = rect.gameObject.AddComponent<LocalizedText>();
                var so = new SerializedObject(localized);
                so.FindProperty("_key").stringValue = key;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return text;
        }

        private static GameObject Root(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            throw new System.InvalidOperationException($"[GameUI] '{name}' not found in {scene.path}.");
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null) throw new System.InvalidOperationException($"[GameUI] {target.GetType().Name}.{field} not found.");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null) throw new System.InvalidOperationException($"[GameUI] {target.GetType().Name}.{field} not found.");
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
