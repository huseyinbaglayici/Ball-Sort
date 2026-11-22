using Project.Common.UI;
using Project.Common.UI.PopUps;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.Game
{
    public readonly struct IntroArgs
    {
        public readonly string RibbonKey;
        public readonly string TitleKey;
        public readonly string BodyKey;
        // Name of the art child to show.
        public readonly string Art;

        public IntroArgs(string ribbonKey, string titleKey, string bodyKey, string art)
        {
            RibbonKey = ribbonKey;
            TitleKey = titleKey;
            BodyKey = bodyKey;
            Art = art;
        }
    }

    // New item or booster card: its art, name and one line; a tap anywhere closes it.
    public sealed class IntroPopup : Popup
    {
        [SerializeField] private LocalizedText _ribbon;
        [SerializeField] private LocalizedText _title;
        [SerializeField] private LocalizedText _body;
        [SerializeField] private Transform _art;
        [SerializeField] private Button[] _close = System.Array.Empty<Button>();

        protected override void Awake()
        {
            base.Awake();
            foreach (var button in _close) button.onClick.AddListener(Close);
        }

        protected override void OnShow(object args)
        {
            var intro = args is IntroArgs a ? a : default;
            _ribbon.Key = intro.RibbonKey;
            _title.Key = intro.TitleKey;
            _body.Key = intro.BodyKey;
            foreach (Transform child in _art) child.gameObject.SetActive(child.name == intro.Art);
        }
    }
}
