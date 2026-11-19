using Project.Common.UI;
using Project.Common.UI.PopUps;
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
    // Top elements hang from the safe area's top, bottom panels sit on its bottom; the board gets the middle.
    public static class GameUiBuilder
    {
        private const string MainScenePath = "Assets/Scenes/Main.unity";
        private const string ResultPopupPath = "Assets/Prefabs/Popups/ResultPopup.prefab";
        private const string MessagePopupPath = "Assets/Prefabs/Popups/MessagePopup.prefab";

        // Distances from the safe area's edges, in canvas units (1080 wide).
        private const float TopRowY = -120f;
        private const float HomeTopRowY = -110f;
        private const float RoundButton = 124f;
        private const float MenuStep = 166f;

        private static readonly Color Ink = new(0.13f, 0.14f, 0.24f);
        private static readonly Color White = Color.white;
        private static readonly Color Pill = new(0.95f, 0.93f, 0.88f);
        private static readonly Color Coin = new(0.98f, 0.76f, 0.22f);
        private static readonly Color Gear = new(0.36f, 0.42f, 0.86f);
        private static readonly Color Toggle = new(0.30f, 0.75f, 0.30f);
        private static readonly Color Danger = new(0.86f, 0.24f, 0.28f);
        private static readonly Color Go = new(0.30f, 0.76f, 0.34f);
        private static readonly Color Badge = new(0.88f, 0.20f, 0.36f);
        private static readonly Color Panel = new(0.20f, 0.22f, 0.46f, 0.92f);
        private static readonly Color Booster = new(0.40f, 0.34f, 0.80f);
        private static readonly Color Banner = new(0.17f, 0.19f, 0.36f);
        private static readonly Color Ribbon = new(0.78f, 0.18f, 0.22f);
        private static readonly Color Mark = new(0.94f, 0.92f, 0.86f);
        private static readonly Color Frame = new(0.62f, 0.14f, 0.22f);
        private static readonly Color FrameBand = new(0.46f, 0.08f, 0.16f);
        private static readonly Color Inner = new(0.97f, 0.94f, 0.88f);
        private static readonly Color Strip = new(0.40f, 0.06f, 0.12f, 0.8f);
        private static readonly Color Tag = new(0.20f, 0.26f, 0.52f);
        private static readonly Color HomeSky = new(0.32f, 0.44f, 0.78f);
        private static readonly Color HomeArt = new(0.42f, 0.62f, 0.52f);
        private static readonly Color Nav = new(0.40f, 0.30f, 0.82f);
        private static readonly Color NavHome = new(0.52f, 0.42f, 0.94f);
        private static readonly Color Lock = new(0.08f, 0.08f, 0.14f, 0.75f);
        private static readonly Color Armed = new(0.55f, 0.95f, 0.80f);

        private static Sprite _rounded;
        private static Sprite _circle;
        private static TMP_FontAsset _font;

        [MenuItem("Tools/Project/Build Game UI")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[GameUI] Leave Play mode first.");
                return;
            }

            _rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            _font = TMP_Settings.defaultFontAsset;

            var message = BuildMessagePopup();
            var result = BuildResultPopup();

            var scene = SceneManager.GetSceneByPath(MainScenePath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var canvas in new[] { "Home Canvas", "HUD Canvas", "UI Canvas" })
                    Root(scene, canvas).GetComponent<CanvasScaler>().matchWidthOrHeight = 0f;
                BuildHome(scene, message);
                BuildHud(scene, message, result);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[GameUI] Home, HUD and popups built.");
        }

        private static void BuildHome(Scene scene, MessagePopup message)
        {
            var canvas = Root(scene, "Home Canvas").transform;
            Clear(canvas);

            var sky = Span("Background", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Img(sky, HomeSky, null);
            var area = SafeAreaRoot(canvas);

            var art = Span("Art", area, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            art.offsetMin = new Vector2(40f, 640f);
            art.offsetMax = new Vector2(-40f, -260f);
            Img(art, HomeArt, _rounded, 0.25f);
            Text("Label", art, Vector2.zero, new Vector2(600, 120), "ART", 72, new Color(1, 1, 1, 0.35f));

            var bottom = Group("Bottom", area, new Vector2(0, -500), 0.4f);

            var play = ButtonRect("PlayButton", bottom, Bottom, new Vector2(0, 450), new Vector2(520, 220), Danger, _rounded, 0.3f);
            var playLevel = Text("Level", play, new Vector2(0, -20), new Vector2(480, 110), "Level 1", 78, White);
            var playTag = DifficultyRibbon(play, new Vector2(0, 88), new Vector2(260, 52), withMarks: false);
            var tag = Rect("RewardTag", play, Center, new Vector2(240, 96), new Vector2(124, 64));
            Img(tag, Tag, _rounded, 0.4f);
            tag.localRotation = Quaternion.Euler(0, 0, 15f);
            var reward = Text("Amount", tag, Vector2.zero, new Vector2(116, 56), "+20", 34, White);

            var road = ButtonRect("RoadButton", bottom, Bottom, new Vector2(420, 450), new Vector2(160, 200), new Color(0, 0, 0, 0), null);
            var roadIcon = Rect("Icon", road, Center, new Vector2(0, 30), new Vector2(120, 120));
            Img(roadIcon, White, _rounded, 0.4f);
            Text("Glyph", roadIcon, Vector2.zero, new Vector2(110, 110), "■", 70, Gear);
            var roadPill = Rect("Count", road, Center, new Vector2(0, -62), new Vector2(160, 56));
            Img(roadPill, Coin, _rounded, 0.4f);
            var roadText = Text("Text", roadPill, Vector2.zero, new Vector2(150, 50), "0/10", 34, Ink);

            // Only Home exists; the other tabs say "coming soon".
            var nav = Span("TabBar", bottom, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 100), new Vector2(0, 200));
            var navBack = Span("Back", nav, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            navBack.offsetMin = new Vector2(0, -300);
            Img(navBack, Nav, null);
            var soon = new Button[4];
            var glyphs = new[] { "$", "♦", "■", "♣", "♠" };
            for (int i = 0, s = 0; i < 5; i++)
            {
                var anchor = new Vector2((i + 0.5f) / 5f, 0.5f);
                if (i == 2)
                {
                    var home = Rect("HomeTab", nav, anchor, new Vector2(0, 25), new Vector2(230, 250));
                    Img(home, NavHome, _rounded, 0.3f);
                    Text("Glyph", home, new Vector2(0, 30), new Vector2(200, 120), glyphs[i], 90, White);
                    Text("Label", home, new Vector2(0, -75), new Vector2(210, 60), "Home", 38, White, "nav.home");
                    continue;
                }
                var tab = ButtonRect("Tab" + i, nav, anchor, Vector2.zero, new Vector2(200, 180), new Color(1, 1, 1, 0.08f), _rounded, 0.3f);
                Text("Glyph", tab, Vector2.zero, new Vector2(180, 160), glyphs[i], 90, White);
                soon[s++] = tab.GetComponent<Button>();
            }

            var top = Group("Top", area, new Vector2(0, 400), 0.4f);
            var avatar = Rect("Avatar", top, TopLeft, new Vector2(40, HomeTopRowY), new Vector2(140, 140), new Vector2(0, 0.5f));
            Img(avatar, Gear, _rounded, 0.3f);
            Text("Glyph", avatar, Vector2.zero, new Vector2(130, 130), "●", 80, White);

            var coins = CoinPill(top, TopLeft, new Vector2(210, HomeTopRowY), 270f);
            Plus(coins, new Vector2(-80, -40), false);

            var lives = Rect("LivesPill", top, TopRight, new Vector2(-190, HomeTopRowY), new Vector2(270, 80), new Vector2(1, 0.5f));
            Img(lives, Pill, _rounded, 0.4f);
            var heart = Rect("Heart", lives, new Vector2(0, 0.5f), Vector2.zero, new Vector2(110, 110));
            Img(heart, Danger, _circle);
            Text("Glyph", heart, new Vector2(0, 4), new Vector2(110, 110), "♥", 96, new Color(1, 1, 1, 0.25f));
            var livesCount = Text("Count", heart, new Vector2(0, 2), new Vector2(100, 90), "5", 52, White);
            var livesTimer = Text("Timer", lives, new Vector2(30, 0), new Vector2(190, 70), "Full", 40, Ink);
            var lifePlus = Plus(lives, new Vector2(-235, -40), true);

            var meta = Text("Meta", top, Vector2.zero, new Vector2(700, 50), "Meta 1", 32, White);
            Anchor(meta.rectTransform, Top, new Vector2(0, -215));

            var starter = SideButton(top, "StarterPackButton", new Vector2(100, -340), "$", "home.starter_pack");
            var challenge = SideButton(top, "ChallengeButton", new Vector2(100, -520), "♦", "home.challenge");

            BuildSettingsMenu(top, HomeTopRowY, withExit: false);

            var hud = area.gameObject.AddComponent<EconomyHud>();
            Set(hud, "_coins", coins.Find("Amount").GetComponent<TMP_Text>());
            Set(hud, "_livesCount", livesCount);
            Set(hud, "_livesTimer", livesTimer);

            var screen = Root(scene, "HomeScreen").GetComponent<HomeScreen>();
            Set(screen, "_view", canvas.gameObject);
            Set(screen, "_levelText", playLevel);
            Set(screen, "_difficulty", playTag);
            Set(screen, "_rewardTag", reward);
            Set(screen, "_metaText", meta);
            Set(screen, "_roadButton", road.GetComponent<Button>());
            Set(screen, "_roadText", roadText);
            Set(screen, "_playButton", play.GetComponent<Button>());
            Set(screen, "_starterPackButton", starter);
            Set(screen, "_challengeButton", challenge);
            Set(screen, "_rewardedLifeButton", lifePlus);
            SetArray(screen, "_comingSoon", soon);
            Set(screen, "_messagePopup", message);
        }

        private static void BuildHud(Scene scene, MessagePopup message, ResultPopup result)
        {
            var canvas = Root(scene, "HUD Canvas").transform;
            Clear(canvas);
            var area = SafeAreaRoot(canvas);

            var panel = Rect("BoosterBar", area, Bottom, new Vector2(0, 125), new Vector2(666, 220));
            Img(panel, Panel, _rounded, 0.25f);
            Entrance(panel, new Vector2(0, -300), 0.3f, 0f, PrimeTween.Ease.OutCubic);
            var bar = panel.gameObject.AddComponent<BoosterBar>();
            var boosters = new[]
            {
                (BoosterType.Undo, "◄", "booster.undo"),
                (BoosterType.ExtraTube, "+", "booster.extra_tube"),
                (BoosterType.FreeMove, "»", "booster.free_move")
            };
            var so = new SerializedObject(bar);
            var slots = so.FindProperty("_slots");
            slots.arraySize = boosters.Length;
            for (var i = 0; i < boosters.Length; i++)
            {
                var (type, glyph, key) = boosters[i];
                var x = (i - 1) * 216f;
                var armed = Rect(type + "Armed", panel, Center, new Vector2(x, 0), new Vector2(184, 184));
                Img(armed, Armed, _rounded, 0.3f);
                armed.gameObject.SetActive(false);

                var button = ButtonRect(type + "Button", panel, Center, new Vector2(x, 0), new Vector2(164, 164), Booster, _rounded, 0.3f);
                Entrance(button, new Vector2(0, -260), 0.2f, 0.1f * i, PrimeTween.Ease.Linear);
                Text("Glyph", button, new Vector2(0, 30), new Vector2(150, 90), glyph, 76, White);
                Text("Name", button, new Vector2(0, -24), new Vector2(150, 40), key, 26, White, key);

                var lockRect = Span("Lock", button, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Img(lockRect, Lock, _rounded, 0.3f).raycastTarget = false;
                var lockLevel = Text("Level", lockRect, Vector2.zero, new Vector2(150, 60), "Lv 1", 38, White);

                var badge = Rect("Badge", button, new Vector2(1, 0), new Vector2(-8, 8), new Vector2(84, 72));
                Img(badge, Badge, _rounded, 0.5f).raycastTarget = false;
                var badgeText = Text("Count", badge, Vector2.zero, new Vector2(76, 60), "0", 40, White);

                var slot = slots.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("Type").enumValueIndex = (int)type;
                slot.FindPropertyRelative("Button").objectReferenceValue = button.GetComponent<Button>();
                slot.FindPropertyRelative("Badge").objectReferenceValue = badgeText;
                slot.FindPropertyRelative("Lock").objectReferenceValue = lockRect.gameObject;
                slot.FindPropertyRelative("LockLevel").objectReferenceValue = lockLevel;
                slot.FindPropertyRelative("Armed").objectReferenceValue = armed.gameObject;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // The settings column is drawn over the dim.
            var top = Group("Top", area, new Vector2(0, 300), 0.3f);
            var coins = CoinPill(top, TopLeft, new Vector2(40, TopRowY), 300f);
            var banner = Rect("LevelBanner", top, Top, new Vector2(0, -270), new Vector2(390, 110));
            Img(banner, Banner, _rounded, 0.3f);
            var levelText = Text("Level", banner, Vector2.zero, new Vector2(370, 100), "Level 1", 64, White);
            var difficulty = DifficultyRibbon(top, Vector2.zero, new Vector2(240, 56), withMarks: true, marksLift: 120f);
            Anchor((RectTransform)difficulty.transform, Top, new Vector2(0, -335));
            var settings = BuildSettingsMenu(top, TopRowY, withExit: true);

            var hud = area.gameObject.AddComponent<EconomyHud>();
            Set(hud, "_coins", coins.Find("Amount").GetComponent<TMP_Text>());

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

        private static MessagePopup BuildMessagePopup()
        {
            var root = PopupRoot("MessagePopup");
            try
            {
                var frame = Rect("Frame", root.transform, Center, Vector2.zero, new Vector2(906, 1180));
                Img(frame, Frame, _rounded, 0.2f);
                var header = DifficultyRibbon(frame, new Vector2(0, 666), new Vector2(500, 110), withMarks: true);
                var band = Rect("TitleBand", frame, Center, new Vector2(0, 541), new Vector2(650, 170));
                Img(band, FrameBand, _rounded, 0.25f);
                var title = Text("Title", band, Vector2.zero, new Vector2(610, 140), "Title", 64, White, "quit.title");
                var close = CloseButton(frame, new Vector2(421, 520));

                var inner = Rect("Inner", frame, Center, new Vector2(0, 91), new Vector2(777, 580));
                Img(inner, Inner, _rounded, 0.25f);
                var heart = Text("Heart", inner, new Vector2(0, 70), new Vector2(360, 320), "♥", 300, Danger);
                var body = Text("Body", inner, new Vector2(0, -190), new Vector2(740, 150), "Body", 46, Ink);

                var button = ButtonRect("Button", frame, Center, new Vector2(0, -376), new Vector2(620, 220), Danger, _rounded, 0.3f);
                var label = Text("Label", button, Vector2.zero, new Vector2(580, 180), "Exit", 70, White, "quit.exit");

                var popup = root.AddComponent<MessagePopup>();
                Set(popup, "_content", frame);
                Set(popup, "_title", title.GetComponent<LocalizedText>());
                Set(popup, "_body", body);
                Set(popup, "_button", button.GetComponent<Button>());
                Set(popup, "_buttonLabel", label.GetComponent<LocalizedText>());
                Set(popup, "_close", close);
                Set(popup, "_header", header);
                Set(popup, "_heart", heart.gameObject);
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
            var root = PopupRoot("ResultPopup");
            try
            {
                // The frame hangs from its top edge, so extra option rows grow it downwards.
                var frame = Rect("Frame", root.transform, Center, new Vector2(0, 590), new Vector2(906, 1180), new Vector2(0.5f, 1f));
                Img(frame, Frame, _rounded, 0.2f);
                var header = DifficultyRibbon(frame, Vector2.zero, new Vector2(500, 110), withMarks: true);
                Anchor((RectTransform)header.transform, Top, new Vector2(0, 76));
                var band = Rect("TitleBand", frame, Top, new Vector2(0, -49), new Vector2(650, 170));
                Img(band, FrameBand, _rounded, 0.25f);
                var levelTitle = Text("Level", band, Vector2.zero, new Vector2(610, 140), "Level 1", 72, White);
                var close = CloseButton(frame, Vector2.zero);
                Anchor((RectTransform)close.transform, Top, new Vector2(421, -70));

                var strip = Rect("Status", frame, Top, new Vector2(0, -265), new Vector2(670, 140));
                Img(strip, Strip, _rounded, 0.3f);
                var statusHeart = Text("Heart", strip, new Vector2(-250, 0), new Vector2(130, 130), "♥", 120, Danger);
                var status = Text("Text", strip, new Vector2(60, 0), new Vector2(500, 100), "Level Failed!", 44, White, "result.failed");

                var rewards = Rect("Rewards", frame, Top, new Vector2(0, -500), new Vector2(800, 300));
                Text("Label", rewards, new Vector2(0, 130), new Vector2(600, 60), "Rewards:", 46, White, "result.rewards");
                var stack = Rect("Coins", rewards, Center, new Vector2(-30, -20), new Vector2(200, 200));
                Img(stack, Coin, _circle);
                Text("Glyph", stack, Vector2.zero, new Vector2(190, 190), "$", 130, new Color(0.6f, 0.4f, 0.05f));
                var amount = Text("Amount", rewards, new Vector2(120, -90), new Vector2(240, 100), "100", 80, White);
                amount.alignment = TextAlignmentOptions.Left;

                var details = Text("Details", frame, Vector2.zero, new Vector2(800, 80), "", 30, White);
                Anchor(details.rectTransform, Top, new Vector2(0, -700));
                var doubleButton = ButtonRect("DoubleButton", frame, Top, new Vector2(0, -800), new Vector2(420, 90), Tag, _rounded, 0.4f);
                Text("Label", doubleButton, Vector2.zero, new Vector2(400, 80), "x2", 36, White, "result.double");

                var primary = ButtonRect("PrimaryButton", frame, Top, new Vector2(0, -980), new Vector2(620, 220), Go, _rounded, 0.3f);
                Text("Label", primary, Vector2.zero, new Vector2(580, 180), "Try Again", 62, White);
                var option = ButtonRect("OptionButton", frame, Top, new Vector2(0, -1225), new Vector2(620, 100), Tag, _rounded, 0.4f);
                Text("Label", option, Vector2.zero, new Vector2(600, 90), "Option", 34, White);

                var popup = root.AddComponent<ResultPopup>();
                Set(popup, "_content", frame);
                Set(popup, "_frame", frame);
                Set(popup, "_header", header);
                Set(popup, "_levelTitle", levelTitle);
                Set(popup, "_status", status.GetComponent<LocalizedText>());
                Set(popup, "_statusHeart", statusHeart.gameObject);
                Set(popup, "_rewards", rewards.gameObject);
                Set(popup, "_reward", amount);
                Set(popup, "_details", details);
                Set(popup, "_double", doubleButton.GetComponent<Button>());
                Set(popup, "_close", close);
                Set(popup, "_primary", primary.GetComponent<Button>());
                Set(popup, "_optionTemplate", option.GetComponent<Button>());
                PrefabUtility.SaveAsPrefabAsset(root, ResultPopupPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<ResultPopup>(ResultPopupPath);
        }

        // The coin pill stays readable above the dim.
        private static GameObject PopupRoot(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0, -400);
            rect.offsetMax = new Vector2(0, 400);
            Img(rect, new Color(0, 0, 0, 0.75f), null);
            // The dim reaches past the safe area; the pill's spot is measured from the safe area's top.
            var pillArea = Span("PillArea", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            pillArea.offsetMin = new Vector2(0, 400);
            pillArea.offsetMax = new Vector2(0, -400);
            var coins = CoinPill(pillArea, TopLeft, new Vector2(40, TopRowY), 300f);
            var hud = pillArea.gameObject.AddComponent<EconomyHud>();
            Set(hud, "_coins", coins.Find("Amount").GetComponent<TMP_Text>());
            return root;
        }

        private static readonly Vector2 Center = new(0.5f, 0.5f);
        private static readonly Vector2 Top = new(0.5f, 1f);
        private static readonly Vector2 Bottom = new(0.5f, 0f);
        private static readonly Vector2 TopLeft = new(0f, 1f);
        private static readonly Vector2 TopRight = new(1f, 1f);

        // The dim sits under the settings column and over everything created before it.
        private static SettingsMenu BuildSettingsMenu(RectTransform parent, float rowY, bool withExit)
        {
            var dim = Span("SettingsDim", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.offsetMin = new Vector2(-600, -3000);
            dim.offsetMax = new Vector2(600, 600);
            Img(dim, new Color(0, 0, 0, 0), null);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dim.gameObject.SetActive(false);

            Button Round(string name, int index, string glyph, Color color)
            {
                var rect = ButtonRect(name, parent, TopRight, new Vector2(-40 - RoundButton / 2, rowY - MenuStep * index),
                    new Vector2(RoundButton, RoundButton), color, _rounded, 0.35f);
                Text("Glyph", rect, Vector2.zero, new Vector2(RoundButton - 10, RoundButton - 10), glyph, 70, White);
                if (index > 0) rect.gameObject.SetActive(false);
                return rect.GetComponent<Button>();
            }

            var gear = Round("SettingsButton", 0, "≡", Gear);
            var sound = Round("SoundButton", 1, "♪", Toggle);
            var music = Round("MusicButton", 2, "♫", Toggle);
            var haptics = Round("VibrationButton", 3, "(•)", Toggle);
            var exit = withExit ? Round("ExitButton", 4, "←", Danger) : null;

            var menu = gear.gameObject.AddComponent<SettingsMenu>();
            Set(menu, "_gear", gear);
            Set(menu, "_dim", dimButton);
            Set(menu, "_sound", sound);
            Set(menu, "_music", music);
            Set(menu, "_haptics", haptics);
            Set(menu, "_exit", exit);
            return menu;
        }

        // The amount text is the child "Amount".
        private static RectTransform CoinPill(RectTransform parent, Vector2 anchor, Vector2 position, float width)
        {
            var pill = Rect("CoinPill", parent, anchor, position, new Vector2(width, 80), new Vector2(0, 0.5f));
            Img(pill, Pill, _rounded, 0.4f);
            var coin = Rect("Coin", pill, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(104, 104));
            Img(coin, Coin, _circle);
            Text("Glyph", coin, Vector2.zero, new Vector2(100, 100), "$", 64, new Color(0.6f, 0.4f, 0.05f));
            var amount = Text("Amount", pill, new Vector2(40, 0), new Vector2(width - 110, 70), "0", 46, Ink);
            amount.name = "Amount";
            return pill;
        }

        private static Button Plus(RectTransform pill, Vector2 offset, bool clickable)
        {
            var plus = clickable
                ? ButtonRect("PlusButton", pill, Center, offset, new Vector2(56, 56), Go, _circle)
                : Rect("Plus", pill, Center, offset, new Vector2(56, 56));
            if (!clickable) Img(plus, Go, _circle);
            Text("Glyph", plus, Vector2.zero, new Vector2(52, 52), "+", 48, White);
            return clickable ? plus.GetComponent<Button>() : null;
        }

        private static Button SideButton(RectTransform parent, string name, Vector2 position, string glyph, string key)
        {
            var rect = ButtonRect(name, parent, TopLeft, position, new Vector2(130, 130), Gear, _rounded, 0.35f);
            Text("Glyph", rect, new Vector2(0, 14), new Vector2(120, 90), glyph, 64, White);
            Text("Label", rect, new Vector2(0, -44), new Vector2(124, 36), key, 22, White, key);
            return rect.GetComponent<Button>();
        }

        private static Button CloseButton(RectTransform parent, Vector2 position)
        {
            var rect = ButtonRect("CloseButton", parent, Center, position, new Vector2(130, 130), Danger, _circle);
            Text("Glyph", rect, Vector2.zero, new Vector2(120, 120), "×", 96, White);
            return rect.GetComponent<Button>();
        }

        private static DifficultyTag DifficultyRibbon(RectTransform parent, Vector2 position, Vector2 size, bool withMarks,
            float marksLift = 0f)
        {
            var root = Rect("Difficulty", parent, Center, position, size);
            var ribbon = Span("Ribbon", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Img(ribbon, Ribbon, _rounded, 0.4f);
            var label = Text("Label", ribbon, Vector2.zero, size - new Vector2(20, 8), "Hard", size.y * 0.6f, White);
            var marks = new GameObject[withMarks ? 3 : 0];
            for (var i = 0; i < marks.Length; i++)
            {
                var big = i == 1;
                var mark = Rect("Mark" + i, root, Top, new Vector2((i - 1) * 50f, marksLift + (big ? 40f : 30f)),
                    big ? new Vector2(56, 56) : new Vector2(44, 44));
                Img(mark, Mark, _circle);
                marks[i] = mark.gameObject;
            }
            var tag = root.gameObject.AddComponent<DifficultyTag>();
            Set(tag, "_root", root.gameObject);
            Set(tag, "_label", label);
            SetArray(tag, "_marks", marks);
            return tag;
        }

        private static RectTransform Group(string name, Transform parent, Vector2 offset, float duration)
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

        private static RectTransform SafeAreaRoot(Transform canvas)
        {
            var area = Span("SafeArea", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            area.gameObject.AddComponent<SafeArea>();
            return area;
        }

        private static void Clear(Transform canvas)
        {
            for (var i = canvas.childCount - 1; i >= 0; i--) Object.DestroyImmediate(canvas.GetChild(i).gameObject);
        }

        private static RectTransform Span(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
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

        private static Image Img(RectTransform rect, Color color, Sprite sprite, float cornerScale = 1f)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            if (sprite == _rounded && sprite != null)
            {
                image.type = Image.Type.Sliced;
                // A smaller multiplier draws the 9-slice corners larger: rounder shapes.
                image.pixelsPerUnitMultiplier = cornerScale;
            }
            return image;
        }

        private static RectTransform ButtonRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
            Color color, Sprite sprite, float cornerScale = 1f)
        {
            var rect = Rect(name, parent, anchor, position, size);
            var image = Img(rect, color, sprite, cornerScale);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            button.colors = colors;
            rect.gameObject.AddComponent<ButtonFeedback>();
            return rect;
        }

        private static TMP_Text Text(string name, Transform parent, Vector2 position, Vector2 size, string value,
            float fontSize, Color color, string key = null)
        {
            var rect = Rect(name, parent, Center, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Min(18f, fontSize);
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
