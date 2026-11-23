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
    // Shared layout pieces and the primitives every screen is built from.
    public static partial class GameUiBuilder
    {
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
            var coin = Pic("Coin", pill, Center, At(93, 254), new Vector2(93, 93), S("UI_Icon_Coin"));
            var hud = pill.gameObject.AddComponent<EconomyHud>();
            Set(hud, "_coins", amount);
            // On Home the counter matches the Home bar's own spot, so it never covers the lives pill.
            var spot = pill.gameObject.AddComponent<PopupCoinSpot>();
            Set(spot, "_bar", bar.rectTransform);
            Set(spot, "_coin", coin.rectTransform);
            SetRect(spot, "_levelBar", At(201, 262), new Vector2(284, 100));
            SetRect(spot, "_levelCoin", At(93, 254), new Vector2(93, 93));
            SetRect(spot, "_homeBar", At(806, 228), new Vector2(292, 96));
            SetRect(spot, "_homeCoin", At(676, 226), new Vector2(118, 118));
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

        // Wide 150-high button with hidden price chip, count badge and "-1 life" line; null key = label set by code.
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

        // Home side button: 150x157 base, 120 icon, 32 px label 92 below the centre.
        private static Button SideButton(RectTransform parent, string name, Vector2 position, Sprite icon, string key)
        {
            var rect = ButtonRect(name, parent, Center, position, new Vector2(150, 157), S("UI_InLevel_IconBase"));
            Pic("Icon", rect, Center, new Vector2(0, 4), new Vector2(120, 120), icon).raycastTarget = false;
            Text("Label", rect, new Vector2(0, -92), new Vector2(200, 46), key, 32, White, key);
            return rect.GetComponent<Button>();
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

        // A Rect field set from an anchored position and a size.
        private static void SetRect(Object target, string field, Vector2 position, Vector2 size)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null) throw new System.InvalidOperationException($"[GameUI] {target.GetType().Name}.{field} not found.");
            property.rectValue = new Rect(position, size);
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
