using System.Collections.Generic;
using Project.Common.UI;
using Project.Common.UI.PopUps;
using Project.Common.Visual;
using Project.Contexts.Application.Economy;
using Project.Contexts.Game;
using Project.Contexts.Home;
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

            // The winding road fills whatever height lies between the period ribbon and Play.
            var road = Span("Road", area, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            road.offsetMin = new Vector2(-Reference.x * 0.5f, RoadBottom);
            road.offsetMax = new Vector2(Reference.x * 0.5f, -RoadTop);
            var roadView = BuildRoad(road);

            var bottom = Group("Bottom", bottomStage, new Vector2(0, -420), 0.4f);
            var play = ButtonRect("PlayButton", bottom, Center, At(585, 2317), new Vector2(750, 150), S("UI_Button_Wide_Go"));
            var playLabel = Text("Label", play, new Vector2(0, 6), new Vector2(660, 110), "Play · Level 1", 66, White);

            var top = Group("Top", topStage, new Vector2(0, 360), 0.4f);
            // Lives on the left, coins and settings on the right, the period ribbon centred under them.
            var livesBar = Pic("LivesBar", top, Center, At(201, 223), new Vector2(240, 96), S("UI_Pill_Well_96"));
            var livesCount = Text("Count", livesBar, new Vector2(30, 0), new Vector2(150, 80), "5", 56, White);
            var heart = Pic("Heart", top, Center, At(84, 223), new Vector2(124, 124), S("UI_Icon_Heart"));
            var livesTimer = Text("Timer", top, At(201, 296), new Vector2(240, 44), "Full", 30, White);
            var lifeAd = ButtonRect("RewardedLifeButton", top, Center, At(360, 223), new Vector2(80, 80), S("UI_Icon_Ad"));
            var coinBar = Pic("CoinBar", top, Center, At(785, 229), new Vector2(292, 96), S("UI_Pill_Well_96"));
            var coins = Text("Amount", coinBar, new Vector2(30, 0), new Vector2(200, 80), "0", 52, White);
            Pic("Coin", top, Center, At(676, 229), new Vector2(118, 118), S("UI_Icon_Coin"));
            var gear = IconButton(top, "SettingsButton", At(1048, 228), S("UI_Icon_Settings"), 150f, 100f);
            var ribbon = Pic("PeriodRibbon", top, Center, At(585, 375), new Vector2(554, 157), S("UI_Popup_Ribbon_Info"));
            var period = Text("Label", ribbon, new Vector2(0, 14), new Vector2(380, 80), "Period 1", 54, White);

            // Right column: event and shop slots. The left column (daily, puzzle) stays empty: no such features yet.
            var challenge = SideButton(top, "ChallengeButton", At(1080, 598), S("UI_Icon_Challenge"), "home.challenge");
            var starter = SideButton(top, "StarterPackButton", At(1080, 821), S("UI_Icon_Shop"), "home.starter_pack");

            var hud = area.gameObject.AddComponent<EconomyHud>();
            Set(hud, "_coins", coins);
            Set(hud, "_livesCount", livesCount);
            Set(hud, "_livesTimer", livesTimer);
            Set(hud, "_heart", heart);
            Set(hud, "_heartSprite", S("UI_Icon_Heart"));
            Set(hud, "_unlimitedSprite", S("UI_Icon_HeartInfinite"));

            var screen = Root(scene, "HomeScreen").GetComponent<HomeScreen>();
            Set(screen, "_view", canvas.gameObject);
            Set(screen, "_levelText", playLabel);
            Set(screen, "_difficulty", null);
            Set(screen, "_metaText", period);
            Set(screen, "_road", roadView.Road);
            Set(screen, "_roadButton", roadView.Chest);
            Set(screen, "_playButton", play.GetComponent<Button>());
            Set(screen, "_starterPackButton", starter);
            Set(screen, "_challengeButton", challenge);
            Set(screen, "_rewardedLifeButton", lifeAd.GetComponent<Button>());
            Set(screen, "_settingsButton", gear);
            Set(screen, "_settingsPopup", settingsPopup);
            Set(screen, "_messagePopup", message);
        }

        // Road edges inside the safe area: under the period ribbon, above the Play button.
        private const float RoadTop = 454f - DesignTop + 16f;
        private const float RoadBottom = 2532f - 2242f - DesignBottom + 20f;

        private static (JourneyRoad Road, Button Chest) BuildRoad(RectTransform road)
        {
            // Node centres of the journey mock, bottom (won) to top (coming), mapped onto the road's height.
            Vector2[] mock =
            {
                new(584, 2047), new(819, 1871), new(876, 1696), new(713, 1522), new(452, 1347),
                new(291, 1174), new(353, 1000), new(590, 823), new(823, 648), new(876, 471)
            };
            const float first = 471f, last = 2047f, pad = 0.07f;
            Vector2 AnchorOf(Vector2 p) => new(p.x / Reference.x, pad + (1f - 2f * pad) * (1f - (p.y - first) / (last - first)));

            var glow = Pic("CurrentGlow", road, Center, Vector2.zero, new Vector2(330, 330), S("UI_Toggle_Knob"));
            glow.color = new Color(1f, 0.78f, 0.35f, 0.35f);
            glow.raycastTarget = false;

            // Dotted path under the nodes: gold up to the current node, grey after it.
            var segments = new GameObject[mock.Length - 1];
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = Span("Path" + i, road, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                for (var d = 1; d <= 3; d++)
                {
                    var anchor = Vector2.Lerp(AnchorOf(mock[i]), AnchorOf(mock[i + 1]), d / 4f);
                    var dot = Pic("Dot" + d, segment, anchor, Vector2.zero, new Vector2(26, 26), S("UI_Toggle_Knob"));
                    dot.color = i < 4 ? new Color(1f, 0.70f, 0.15f) : new Color(0.62f, 0.62f, 0.72f, 0.85f);
                    dot.raycastTarget = false;
                }
                segments[i] = segment.gameObject;
            }

            var view = road.gameObject.AddComponent<JourneyRoad>();
            var so = new SerializedObject(view);
            var nodes = so.FindProperty("_nodes");
            nodes.arraySize = mock.Length;
            for (var i = 0; i < mock.Length; i++)
            {
                var node = Rect("Node" + i, road, AnchorOf(mock[i]), Vector2.zero, new Vector2(150, 150));
                var body = Pic("Body", node, Center, Vector2.zero, Vector2.zero, S("UI_Toggle_Knob"));
                Stretch(body.rectTransform);
                body.raycastTarget = false;
                var number = Text("Number", node, new Vector2(0, 2), new Vector2(130, 90), "1", 64, Ink);
                var check = Pic("Check", node, Center, new Vector2(52, -50), new Vector2(66, 66), S("UI_Icon_Check"));
                // Comets on the top-right of hard (one) and super hard (two) nodes.
                var marks = Rect("Marks", node, new Vector2(1f, 1f), new Vector2(-30, -6), new Vector2(100, 52));
                var comets = new GameObject[2];
                for (var c = 0; c < comets.Length; c++)
                    comets[c] = Pic("Comet" + c, marks, Center, new Vector2(-24 + c * 44, 0), new Vector2(52, 52), S("UI_Icon_Comet")).gameObject;
                var tag = marks.gameObject.AddComponent<DifficultyTag>();
                Set(tag, "_root", marks.gameObject);
                SetArray(tag, "_marks", comets);

                var element = nodes.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Rect").objectReferenceValue = node;
                element.FindPropertyRelative("Body").objectReferenceValue = body;
                element.FindPropertyRelative("Number").objectReferenceValue = number;
                element.FindPropertyRelative("Check").objectReferenceValue = check.gameObject;
                element.FindPropertyRelative("Tag").objectReferenceValue = tag;
            }
            so.FindProperty("_currentIndex").intValue = 4;
            so.FindProperty("_nodeSize").floatValue = 150f;
            so.FindProperty("_currentSize").floatValue = 210f;
            so.FindProperty("_mascotOffset").vector2Value = new Vector2(190f, 0f);
            so.FindProperty("_chestOffset").vector2Value = new Vector2(-120f, 70f);
            so.ApplyModifiedPropertiesWithoutUndo();

            var chest = ButtonRect("RoadChest", road, Center, Vector2.zero, new Vector2(126, 126), S("UI_Icon_RewardChest"));
            var mascot = Pic("Mascot", road, Center, Vector2.zero, new Vector2(250, 250), S("Luno_512"));
            mascot.raycastTarget = false;
            Set(view, "_mascot", mascot.rectTransform);
            Set(view, "_chest", chest);
            Set(view, "_currentGlow", glow.gameObject);
            SetArray(view, "_segments", segments);
            return (view, chest.GetComponent<Button>());
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

                var lockText = Text("Lock", button, new Vector2(0, -70), new Vector2(170, 48), "Lv 1", 34, White);

                // Count badge at the bottom-right; the coin price box takes the bottom centre when stock is out.
                var count = Pic("CountBadge", button, Center, new Vector2(63, -69), new Vector2(86, 89), S("UI_Badge_Count"));
                count.raycastTarget = false;
                var countText = Text("Count", count, new Vector2(0, 2), new Vector2(76, 66), "0", 48, White);

                var price = Pic("PricePill", button, Center, new Vector2(13, -79), new Vector2(169, 64), S("UI_Pill_Well_60"));
                price.raycastTarget = false;
                Pic("Coin", price, new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(62, 62), S("UI_Icon_Coin")).raycastTarget = false;
                var priceText = Text("Amount", price, new Vector2(22, 0), new Vector2(104, 52), "0", 38, White);

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
            var coins = Text("Amount", coinBar, new Vector2(27, -4), new Vector2(162, 76), "0", 52, White);
            Pic("Coin", top, Center, At(93, 254), new Vector2(93, 93), S("UI_Icon_Coin"));
            var pill = Pic("LevelPill", top, Center, At(585, 446), new Vector2(432, 140), S("UI_InLevel_LevelPill"));
            var levelText = Text("Level", pill, new Vector2(0, 8), new Vector2(371, 90), "Level 1", 66, White);
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

        // Shared popup frame: panel top 608 on the design screen.
        private const float FrameTop = 608f;

        private static MessagePopup BuildMessagePopup()
        {
            var root = PopupRoot("MessagePopup", new Color(0f, 0f, 0f, 0.894f));
            try
            {
                var (frame, ribbon, title) = Panel(root, 1331f, "lives.out_title");
                var close = CloseButton(frame);
                var header = DifficultyBadge(frame, F(585, 499), new Vector2(381, 81), F(585, 430));
                var well = Well(frame, 1163f, new Vector2(866, 655));
                var heart = Pic("Heart", well, Center, new Vector2(0, 70), new Vector2(300, 300), S("UI_Icon_HeartBroken"));
                var body = Text("Body", well, Vector2.zero, Vector2.zero, "Body", 52, White);
                body.textWrappingMode = TextWrappingModes.Normal;
                Stretch(body.rectTransform, 50f, 50f);
                var button = WideButton(frame, "Button", 1669f, S("UI_Button_Wide_Go"), "common.ok");

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
                var (frame, ribbon, title) = Panel(root, 1331f, "result.win");
                var close = CloseButton(frame);
                // Content box of the fail and win panels.
                var well = Well(frame, 1120f, new Vector2(816, 361));
                var mascot = Pic("WinPicture", well, Center, new Vector2(-190, 10), new Vector2(260, 260), S("Luno_512"));
                var loss = Pic("LossPicture", well, Center, new Vector2(0, 40), new Vector2(220, 220), S("UI_Icon_HeartBroken"));
                var rewards = Rect("Rewards", well, Center, new Vector2(130, 10), new Vector2(380, 160));
                var coinIcon = Pic("CoinIcon", rewards, Center, new Vector2(-90, 0), new Vector2(140, 140), S("UI_Icon_Reward_CoinSack_CC"));
                var amount = Text("Amount", rewards, new Vector2(100, 0), new Vector2(200, 100), "+20", 72, White);
                var details = Text("Details", well, Vector2.zero, new Vector2(740, 100), "", 34, White);
                details.textWrappingMode = TextWrappingModes.Normal;
                details.rectTransform.pivot = new Vector2(0.5f, 0f);
                Anchor(details.rectTransform, Bottom, new Vector2(0, 22));

                var primary = WideButton(frame, "PrimaryButton", 1669f, S("UI_Button_Wide_Go"), null);
                // Extra choices are smaller purple buttons stacked under the main one.
                var option = WideButton(frame, "OptionButton", 1909f, S("UI_Button_Wide_Purple"), null, new Vector2(560, 220));
                var doubleButton = WideButton(frame, "DoubleButton", 1909f, S("UI_Button_Wide_Purple"), "result.double", new Vector2(560, 220));
                var adIcon = Pic("Ad", doubleButton.transform, new Vector2(0f, 0.5f), new Vector2(110, 6), new Vector2(84, 84), S("UI_Icon_Ad"));
                adIcon.raycastTarget = false;
                var doubleLabel = (RectTransform)doubleButton.transform.Find("Label");
                doubleLabel.anchoredPosition = new Vector2(50, 8);
                doubleLabel.sizeDelta = new Vector2(360, 100);

                var popup = root.AddComponent<ResultPopup>();
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
                Text("Title", content, At(585, 227), new Vector2(600, 110), "settings.title", 80, White, "settings.title");
                var close = ButtonRect("CloseButton", content, Center, At(1003, 227), new Vector2(150, 155), S("UI_Button_Close"));

                ToggleSwitch Switch(string name, float x, string icon, string key)
                {
                    var button = ButtonRect(name, content, Center, At(x, 896), new Vector2(235, 235), S("UI_InLevel_IconBase"));
                    button.GetComponent<Button>().transition = Selectable.Transition.None;
                    var iconImage = Pic("Icon", button, Center, new Vector2(0, 6), new Vector2(150, 150), S(icon));
                    iconImage.raycastTarget = false;
                    var off = Pic("Off", button, Center, new Vector2(0, 6), new Vector2(150, 150), S("UI_Icon_OffSlash"));
                    off.raycastTarget = false;
                    Text("Label", button, new Vector2(0, -160), new Vector2(260, 60), key, 44, White, key);
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
            var amount = Text("Amount", bar, new Vector2(27, -4), new Vector2(162, 76), "0", 52, White);
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
            var frame = Rect("Frame", content, Center, new Vector2(0, Reference.y * 0.5f - FrameTop), new Vector2(999, height), new Vector2(0.5f, 1f));
            Sliced(frame.gameObject.AddComponent<Image>(), S("UI_Popup_Panel"));
            var ribbon = Pic("Ribbon", frame, Top, F(585, 689), new Vector2(730, 206), S("UI_Popup_Ribbon_Info"));
            ribbon.raycastTarget = false;
            var title = Text("Title", ribbon, new Vector2(0, 16), new Vector2(560, 120), titleKey, 72, White, titleKey);
            return (frame, ribbon, title);
        }

        private static RectTransform Well(RectTransform frame, float y, Vector2 size)
        {
            var well = Pic("Well", frame, Top, F(585, y), size, S("UI_InLevel_Tray"));
            well.raycastTarget = false;
            return well.rectTransform;
        }

        private static Button CloseButton(RectTransform frame) =>
            ButtonRect("CloseButton", frame, Top, F(1041, 703), new Vector2(181, 187), S("UI_Button_Close")).GetComponent<Button>();

        // A wide button at y on the design screen; a null key leaves the label for code.
        private static Button WideButton(Component parent, string name, float y, Sprite sprite, string key, Vector2? size = null)
        {
            var box = size ?? new Vector2(707, 278);
            var rect = ButtonRect(name, parent, Top, F(585, y), box, sprite);
            var button = rect.GetComponent<Button>();
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { disabledSprite = S("UI_Button_Wide_Off") };
            Text("Label", rect, new Vector2(0, 10), box * new Vector2(0.85f, 0.54f), key ?? "Option", box.y * 0.26f, White, key);
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
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Min(20f, fontSize);
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
