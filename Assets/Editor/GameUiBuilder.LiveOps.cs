using Project.Common.UI;
using Project.Common.UI.PopUps;
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

        private static OrbitRushScreen BuildOrbitRushScreen()
        {
            var layout = AssetDatabase.LoadAssetAtPath<OrbitRushMapLayout>(OrbitRushLayoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<OrbitRushMapLayout>();
                AssetDatabase.CreateAsset(layout, OrbitRushLayoutPath);
            }

            var root = PopupRoot("OrbitRushScreen", new Color(0.09f, 0.05f, 0.24f, 1f));
            try
            {
                // The safe area; the map fills its width, overflowing mostly downwards so the treasure clears the header.
                var content = Span("Content", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                content.offsetMin = new Vector2(0, 400);
                content.offsetMax = new Vector2(0, -400);
                content.SetSiblingIndex(0);
                var map = Pic("Map", content, Center, Vector2.zero, OrbitRushMapLayout.MapSize, S("UI_LiveOps_OrbitRush_Map"),
                    new Vector2(0.5f, 0.75f));
                map.raycastTarget = false;
                map.preserveAspect = false;
                var fitter = map.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = OrbitRushMapLayout.MapSize.x / OrbitRushMapLayout.MapSize.y;

                var popup = root.AddComponent<OrbitRushScreen>();
                var so = new SerializedObject(popup);
                var rewards = so.FindProperty("_rewards");
                rewards.arraySize = OrbitRushService.Milestones.Length;
                for (var i = 0; i < OrbitRushService.Milestones.Length; i++)
                {
                    var milestone = OrbitRushService.Milestones[i];
                    var size = milestone.Kind switch
                    {
                        MilestoneKind.Treasure => new Vector2(420, 300),
                        MilestoneKind.Chest => new Vector2(240, 200),
                        _ => new Vector2(150, 190)
                    };
                    var rect = Rect(milestone.Kind + "Reward" + i, map, OrbitRushMapLayout.Anchor(layout.Rewards[i]), Vector2.zero, size);
                    var hit = rect.gameObject.AddComponent<Image>();
                    hit.color = new Color(1f, 1f, 1f, 0f);
                    var button = rect.gameObject.AddComponent<Button>();
                    button.targetGraphic = hit;
                    button.transition = Selectable.Transition.None;
                    rect.gameObject.AddComponent<ButtonFeedback>();

                    var pillY = size.y * 0.5f + 20f;
                    var pill = Pic("Pill", rect, Center, new Vector2(0, pillY), new Vector2(190, 64), S("UI_Pill_Well_60"));
                    pill.raycastTarget = false;
                    Pic("Coin", pill, new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(62, 62), S("UI_Icon_Coin")).raycastTarget = false;
                    var amount = Text("Amount", pill, new Vector2(22, 2), new Vector2(110, 48), "0", 36, White);
                    var ready = Pic("Ready", rect, Center, new Vector2(0, -size.y * 0.5f + 10f), new Vector2(230, 84), S("UI_Button_Wide_Go"));
                    ready.raycastTarget = false;
                    Text("Label", ready, new Vector2(0, 5), new Vector2(190, 56), "rush.claim", 38, White, "rush.claim");
                    var done = Pic("Done", rect, Center, Vector2.zero, new Vector2(110, 110), S("UI_Icon_Check"));
                    done.raycastTarget = false;

                    var element = rewards.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("Button").objectReferenceValue = button;
                    element.FindPropertyRelative("Ready").objectReferenceValue = ready.gameObject;
                    element.FindPropertyRelative("Done").objectReferenceValue = done.gameObject;
                    element.FindPropertyRelative("Amount").objectReferenceValue = amount;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                // The player's marker stands on its tile with the arrow's tip.
                var marker = Rect("Marker", map, OrbitRushMapLayout.Anchor(layout.Stops[0]), Vector2.zero, new Vector2(170, 206), new Vector2(0.5f, 0f));
                Pic("Arrow", marker, Bottom, new Vector2(0, 19), new Vector2(58, 38), S("UI_LiveOps_ProfileArrow")).raycastTarget = false;
                var mask = Pic("Mask", marker, Top, new Vector2(0, -85), new Vector2(170, 170), S("UI_LiveOps_ProfileMask"));
                mask.raycastTarget = false;
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                Pic("Avatar", mask, Center, Vector2.zero, new Vector2(132, 132), S("UI_LiveOps_ProfileAvatar_Luno")).raycastTarget = false;
                Pic("Frame", marker, Top, new Vector2(0, -85), new Vector2(170, 170), S("UI_LiveOps_ProfileFrame")).raycastTarget = false;

                // Title, countdown and close hang from the safe top; the coin counter stays above it all.
                var header = Rect("Header", root.transform, Top, new Vector2(0, DesignTop - 400f), Reference, new Vector2(0.5f, 1f));
                Text("Title", header, At(585, 236), new Vector2(440, 96), "rush.title", 72, White, "rush.title");
                // Time left and steps won side by side under the title.
                var timerPill = Pic("TimerPill", header, Center, At(440, 344), new Vector2(300, 84), S("UI_Pill_Well_96"));
                var timer = Text("Timer", timerPill, new Vector2(0, 2), new Vector2(250, 60), "2d 14h", 42, White);
                var progressPill = Pic("ProgressPill", header, Center, At(770, 344), new Vector2(240, 84), S("UI_Pill_Well_96"));
                var progress = Text("Progress", progressPill, new Vector2(0, 2), new Vector2(200, 60), "0 / 14", 42, White);
                var close = ButtonRect("CloseButton", header, Center, At(1048, 236), new Vector2(124, 128), S("UI_Button_Close"));
                root.transform.Find("Coins").SetAsLastSibling();

                Set(popup, "_content", content);
                Set(popup, "_layout", layout);
                Set(popup, "_marker", marker);
                Set(popup, "_timer", timer);
                Set(popup, "_progress", progress);
                Set(popup, "_close", close.GetComponent<Button>());
                Set(popup, "_coinTarget", root.transform.Find("Coins/Coin"));
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
        private static void BuildLiveOpsHome(Scene scene, DailyRewardPopup daily, OrbitRushScreen orbitRush)
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

            var go = new GameObject("LiveOpsHome", typeof(RectTransform));
            go.transform.SetParent(area, false);
            var home = go.AddComponent<LiveOpsHome>();
            Set(home, "_daily", dailyButton);
            Set(home, "_dailyBadge", Badge(dailyButton));
            SetArray(home, "_event", entries.ConvertAll(b => (Object)b).ToArray());
            SetArray(home, "_eventBadges", entries.ConvertAll(b => (Object)Badge(b)).ToArray());
            Set(home, "_eventTimer", timer);
            Set(home, "_dailyPopup", daily);
            Set(home, "_orbitRush", orbitRush);
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
