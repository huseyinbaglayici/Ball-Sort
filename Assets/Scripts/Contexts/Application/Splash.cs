using Project.Common.Tween;
using Project.Contexts.Main;
using Project.Core.Events;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Project.Contexts.Application
{
    // Boot scene: opening art with a "Loading..." line while Main loads; a tap skips the hold, then it fades out.
    public sealed class Splash : MonoBehaviour, IPointerDownHandler
    {
        private const int MaxDots = 3;

        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TMP_Text _loading;
        [SerializeField] private float _dotTime = 0.35f;
        [SerializeField] private float _holdSeconds = 1.2f;

        // Localization starts with Main, so the word follows the device language.
        private string _word;

        private bool _mainReady;
        private bool _skipped;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EventBus<MainReady>.Subscribe(OnMainReady);
        }

        private void OnDestroy() => EventBus<MainReady>.Unsubscribe(OnMainReady);

        private async void Start()
        {
            var shownAt = Time.realtimeSinceStartup;
            _word = UnityEngine.Application.systemLanguage == SystemLanguage.Turkish ? "Yükleniyor" : "Loading";
            ShowDots(Time.realtimeSinceStartup);
            var load = SceneManager.LoadSceneAsync(SceneNames.Main);
            if (load == null)
            {
                Debug.LogError($"[Splash] Scene '{SceneNames.Main}' is not in the Build Profiles scene list.");
                return;
            }

            while (!load.isDone)
            {
                ShowDots(Time.realtimeSinceStartup);
                await Awaitable.NextFrameAsync();
            }

            while (!_mainReady || (!_skipped && Time.realtimeSinceStartup - shownAt < _holdSeconds))
            {
                if (this == null) return;
                ShowDots(Time.realtimeSinceStartup);
                await Awaitable.NextFrameAsync();
            }

            await TweenPresets.FadeOut(_group);
            if (this != null) Destroy(gameObject);
        }

        private void OnMainReady(MainReady _) => _mainReady = true;

        public void OnPointerDown(PointerEventData eventData) => _skipped = true;

        // One to three dots; the hidden rest keep the line's width, so the word never shifts.
        private void ShowDots(float time)
        {
            if (_loading == null) return;
            var shown = 1 + (int)(time / Mathf.Max(_dotTime, 0.05f)) % MaxDots;
            _loading.text = _word + new string('.', shown) + "<alpha=#00>" + new string('.', MaxDots - shown);
        }
    }
}
