using Project.Core.Audio;
using Project.Core.Events;
using Project.Core.Localization;
using Project.Core.Services;
using TMPro;
using UnityEngine;

namespace Project.Contexts.Application.Economy
{
    // While shown it also plays the lives sounds: refill, granted life, unlimited ended, last life spent.
    public sealed class EconomyHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text _coins;
        [SerializeField] private TMP_Text _lives;
        [SerializeField] private TMP_Text _livesCount;
        [SerializeField] private TMP_Text _livesTimer;
        // Heart art: normal, or the infinite one while lives are unlimited.
        [SerializeField] private UnityEngine.UI.Image _heart;
        [SerializeField] private Sprite _heartSprite;
        [SerializeField] private Sprite _unlimitedSprite;

        private WalletService _wallet;
        private LivesService _livesService;
        private LocalizationService _loc;
        private float _nextTick;
        // Coins already in the wallet that are still flying in; the counter leaves them out until they land.
        private int _held;

        private void OnEnable()
        {
            if (!Services.TryGet(out _wallet) || !Services.TryGet(out _livesService) || !Services.TryGet(out _loc)) return;
            EventBus<CoinsChanged>.Subscribe(OnCoinsChanged);
            EventBus<LivesChanged>.Subscribe(OnLivesChanged);
            EventBus<LanguageChanged>.Subscribe(OnLanguageChanged);
            Refresh();
        }

        private void OnDisable()
        {
            _held = 0;
            EventBus<CoinsChanged>.Unsubscribe(OnCoinsChanged);
            EventBus<LivesChanged>.Unsubscribe(OnLivesChanged);
            EventBus<LanguageChanged>.Unsubscribe(OnLanguageChanged);
        }

        private void Update()
        {
            if (_livesService == null || Time.unscaledTime < _nextTick) return;
            Refresh();
        }

        private void OnCoinsChanged(CoinsChanged _) => Refresh();

        public void Hold(int coins)
        {
            _held = Mathf.Max(0, coins);
            Refresh();
        }

        // A landed share of the held coins counts up.
        public void Release(int coins) => Hold(_held - coins);

        private void OnLivesChanged(LivesChanged evt)
        {
            Refresh();
            var sound = evt.Reason switch
            {
                LivesChangeReason.Refilled => "life_refill",
                LivesChangeReason.Granted => "life",
                LivesChangeReason.UnlimitedEnded => "unlimited_end",
                LivesChangeReason.Spent when evt.Count == 0 => "life_spent",
                _ => null
            };
            if (sound != null && Services.TryGet<AudioManager>(out var audio)) audio.PlayOneShotSound(sound);
        }
        private void OnLanguageChanged(LanguageChanged _) => Refresh();

        private void Refresh()
        {
            if (_wallet == null) return;
            _nextTick = Time.unscaledTime + 1f;
            if (_coins != null) _coins.text = Mathf.Max(0, _wallet.Coins - _held).ToString();
            if (_lives != null) _lives.text = EconomyText.Lives(_livesService, _loc);
            if (_livesCount != null) _livesCount.text = EconomyText.LivesCount(_livesService);
            if (_livesTimer != null)
            {
                _livesTimer.text = EconomyText.LivesTimer(_livesService, _loc);
            }
            if (_heart != null) _heart.sprite = _livesService.IsUnlimited ? _unlimitedSprite : _heartSprite;
        }
    }
}
