using Project.Common.UI;
using Project.Common.UI.PopUps;
using Project.Contexts.Application.Economy;
using Project.Contexts.LiveOps;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Editor
{
    // Live ops: the daily reward popup, the Orbit Rush map and their Home entry points.
    public static partial class GameUiBuilder
    {
        private const string DailyRewardPopupPath = "Assets/Prefabs/Popups/DailyRewardPopup.prefab";
        private const string OrbitRushScreenPath = "Assets/Prefabs/Popups/OrbitRushScreen.prefab";
        private const string OrbitRushLayoutPath = "Assets/Configs/OrbitRushMapLayout.asset";

        // ---------------------------------------------------------------- Daily reward

        private static DailyRewardPopup BuildDailyRewardPopup()
        {
            var root = PopupRoot("DailyRewardPopup", new Color(0f, 0f, 0f, 0.85f));
            try
            {
                var (frame, _, _) = Panel(root, 900f, "daily.title");
                var content = (RectTransform)frame.parent;
                // Hero art rests on the ribbon, behind it.
                var hero = Pic("Hero", content, Center, At(585, 520), new Vector2(800, 400), S("UI_DailyReward_Luno"));
                hero.raycastTarget = false;
                hero.transform.SetSiblingIndex(0);
                var close = CloseButton(frame);

                var icons = new[]
                {
                    "UI_Icon_Coin", "UI_Icon_Coin", "UI_Icon_Coin", "UI_Icon_Reward_CoinSack_CC", "UI_Icon_Reward_CoinSack_CC",
                    "UI_Reward_CoinPile_CC", "UI_Icon_Reward_CoinChest_CC"
                };
                var popup = root.AddComponent<DailyRewardPopup>();
                var so = new SerializedObject(popup);
                var days = so.FindProperty("_days");
                days.arraySize = icons.Length;
                for (var i = 0; i < icons.Length; i++)
                {
                    var x = i < 4 ? 585 + (i - 1.5f) * 196f : 585 + (i - 5) * 196f;
                    var y = i < 4 ? 1010f : 1250f;
                    var cell = Rect("Day" + (i + 1), frame, Top, F(x, y), new Vector2(180, 220));
                    // Today: the popup's gold rim around the cell, slices halved to fit.
                    var today = Pic("Today", cell, Center, new Vector2(0, -4), new Vector2(214, 262), S("UI_Popup_Panel"));
                    today.pixelsPerUnitMultiplier = 2f;
                    today.raycastTarget = false;
                    Pic("Frame", cell, Center, Vector2.zero, new Vector2(180, 220), S("UI_InLevel_Tray")).raycastTarget = false;
                    var day = Text("Day", cell, new Vector2(0, 78), new Vector2(150, 44), "Day 1", 34, White);
                    var iconSize = i == icons.Length - 1 ? 124f : 100f;
                    var icon = Pic("Icon", cell, Center, new Vector2(0, 6), new Vector2(iconSize, iconSize), S(icons[i]));
                    icon.raycastTarget = false;
                    var amount = Text("Amount", cell, new Vector2(0, -74), new Vector2(150, 50), "50", 42, White);
                    var tick = Pic("Tick", cell, Center, new Vector2(46, -30), new Vector2(84, 84), S("UI_Icon_Check"));
                    tick.raycastTarget = false;

                    var element = days.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("Root").objectReferenceValue = cell;
                    element.FindPropertyRelative("Day").objectReferenceValue = day;
                    element.FindPropertyRelative("Amount").objectReferenceValue = amount;
                    element.FindPropertyRelative("Icon").objectReferenceValue = icon;
                    element.FindPropertyRelative("Today").objectReferenceValue = today.gameObject;
                    element.FindPropertyRelative("Tick").objectReferenceValue = tick.gameObject;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                var claim = WideButton(frame, "ClaimButton", 1500f, S("UI_Button_Wide_Go"), "daily.claim");
                root.transform.Find("Coins").SetAsLastSibling();
                Set(popup, "_content", content);
                Set(popup, "_claim", claim);
                Set(popup, "_claimLabel", claim.transform.Find("Label").GetComponent<LocalizedText>());
                Set(popup, "_close", close);
                Set(popup, "_coinTarget", root.transform.Find("Coins/Coin"));
                Set(popup, "_coin", S("UI_Icon_Coin"));
                PrefabUtility.SaveAsPrefabAsset(root, DailyRewardPopupPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<DailyRewardPopup>(DailyRewardPopupPath);
        }

        // ---------------------------------------------------------------- Orbit Rush

        private static readonly Color RushBackdrop = new(0.09f, 0.05f, 0.24f, 1f);

        private static OrbitRushScreen BuildOrbitRushScreen()
        {
            var layout = AssetDatabase.LoadAssetAtPath<OrbitRushMapLayout>(OrbitRushLayoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<OrbitRushMapLayout>();
                AssetDatabase.CreateAsset(layout, OrbitRushLayoutPath);
            }

            // Its own coin pill sits in the header row, so the shared one is left out.
            var root = PopupRoot("OrbitRushScreen", RushBackdrop, coins: false);
            try
            {
                // The map fills the safe area's width, overflowing mostly downwards so the treasure clears the header.
                var content = Span("Content", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                content.offsetMin = new Vector2(0, 400);
                content.offsetMax = new Vector2(0, -400);
                var map = Pic("Map", content, Center, Vector2.zero, OrbitRushMapLayout.MapSize, S("UI_LiveOps_OrbitRush_Map"),
                    new Vector2(0.5f, 0.75f));
                map.raycastTarget = false;
                map.preserveAspect = false;
                var fitter = map.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = OrbitRushMapLayout.MapSize.x / OrbitRushMapLayout.MapSize.y;

                var popup = root.AddComponent<OrbitRushScreen>();
                var so = new SerializedObject(popup);
                var gifts = so.FindProperty("_gifts");
                gifts.arraySize = OrbitRushService.Stages + 1;
                for (var i = 0; i <= OrbitRushService.Stages; i++)
                {
                    var grand = i == OrbitRushService.Stages;
                    // Flags, chests, then the treasure island, as on the map art.
                    var size = grand ? new Vector2(420, 300) : i is 1 or 2 ? new Vector2(240, 200) : new Vector2(150, 190);
                    var rect = Rect((grand ? "GrandPrize" : "Stage" + (i + 1)) + "Gift", map, OrbitRushMapLayout.Anchor(layout.Rewards[i]), Vector2.zero, size);
                    var hit = rect.gameObject.AddComponent<Image>();
                    hit.color = new Color(1f, 1f, 1f, 0f);
                    var button = rect.gameObject.AddComponent<Button>();
                    button.targetGraphic = hit;
                    button.transition = Selectable.Transition.None;
                    rect.gameObject.AddComponent<ButtonFeedback>();

                    var pill = Pic("Pill", rect, Center, new Vector2(0, size.y * 0.5f + 20f), new Vector2(190, 64), S("UI_Pill_Well_60"));
                    pill.raycastTarget = false;
                    Pic("Coin", pill, new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(62, 62), S("UI_Icon_Coin")).raycastTarget = false;
                    var amount = Text("Amount", pill, new Vector2(22, 2), new Vector2(110, 48), "0", 36, White);
                    var ready = Pic("Ready", rect, Center, new Vector2(0, -size.y * 0.5f + 10f), new Vector2(230, 84), S("UI_Button_Wide_Go"));
                    ready.raycastTarget = false;
                    Text("Label", ready, new Vector2(0, 5), new Vector2(190, 56), "rush.claim", 38, White, "rush.claim");
                    var done = Pic("Done", rect, Center, Vector2.zero, new Vector2(110, 110), S("UI_Icon_Check"));
                    done.raycastTarget = false;

                    var element = gifts.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("Root").objectReferenceValue = rect;
                    element.FindPropertyRelative("Ready").objectReferenceValue = ready.gameObject;
                    element.FindPropertyRelative("Done").objectReferenceValue = done.gameObject;
                    element.FindPropertyRelative("Amount").objectReferenceValue = amount;
                }

                // The player's marker stands on its tile with the arrow's tip.
                var marker = Rect("Marker", map, OrbitRushMapLayout.Anchor(layout.Stops[0]), Vector2.zero, new Vector2(170, 206), new Vector2(0.5f, 0f));
                Pic("Arrow", marker, Bottom, new Vector2(0, 19), new Vector2(58, 38), S("UI_LiveOps_ProfileArrow")).raycastTarget = false;
                var mask = Pic("Mask", marker, Top, new Vector2(0, -85), new Vector2(170, 170), S("UI_LiveOps_ProfileMask"));
                mask.raycastTarget = false;
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                Pic("Avatar", mask, Center, Vector2.zero, new Vector2(132, 132), S("UI_LiveOps_ProfileAvatar_Luno")).raycastTarget = false;
                Pic("Frame", marker, Top, new Vector2(0, -85), new Vector2(170, 170), S("UI_LiveOps_ProfileFrame")).raycastTarget = false;

                // Header: info, title and close on one line; coins, time left and stage in equal pills below.
                var header = Rect("Header", root.transform, Top, new Vector2(0, DesignTop - 400f), Reference, new Vector2(0.5f, 1f));
                var info = ButtonRect("InfoButton", header, Center, At(122, 236), new Vector2(118, 124), S("UI_InLevel_IconBase"));
                Text("Mark", info, new Vector2(0, 4), new Vector2(80, 80), "?", 64, White);
                Text("Title", header, At(585, 236), new Vector2(600, 96), "rush.title", 72, White, "rush.title");
                var close = ButtonRect("CloseButton", header, Center, At(1048, 236), new Vector2(124, 128), S("UI_Button_Close"));
                var coinPill = Pic("CoinPill", header, Center, At(215, 352), new Vector2(300, 84), S("UI_Pill_Well_96"));
                var coin = Pic("Coin", coinPill, new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(92, 92), S("UI_Icon_Coin"));
                coin.raycastTarget = false;
                var coins = Text("Amount", coinPill, new Vector2(30, 2), new Vector2(190, 60), "0", 44, White);
                coinPill.gameObject.AddComponent<EconomyHud>();
                Set(coinPill.GetComponent<EconomyHud>(), "_coins", coins);
                var timerPill = Pic("TimerPill", header, Center, At(585, 352), new Vector2(300, 84), S("UI_Pill_Well_96"));
                var timer = Text("Timer", timerPill, new Vector2(0, 2), new Vector2(250, 60), "2d 14h", 42, White);
                var stagePill = Pic("StagePill", header, Center, At(955, 352), new Vector2(300, 84), S("UI_Pill_Well_96"));
                var stage = Text("Stage", stagePill, new Vector2(0, 2), new Vector2(260, 60), "Stage 1/5", 40, White);

                // Bottom tray on the safe bottom: the room's racers (race view), a status line and the action button.
                var panel = Rect("Panel", root.transform, Bottom, new Vector2(0, 400f + 16f), new Vector2(1110, 880), new Vector2(0.5f, 0f));
                Sliced(panel.gameObject.AddComponent<Image>(), S("UI_InLevel_Tray"));
                var toggle = ButtonRect("ViewToggle", panel, Top, new Vector2(390, 44), new Vector2(250, 90), S("UI_Button_Wide_Purple"));
                Text("Label", toggle, new Vector2(0, 5), new Vector2(210, 60), "rush.view_map", 36, White, "rush.view_map");
                var status = Text("Status", panel, Vector2.zero, new Vector2(980, 100), "Status", 40, White);
                status.textWrappingMode = TextWrappingModes.Normal;
                Anchor(status.rectTransform, Bottom, new Vector2(0, 250));
                var action = ButtonRect("ActionButton", panel, Bottom, new Vector2(0, 100), new Vector2(560, 140), S("UI_Button_Wide_Go"));
                Text("Label", action, new Vector2(0, 8), new Vector2(460, 90), "rush.play", 56, White, "rush.play");

                var race = Span("Race", panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var gear = Text("Gear", race, Vector2.zero, new Vector2(400, 56), "Speed x1", 42, new Color(1f, 0.85f, 0.35f));
                gear.alignment = TextAlignmentOptions.MidlineLeft;
                Anchor(gear.rectTransform, Top, new Vector2(-300, -62));
                var rows = so.FindProperty("_rows");
                rows.arraySize = OrbitRushService.Capacity;
                for (var i = 0; i < OrbitRushService.Capacity; i++)
                {
                    var row = Pic("Row" + (i + 1), race, Top, new Vector2(0, -150 - i * 88), new Vector2(1030, 80), S("UI_Pill_Well_96"));
                    row.raycastTarget = false;
                    var rank = Text("Rank", row, new Vector2(-470, 2), new Vector2(60, 60), "1", 44, White);
                    var avatar = Pic("Avatar", row, Center, new Vector2(-390, 0), new Vector2(70, 72), S("UI_InLevel_IconBase"));
                    avatar.raycastTarget = false;
                    var initial = Text("Initial", avatar, new Vector2(0, 3), new Vector2(60, 56), "N", 40, White);
                    var face = Pic("Face", avatar, Center, new Vector2(0, 2), new Vector2(58, 58), S("UI_LiveOps_ProfileAvatar_Luno"));
                    face.raycastTarget = false;
                    var name = Text("Name", row, new Vector2(-205, 2), new Vector2(250, 56), "Name", 38, White);
                    name.alignment = TextAlignmentOptions.MidlineLeft;
                    var track = Pic("Track", row, Center, new Vector2(175, 0), new Vector2(420, 46), S("UI_Pill_Well_60"));
                    track.raycastTarget = false;
                    var fill = Pic("Fill", track, new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(400, 34), S("UI_Button_Wide_Go"), new Vector2(0, 0.5f));
                    fill.raycastTarget = false;
                    var score = Text("Score", row, new Vector2(455, 2), new Vector2(130, 56), "0/5", 38, White);

                    var element = rows.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("Root").objectReferenceValue = row.gameObject;
                    element.FindPropertyRelative("Background").objectReferenceValue = row;
                    element.FindPropertyRelative("Rank").objectReferenceValue = rank;
                    element.FindPropertyRelative("Initial").objectReferenceValue = initial;
                    element.FindPropertyRelative("PlayerFace").objectReferenceValue = face.gameObject;
                    element.FindPropertyRelative("Name").objectReferenceValue = name;
                    element.FindPropertyRelative("Fill").objectReferenceValue = fill.rectTransform;
                    element.FindPropertyRelative("Score").objectReferenceValue = score;
                }
                var medals = so.FindProperty("_medals");
                medals.arraySize = 3;
                medals.GetArrayElementAtIndex(0).colorValue = new Color(1f, 0.82f, 0.25f);
                medals.GetArrayElementAtIndex(1).colorValue = new Color(0.82f, 0.88f, 0.98f);
                medals.GetArrayElementAtIndex(2).colorValue = new Color(0.95f, 0.6f, 0.35f);
                so.ApplyModifiedPropertiesWithoutUndo();

                // Rules over everything; a tap anywhere closes them.
                var infoPanel = Span("InfoPanel", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var infoDim = infoPanel.gameObject.AddComponent<Image>();
                infoDim.color = new Color(0f, 0f, 0f, 0.8f);
                var infoClose = infoPanel.gameObject.AddComponent<Button>();
                infoClose.targetGraphic = infoDim;
                infoClose.transition = Selectable.Transition.None;
                var card = Pic("Card", infoPanel, Center, Vector2.zero, new Vector2(900, 820), S("UI_Popup_Panel"));
                card.raycastTarget = false;
                Text("Title", card, new Vector2(0, 320), new Vector2(760, 90), "rush.info_title", 60, White, "rush.info_title");
                var rules = Text("Body", card, new Vector2(0, -40), new Vector2(760, 560), "rush.info_body", 40, Ink, "rush.info_body");
                rules.textWrappingMode = TextWrappingModes.Normal;
                rules.alignment = TextAlignmentOptions.TopLeft;
                infoPanel.gameObject.SetActive(false);

                Set(popup, "_content", content);
                Set(popup, "_layout", layout);
                Set(popup, "_marker", marker);
                Set(popup, "_timer", timer);
                Set(popup, "_stage", stage);
                Set(popup, "_close", close.GetComponent<Button>());
                Set(popup, "_info", info.GetComponent<Button>());
                Set(popup, "_infoPanel", infoPanel.gameObject);
                Set(popup, "_infoClose", infoClose);
                Set(popup, "_panel", panel);
                Set(popup, "_race", race.gameObject);
                Set(popup, "_gear", gear);
                Set(popup, "_status", status);
                Set(popup, "_action", action.GetComponent<Button>());
                Set(popup, "_actionLabel", action.Find("Label").GetComponent<LocalizedText>());
                Set(popup, "_toggle", toggle.GetComponent<Button>());
                Set(popup, "_toggleLabel", toggle.Find("Label").GetComponent<LocalizedText>());
                Set(popup, "_coinTarget", coin.rectTransform);
                Set(popup, "_coin", S("UI_Icon_Coin"));
                PrefabUtility.SaveAsPrefabAsset(root, OrbitRushScreenPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<OrbitRushScreen>(OrbitRushScreenPath);
        }

        // ---------------------------------------------------------------- Home entry points

        // Reuses the entry buttons BuildHome made (found by name), else adds side buttons.
        private static void BuildLiveOpsHome(Scene scene, DailyRewardPopup daily, OrbitRushScreen orbitRush, MessagePopup message)
        {
            var canvas = Root(scene, "Home Canvas").transform;
            var area = canvas.Find("Content/SafeArea") ?? canvas.Find("SafeArea");
            var top = area.Find("TopStage/Top") ?? area.Find("TopStage");

            var dailyButton = Find<Button>(canvas, "DailyButton") ??
                              SideButton((RectTransform)top, "DailyButton", At(90, 880), S("UI_Icon_Daily"), "daily.button");
            var eventButton = Find<Button>(canvas, "EventButton") ??
                              SideButton((RectTransform)top, "EventButton", At(1080, 880), S("UI_Icon_Challenge"), "rush.button");
            // Every event entry opens the map: the side button and the bar's event and Orbit Rush tabs.
            var entries = new System.Collections.Generic.List<Button> { eventButton };
            foreach (var name in new[] { "OrbitRushTab", "EventTab" })
            {
                var entry = Find<Button>(canvas, name);
                if (entry != null) entries.Add(entry);
            }

            var timer = Find<TMP_Text>(eventButton.transform, "Timer");
            if (timer == null)
            {
                var pill = Pic("TimerPill", eventButton.transform, Center, new Vector2(0, 86), new Vector2(150, 56), S("UI_Pill_Well_60"));
                pill.raycastTarget = false;
                timer = Text("Timer", pill, new Vector2(0, 2), new Vector2(130, 44), "2d 14h", 30, White);
            }
            // The race count takes the name label's place under the icon.
            var label = eventButton.transform.Find("Label");
            var countPill = Pic("CountPill", eventButton.transform, Center, new Vector2(0, label != null ? ((RectTransform)label).anchoredPosition.y : -100f),
                new Vector2(150, 54), S("UI_Pill_Well_60"));
            countPill.raycastTarget = false;
            var count = Text("Count", countPill, new Vector2(0, 2), new Vector2(130, 44), "0/5", 32, White);
            countPill.gameObject.SetActive(false);

            var go = new GameObject("LiveOpsHome", typeof(RectTransform));
            go.transform.SetParent(area, false);
            var home = go.AddComponent<LiveOpsHome>();
            Set(home, "_daily", dailyButton);
            Set(home, "_dailyBadge", Badge(dailyButton));
            SetArray(home, "_event", entries.ConvertAll(b => (Object)b).ToArray());
            SetArray(home, "_eventBadges", entries.ConvertAll(b => (Object)Badge(b)).ToArray());
            Set(home, "_eventTimer", timer);
            Set(home, "_eventLabel", label != null ? label.gameObject : null);
            Set(home, "_eventCount", countPill.gameObject);
            Set(home, "_eventCountText", count);
            Set(home, "_play", Find<Button>(canvas, "PlayButton"));
            Set(home, "_dailyPopup", daily);
            Set(home, "_orbitRush", orbitRush);
            Set(home, "_messagePopup", message);
            Set(home, "_collectable", S("UI_Icon_OrbitRush"));
        }

        // The button's "Badge" child, or a new "!" badge on its top-right corner.
        private static GameObject Badge(Button button)
        {
            var existing = button.transform.Find("Badge");
            if (existing != null) return existing.gameObject;
            var size = ((RectTransform)button.transform).sizeDelta;
            var badge = Pic("Badge", button.transform, Center, new Vector2(size.x * 0.38f, size.y * 0.38f), new Vector2(62, 64), S("UI_Badge_Count"));
            badge.raycastTarget = false;
            Text("Mark", badge, new Vector2(0, 3), new Vector2(40, 46), "!", 40, White);
            badge.gameObject.SetActive(false);
            return badge.gameObject;
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            foreach (var component in root.GetComponentsInChildren<T>(true))
                if (component.name == name) return component;
            return null;
        }
    }
}
