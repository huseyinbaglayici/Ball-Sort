using Project.Core.Services;
using UnityEngine;

namespace Project.Contexts.Main
{
    // A popup's coin counter takes the spot of the open screen's own: top left in a level, top right on Home.
    public sealed class PopupCoinSpot : MonoBehaviour
    {
        [SerializeField] private RectTransform _bar;
        [SerializeField] private RectTransform _coin;
        [SerializeField] private Rect _levelBar;
        [SerializeField] private Rect _levelCoin;
        [SerializeField] private Rect _homeBar;
        [SerializeField] private Rect _homeCoin;

        private void OnEnable()
        {
            var home = Services.TryGet<MainFlow>(out var flow) && flow.Current == MainState.Home;
            Place(_bar, home ? _homeBar : _levelBar);
            Place(_coin, home ? _homeCoin : _levelCoin);
        }

        private static void Place(RectTransform target, Rect spot)
        {
            if (target == null) return;
            target.anchoredPosition = spot.position;
            target.sizeDelta = spot.size;
        }
    }
}
