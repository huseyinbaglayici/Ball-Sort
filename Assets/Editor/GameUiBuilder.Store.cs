using Project.Common.UI;
using Project.Contexts.Store;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Editor
{
    // Store popups: the shop page, the No Ads offer and the out-of-lives panel.
    public static partial class GameUiBuilder
    {
        private const string ShopPopupPath = "Assets/Prefabs/Popups/ShopPopup.prefab";
        private const string NoAdsPopupPath = "Assets/Prefabs/Popups/NoAdsPopup.prefab";
        private const string LivesPopupPath = "Assets/Prefabs/Popups/LivesPopup.prefab";
        private static readonly Color Gold = new(1f, 0.85f, 0.35f);

        // Full-screen page: title row, a demo note, then a scrolling list of cards built at runtime.
        private static ShopPopup BuildShopPopup()
        {
            var root = PopupRoot("ShopPopup", new Color(0.212f, 0.125f, 0.588f, 1f));
            try
            {
                var header = Rect("Header", root.transform, Top, new Vector2(0, DesignTop - 400f), Reference, new Vector2(0.5f, 1f));
                // Left of the coin counter, which takes the Home bar's spot.
                var pageTitle = Text("Title", header, At(250, 228), new Vector2(360, 100), "store.title", 72, White, "store.title");
                pageTitle.alignment = TextAlignmentOptions.MidlineLeft;
                var close = ButtonRect("CloseButton", header, Center, At(1048, 228), new Vector2(124, 128), S("UI_Button_Close"));
                var demo = Text("Demo", header, At(585, 330), new Vector2(1000, 50), "store.demo", 34, Gold, "store.demo");
                var status = Text("Status", header, At(585, 1300), new Vector2(900, 110), "store.bought", 80, White);

                // From under the title row to the safe bottom; the root reaches 400 past both edges.
                var scrollRect = Span("Scroll", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                scrollRect.offsetMin = new Vector2(50, 420);
                scrollRect.offsetMax = new Vector2(-50, -(400 + 400 - DesignTop));
                var viewport = Span("Viewport", scrollRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                viewport.gameObject.AddComponent<RectMask2D>();
                // Catches drags between the cards.
                viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
                var list = Span("List", viewport, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
                list.pivot = new Vector2(0.5f, 1f);
                var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 28f;
                layout.padding = new RectOffset(0, 0, 10, 80);
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport;
                scroll.content = list;
                scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Elastic;

                var sectionHeader = Text("SectionHeader", list, Vector2.zero, new Vector2(1000, 100), "Offers", 60, White);
                sectionHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 100f;

                var card = Pic("Card", list, Center, Vector2.zero, new Vector2(1000, 300), S("UI_InLevel_Tray"));
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 300f;
                Pic("Icon", card, Center, new Vector2(-360, 0), new Vector2(200, 200), S("UI_Icon_Coin")).raycastTarget = false;
                var title = Text("Title", card, new Vector2(-30, 70), new Vector2(500, 80), "Title", 54, White);
                title.alignment = TextAlignmentOptions.MidlineLeft;
                var contents = Text("Contents", card, new Vector2(-45, -20), new Vector2(470, 110), "Contents", 34, White);
                contents.alignment = TextAlignmentOptions.MidlineLeft;
                contents.textWrappingMode = TextWrappingModes.Normal;
                var timer = Text("Timer", card, new Vector2(-30, -105), new Vector2(500, 50), "Timer", 32, Gold);
                timer.alignment = TextAlignmentOptions.MidlineLeft;
                var buy = ButtonRect("Buy", card, Center, new Vector2(340, 0), new Vector2(260, 130), S("UI_Button_Wide_Go"));
                Text("Label", buy, new Vector2(0, 6), new Vector2(220, 80), "$0.99", 48, White);

                var popup = root.AddComponent<ShopPopup>();
                Set(popup, "_content", header);
                Set(popup, "_close", close.GetComponent<Button>());
                Set(popup, "_scroll", scroll);
                Set(popup, "_list", list);
                Set(popup, "_headerTemplate", sectionHeader);
                Set(popup, "_cardTemplate", card.rectTransform);
                Set(popup, "_demo", demo);
                Set(popup, "_status", status);
                Set(popup, "_beginnerIcon", S("UI_Icon_Reward_CoinChest_CC"));
                Set(popup, "_bundleIcon", S("UI_Icon_Reward_CoinSack_CC"));
                Set(popup, "_noAdsIcon", S("UI_Icon_NoAds"));
                SetArray(popup, "_coinIcons", new Object[]
                {
                    S("UI_Icon_Coin"), S("UI_Reward_CoinPile_CC"), S("UI_Icon_Reward_CoinSack_CC"), S("UI_Icon_Reward_CoinChest_CC")
                });
                PrefabUtility.SaveAsPrefabAsset(root, ShopPopupPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<ShopPopup>(ShopPopupPath);
        }

        private static NoAdsPopup BuildNoAdsPopup()
        {
            var root = PopupRoot("NoAdsPopup", new Color(0f, 0f, 0f, 0.85f));
            try
            {
                var (frame, _, _) = Panel(root, 1200f, "store.no_ads");
                var close = CloseButton(frame);
                var well = Well(frame, 1170f, new Vector2(720, 440));
                Pic("Icon", well, Center, new Vector2(0, 80), new Vector2(200, 200), S("UI_Icon_NoAds")).raycastTarget = false;
                var body = Text("Body", well, Vector2.zero, new Vector2(660, 150), "store.no_ads_body", 38, White, "store.no_ads_body");
                body.textWrappingMode = TextWrappingModes.Normal;
                body.rectTransform.pivot = new Vector2(0.5f, 0f);
                Anchor(body.rectTransform, Bottom, new Vector2(0, 24));
                var buy = WideButton(frame, "BuyButton", 1510f, S("UI_Button_Wide_Go"), null);
                var restore = WideButton(frame, "RestoreButton", 1680f, S("UI_Button_Wide_Purple"), "store.restore", 50f);
                var status = Text("Status", frame, Vector2.zero, new Vector2(700, 50), "", 34, Ink);
                Anchor(status.rectTransform, Bottom, new Vector2(0, 120));
                var demo = Text("Demo", frame, Vector2.zero, new Vector2(700, 50), "store.demo_short", 32, Ink, "store.demo_short");
                Anchor(demo.rectTransform, Bottom, new Vector2(0, 70));

                var popup = root.AddComponent<NoAdsPopup>();
                Set(popup, "_content", (RectTransform)frame.parent);
                Set(popup, "_close", close);
                Set(popup, "_buy", buy);
                Set(popup, "_price", buy.transform.Find("Label").GetComponent<TMP_Text>());
                Set(popup, "_restore", restore);
                Set(popup, "_demo", demo);
                Set(popup, "_status", status);
                PrefabUtility.SaveAsPrefabAsset(root, NoAdsPopupPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<NoAdsPopup>(NoAdsPopupPath);
        }

        // Out of lives: heart and timer in the well, the coin refill as the main button, the rewarded life under it.
        private static LivesPopup BuildLivesPopup(ShopPopup shop)
        {
            var root = PopupRoot("LivesPopup", new Color(0f, 0f, 0f, 0.85f));
            try
            {
                var (frame, _, _) = Panel(root, 1100f, "lives.out_title");
                var close = CloseButton(frame);
                var well = Well(frame, 1170f, new Vector2(720, 440));
                Pic("Heart", well, Center, new Vector2(0, 62), new Vector2(210, 210), S("UI_Icon_HeartBroken")).raycastTarget = false;
                var timer = Text("Timer", well, Vector2.zero, new Vector2(660, 110), "", 46, White);
                timer.rectTransform.pivot = new Vector2(0.5f, 0f);
                Anchor(timer.rectTransform, Bottom, new Vector2(0, 30));
                var refill = WideButton(frame, "RefillButton", 1510f, S("UI_Button_Wide_Go"), "lives.refill_all");
                // The price chip stays on, the label beside it.
                var price = refill.transform.Find("Price");
                price.gameObject.SetActive(true);
                var label = (RectTransform)refill.transform.Find("Label");
                label.anchoredPosition += new Vector2(80, 0);
                var ad = WideButton(frame, "AdButton", 1680f, S("UI_Button_Wide_Purple"), "lives.ad", 50f);

                var popup = root.AddComponent<LivesPopup>();
                Set(popup, "_content", (RectTransform)frame.parent);
                Set(popup, "_close", close);
                Set(popup, "_timer", timer);
                Set(popup, "_refill", refill);
                Set(popup, "_refillLabel", label.GetComponent<LocalizedText>());
                Set(popup, "_refillPrice", price.gameObject);
                Set(popup, "_ad", ad);
                Set(popup, "_shopPopup", shop);
                PrefabUtility.SaveAsPrefabAsset(root, LivesPopupPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<LivesPopup>(LivesPopupPath);
        }
    }
}
