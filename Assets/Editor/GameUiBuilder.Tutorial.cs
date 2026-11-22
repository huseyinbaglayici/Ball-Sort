using Project.Contexts.Game;
using Project.Contexts.Game.Levels;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Editor
{
    // First-time teaching: the hand, line and marks over the board, and the new item or booster card.
    public static partial class GameUiBuilder
    {
        private const string IntroPopupPath = "Assets/Prefabs/Popups/IntroPopup.prefab";
        // Fingertip on the hand art.
        private static readonly Vector2 HandTip = new(0.19f, 0.95f);

        private static Sprite Art(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new System.InvalidOperationException($"[GameUI] Sprite '{path}' is missing.");
            return sprite;
        }

        private static void BuildTutorial(Scene scene, IntroPopup intro)
        {
            var canvas = Root(scene, "HUD Canvas").transform;
            var layer = Span("Tutorial", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            layer.SetAsLastSibling();

            var line = Text("Line", layer, Vector2.zero, new Vector2(1050, 100), "tutorial.pick", 60, White, "tutorial.pick");
            var yes = Pic("Yes", layer, Center, Vector2.zero, new Vector2(110, 110), S("UI_Icon_Check"));
            var no = Pic("No", layer, Center, Vector2.zero, new Vector2(110, 114), S("UI_Button_Close"));
            var hand = Pic("Hand", layer, Center, Vector2.zero, new Vector2(196, 290), S("UI_Tutorial_Hand"));
            hand.rectTransform.pivot = HandTip;
            foreach (var image in new[] { yes, no, hand }) image.raycastTarget = false;

            var tutorial = layer.gameObject.AddComponent<LevelTutorial>();
            var game = Root(scene, "GameController").GetComponent<GameController>();
            Set(tutorial, "_board", new SerializedObject(game).FindProperty("_board").objectReferenceValue);
            Set(tutorial, "_catalog", AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogPath));
            Set(tutorial, "_finger", hand.rectTransform);
            Set(tutorial, "_line", line.rectTransform);
            Set(tutorial, "_lineText", line.GetComponent<Project.Common.UI.LocalizedText>());
            Set(tutorial, "_yes", yes.rectTransform);
            Set(tutorial, "_no", no.rectTransform);
            Set(tutorial, "_introPopup", intro);
        }

        private static IntroPopup BuildIntroPopup()
        {
            var root = PopupRoot("IntroPopup", new Color(0f, 0f, 0f, 0.85f));
            try
            {
                var (frame, _, ribbonTitle) = Panel(root, 1030f, "intro.new_item");
                var name = Text("Name", frame, F(585, 950), new Vector2(720, 90), "intro.hidden", 62, Ink, "intro.hidden");
                Anchor(name.rectTransform, Top, F(585, 950));
                var well = Well(frame, 1225f, new Vector2(720, 500));
                var art = Rect("Art", well, Center, new Vector2(0, 70), new Vector2(700, 260));
                IntroArt(art);
                var body = Text("Body", well, Vector2.zero, new Vector2(660, 150), "help.hidden", 42, White, "help.hidden");
                body.textWrappingMode = TextWrappingModes.Normal;
                body.alignment = TextAlignmentOptions.Bottom;
                body.rectTransform.pivot = new Vector2(0.5f, 0f);
                Anchor(body.rectTransform, Bottom, new Vector2(0, 22));
                var button = WideButton(frame, "Button", 1600f, S("UI_Button_Wide_Go"), "common.continue");

                // A tap anywhere closes it too.
                var dim = root.GetComponent<Image>();
                var tapAnywhere = root.AddComponent<Button>();
                tapAnywhere.targetGraphic = dim;
                tapAnywhere.transition = Selectable.Transition.None;

                var popup = root.AddComponent<IntroPopup>();
                Set(popup, "_content", (RectTransform)frame.parent);
                Set(popup, "_ribbon", ribbonTitle.GetComponent<Project.Common.UI.LocalizedText>());
                Set(popup, "_title", name.GetComponent<Project.Common.UI.LocalizedText>());
                Set(popup, "_body", body.GetComponent<Project.Common.UI.LocalizedText>());
                Set(popup, "_art", art);
                SetArray(popup, "_close", new Object[] { button, tapAnywhere });
                PrefabUtility.SaveAsPrefabAsset(root, IntroPopupPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<IntroPopup>(IntroPopupPath);
        }

        // One child per card, named as IntroArgs.Art.
        private static void IntroArt(RectTransform art)
        {
            Image Layer(RectTransform parent, string name, Sprite sprite, Vector2 at, Vector2 size)
            {
                var image = Pic(name, parent, Center, at, size, sprite);
                image.raycastTarget = false;
                return image;
            }

            RectTransform Card(string name) => Rect(name, art, Center, Vector2.zero, new Vector2(700, 260));

            var tube = new Vector2(98, 260);
            Layer(Card("Hidden"), "Shell", Art("Assets/Art/Sprites/Obstacles/Orb_Hidden_Shell.png"), Vector2.zero, new Vector2(220, 220));
            Layer(Card("Bomb"), "Bomb", Art("Assets/Art/Sprites/Obstacles/Orb_Bomb_Base.png"), Vector2.zero, new Vector2(230, 230));
            Layer(Card("Frozen"), "Ice", Art("Assets/Art/Sprites/Obstacles/Tube_Frozen.png"), Vector2.zero, tube);

            var shutter = Card("Shutter");
            foreach (var part in new[] { "Shutter_Curtain", "Shutter_Frame", "Shutter_Housing", "Padlock_Base" })
                Layer(shutter, part, Art($"Assets/Art/Sprites/Obstacles/{part}.png"), new Vector2(-60, 0), tube);
            Layer(shutter, "Key", Art("Assets/Art/Sprites/Obstacles/Orb_Key.png"), new Vector2(90, -30), new Vector2(170, 170));

            var tower = Card("Tower");
            foreach (var part in new[] { "Tube_Back_Glass", "Tube_End_Piece" })
                Layer(tower, part, Art($"Assets/Art/Sprites/Tube/{part}.png"), Vector2.zero, tube);
            var colors = new[] { "Violet", "Yellow", "Violet", "Yellow", "Violet" };
            for (var i = 0; i < colors.Length; i++)
                Layer(tower, "Orb" + i, Art($"Assets/Art/Sprites/Orb/Orb_Core_{colors[i]}.png"), new Vector2(0, -74 + i * 37), new Vector2(64, 64));
            foreach (var part in new[] { "Tube_Empty_Cap", "Tube_RimFront" })
                Layer(tower, part, Art($"Assets/Art/Sprites/Tube/{part}.png"), Vector2.zero, tube);

            foreach (var (name, icon) in new[] { ("Undo", "UI_Icon_Undo"), ("ExtraTube", "UI_Icon_ExtraTube"), ("FreeMove", "UI_Icon_FreeMove") })
            {
                var card = Card(name);
                Layer(card, "Base", S("UI_InLevel_BoosterBase_Up"), Vector2.zero, new Vector2(230, 228));
                Layer(card, "Icon", S(icon), new Vector2(0, 8), new Vector2(150, 150));
            }
        }
    }
}
