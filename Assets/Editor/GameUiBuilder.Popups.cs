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
    // Popup prefabs: message, fail panel and settings with its pages.
    public static partial class GameUiBuilder
    {
        // ---------------------------------------------------------------- Popups

        // Shared popup frame: 880 wide, panel top at 760 on the design screen.
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
                var (frame, ribbon, _) = Panel(root, 1030f, "result.failed");
                ribbon.sprite = S("UI_Popup_Ribbon_Lose");
                var close = CloseButton(frame);
                // Content box: picture on top, the reason under it.
                var well = Well(frame, 1170f, new Vector2(720, 440));
                Pic("LossPicture", well, Center, new Vector2(0, 56), new Vector2(210, 210), S("UI_Icon_HeartBroken"));
                var details = Text("Details", well, Vector2.zero, new Vector2(660, 130), "", 42, White);
                details.textWrappingMode = TextWrappingModes.Normal;
                // Sits on the box's bottom edge, under the picture.
                details.alignment = TextAlignmentOptions.Bottom;
                details.rectTransform.pivot = new Vector2(0.5f, 0f);
                Anchor(details.rectTransform, Bottom, new Vector2(0, 24));

                var primary = WideButton(frame, "PrimaryButton", 1510f, S("UI_Button_Wide_Go"), null);
                // Extra choices are purple buttons stacked under the main one.
                var option = WideButton(frame, "OptionButton", 1680f, S("UI_Button_Wide_Purple"), null, 50f);

                // Hold-to-peek caption: a frameless breathing line that eases the fail panel away while pressed.
                var panelGroup = frame.parent.gameObject.AddComponent<CanvasGroup>();
                // Dark ink: white would vanish on the panel's light floor.
                var peek = Text("HoldToPeek", frame, Vector2.zero, new Vector2(600, 64), "result.peek", 38, Ink, "result.peek");
                peek.raycastTarget = true;
                peek.rectTransform.pivot = new Vector2(0.5f, 0f);
                Anchor(peek.rectTransform, Bottom, new Vector2(0, 54));
                // On the root, so pressing the dim outside the panel peeks as well.
                var hold = root.AddComponent<HoldToPeek>();
                Set(hold, "_panel", panelGroup);
                Set(hold, "_dim", root.GetComponent<Image>());
                Set(hold, "_caption", peek);

                var popup = root.AddComponent<ResultPopup>();
                Set(popup, "_peek", peek.gameObject);
                Set(popup, "_content", (RectTransform)frame.parent);
                Set(popup, "_frame", frame);
                Set(popup, "_details", details);
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

        private const string AppLinksPath = "Assets/Configs/AppLinks.asset";
        private const string LevelCatalogPath = "Assets/Configs/LevelCatalog.asset";

        // Full-screen settings page: switches, the rows well, a language page and a help page; version footer below.
        private static SettingsPopup BuildSettingsPopup()
        {
            var links = AssetDatabase.LoadAssetAtPath<Project.Core.Settings.AppLinks>(AppLinksPath);
            if (links == null)
            {
                links = ScriptableObject.CreateInstance<Project.Core.Settings.AppLinks>();
                AssetDatabase.CreateAsset(links, AppLinksPath);
            }
            var buildInfo = BuildInfoWriter.Write(EditorUserBuildSettings.activeBuildTarget);

            var root = PopupRoot("SettingsPopup", new Color(0.212f, 0.125f, 0.588f, 1f), coins: false);
            try
            {
                var content = Rect("Content", root.transform, Center, Vector2.zero, Reference);
                // Title row hangs from the safe top like the screens' top bars (the root reaches 400 past it).
                var header = Rect("Header", root.transform, Top, new Vector2(0, DesignTop - 400f), Reference, new Vector2(0.5f, 1f));
                var title = Text("Title", header, At(585, 228), new Vector2(600, 100), "settings.title", 72, White, "settings.title");
                var close = ButtonRect("CloseButton", header, Center, At(1048, 228), new Vector2(124, 128), S("UI_Button_Close"));

                var mainPage = Span("MainPage", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                ToggleSwitch Switch(string name, float x, string icon, string key)
                {
                    var button = ButtonRect(name, mainPage, Center, At(x, 896), new Vector2(235, 235), S("UI_InLevel_IconBase"));
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

                // Rows stack from the well's top; the popup places them and sizes the well.
                var rowPanel = Pic("Rows", mainPage, Center, At(585, 1160), new Vector2(760, 400), S("UI_InLevel_Tray"), new Vector2(0.5f, 1f));
                rowPanel.raycastTarget = false;
                Button Row(string name, string key)
                {
                    var row = ButtonRect(name, rowPanel, Top, new Vector2(0, -115), new Vector2(630, 150), S("UI_Button_Wide_Purple"));
                    Text("Label", row, new Vector2(0, 8), new Vector2(560, 90), key ?? "", 50, White, key);
                    return row.GetComponent<Button>();
                }
                var languageRow = Row("LanguageRow", null);
                var helpRow = Row("HelpRow", "settings.help");
                var feedbackRow = Row("FeedbackRow", "settings.feedback");
                var copyRow = Row("CopyAddressRow", null);

                // Language page: one button per language table, a check on the current one.
                var languagePage = Span("LanguagePage", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var list = Rect("List", languagePage, Center, At(585, 640), new Vector2(630, 900), new Vector2(0.5f, 1f));
                var listLayout = list.gameObject.AddComponent<VerticalLayoutGroup>();
                listLayout.spacing = 30f;
                listLayout.childAlignment = TextAnchor.UpperCenter;
                listLayout.childControlWidth = listLayout.childControlHeight = false;
                listLayout.childForceExpandWidth = listLayout.childForceExpandHeight = false;
                var template = ButtonRect("LanguageTemplate", list, Top, Vector2.zero, new Vector2(630, 150), S("UI_Button_Wide_Purple"));
                Text("Label", template, new Vector2(0, 8), new Vector2(440, 90), "Language", 54, White);
                Pic("Check", template, Center, new Vector2(240, 8), new Vector2(80, 80), S("UI_Icon_Check")).raycastTarget = false;
                var languageBack = WideBack(languagePage, 1900);
                languagePage.gameObject.SetActive(false);

                // Help page: the basics, then one note per mechanic met; scrolls on short screens.
                var helpPage = Span("HelpPage", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var helpWell = Pic("Well", helpPage, Center, At(585, 1160), new Vector2(960, 1300), S("UI_InLevel_Tray"));
                var viewport = Span("Viewport", helpWell, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Stretch(viewport, 30f, 36f);
                viewport.gameObject.AddComponent<RectMask2D>();
                var body = Rect("Body", viewport, Top, Vector2.zero, new Vector2(900, 0), new Vector2(0.5f, 1f));
                body.anchorMin = new Vector2(0f, 1f);
                body.anchorMax = new Vector2(1f, 1f);
                body.sizeDelta = Vector2.zero;
                var bodyLayout = body.gameObject.AddComponent<VerticalLayoutGroup>();
                bodyLayout.padding = new RectOffset(24, 24, 24, 24);
                bodyLayout.spacing = 30f;
                bodyLayout.childControlWidth = bodyLayout.childControlHeight = true;
                bodyLayout.childForceExpandWidth = true;
                bodyLayout.childForceExpandHeight = false;
                body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var scroll = helpWell.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport;
                scroll.content = body;
                scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                helpWell.raycastTarget = true;

                GameObject Line(string key)
                {
                    var line = Text(key, body, Vector2.zero, new Vector2(840, 60), key, 40, White, key);
                    line.enableAutoSizing = false;
                    line.textWrappingMode = TextWrappingModes.Normal;
                    line.alignment = TextAlignmentOptions.Left;
                    return line.gameObject;
                }
                foreach (var key in new[] { "help.move", "help.target", "help.capacity", "help.same" }) Line(key);
                var notes = new (Project.Contexts.Game.Levels.Mechanic Mechanic, string Key)[]
                {
                    (Project.Contexts.Game.Levels.Mechanic.Portal, "help.portal"), (Project.Contexts.Game.Levels.Mechanic.HiddenBall, "help.hidden"), (Project.Contexts.Game.Levels.Mechanic.FrozenTube, "help.frozen"),
                    (Project.Contexts.Game.Levels.Mechanic.ShutterKey, "help.shutter"), (Project.Contexts.Game.Levels.Mechanic.Bomb, "help.bomb"), (Project.Contexts.Game.Levels.Mechanic.Tower, "help.tower")
                };
                var help = helpPage.gameObject.AddComponent<HelpPage>();
                Set(help, "_catalog", AssetDatabase.LoadAssetAtPath<Project.Contexts.Game.Levels.LevelCatalog>(LevelCatalogPath));
                var helpSo = new SerializedObject(help);
                var noteList = helpSo.FindProperty("_notes");
                noteList.arraySize = notes.Length;
                for (var i = 0; i < notes.Length; i++)
                {
                    var item = noteList.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("Mechanic").intValue = (int)notes[i].Mechanic;
                    item.FindPropertyRelative("Item").objectReferenceValue = Line(notes[i].Key);
                }
                helpSo.ApplyModifiedPropertiesWithoutUndo();
                var helpBack = WideBack(helpPage, 1950);
                helpPage.gameObject.SetActive(false);

                // Footer on the safe bottom: name, version and build; legal links above it once their pages exist.
                var footerY = 400f + DesignBottom + 50f;
                var footer = Rect("Footer", root.transform, Bottom, new Vector2(0, footerY), new Vector2(760, 80));
                var footerHit = footer.gameObject.AddComponent<Image>();
                footerHit.color = Color.clear;
                var footerButton = footer.gameObject.AddComponent<Button>();
                footerButton.targetGraphic = footerHit;
                footer.gameObject.AddComponent<ButtonFeedback>();
                var footerLabel = Text("Label", footer, Vector2.zero, new Vector2(740, 60), "Orbit Sort", 34, White);
                Button Link(string name, float x, string key)
                {
                    var link = Rect(name, root.transform, Bottom, new Vector2(x, footerY + 80f), new Vector2(300, 70));
                    var hit = link.gameObject.AddComponent<Image>();
                    hit.color = Color.clear;
                    var button = link.gameObject.AddComponent<Button>();
                    button.targetGraphic = hit;
                    link.gameObject.AddComponent<ButtonFeedback>();
                    Text("Label", link, Vector2.zero, new Vector2(280, 56), key, 36, White, key);
                    link.gameObject.SetActive(false);
                    return button;
                }
                var privacy = Link("PrivacyLink", -170f, "settings.privacy");
                var terms = Link("TermsLink", 170f, "settings.terms");

                var toast = Pic("Toast", root.transform, Bottom, new Vector2(0, footerY + 170f), new Vector2(420, 96), S("UI_Pill_Well_96"));
                toast.raycastTarget = false;
                var toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
                toastGroup.alpha = 0f;
                toastGroup.blocksRaycasts = false;
                toastGroup.interactable = false;
                var toastLabel = Text("Label", toast, new Vector2(0, 2), new Vector2(360, 64), "settings.copied", 38, White);

                var popup = root.AddComponent<SettingsPopup>();
                // A full page: shows at once, no panel motion.
                var entrance = new SerializedObject(popup);
                entrance.FindProperty("_entrance").enumValueIndex = (int)PopupEntrance.None;
                entrance.FindProperty("_rowStep").floatValue = 170f;
                entrance.FindProperty("_rowPadding").floatValue = 30f;
                entrance.ApplyModifiedPropertiesWithoutUndo();
                Set(popup, "_content", content);
                Set(popup, "_sound", sound);
                Set(popup, "_music", music);
                Set(popup, "_haptics", haptics);
                Set(popup, "_close", close.GetComponent<Button>());
                Set(popup, "_title", title.GetComponent<LocalizedText>());
                Set(popup, "_mainPage", mainPage.gameObject);
                Set(popup, "_languagePage", languagePage.gameObject);
                Set(popup, "_helpPage", helpPage.gameObject);
                SetArray(popup, "_backButtons", new Object[] { languageBack, helpBack });
                Set(popup, "_rowPanel", rowPanel.rectTransform);
                Set(popup, "_languageRow", languageRow);
                Set(popup, "_languageLabel", languageRow.GetComponentInChildren<TMP_Text>());
                Set(popup, "_helpRow", helpRow);
                Set(popup, "_feedbackRow", feedbackRow);
                Set(popup, "_copyRow", copyRow);
                Set(popup, "_copyLabel", copyRow.GetComponentInChildren<TMP_Text>());
                Set(popup, "_languageList", list);
                Set(popup, "_languageTemplate", template.GetComponent<Button>());
                Set(popup, "_footer", footerButton);
                Set(popup, "_footerLabel", footerLabel);
                Set(popup, "_privacy", privacy);
                Set(popup, "_terms", terms);
                Set(popup, "_toast", toastGroup);
                Set(popup, "_toastLabel", toastLabel);
                Set(popup, "_links", links);
                Set(popup, "_buildInfo", buildInfo);
                PrefabUtility.SaveAsPrefabAsset(root, SettingsPopupPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<SettingsPopup>(SettingsPopupPath);
        }

        // Purple Back button centred at y on the design screen.
        private static Button WideBack(RectTransform page, float y)
        {
            var back = ButtonRect("BackButton", page, Center, At(585, y), new Vector2(630, 150), S("UI_Button_Wide_Purple"));
            Text("Label", back, new Vector2(0, 8), new Vector2(440, 90), "settings.back", 54, White, "settings.back");
            return back.GetComponent<Button>();
        }
    }
}
