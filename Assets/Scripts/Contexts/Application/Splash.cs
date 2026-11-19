using Project.Common.Tween;
using Project.Contexts.Main;
using Project.Core.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Contexts.Application
{
    // Boot scene: loads Main, waits for its first screen, then fades out and destroys itself.
    public sealed class Splash : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _progressFill;

        private bool _mainReady;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EventBus<MainReady>.Subscribe(OnMainReady);
        }

        private void OnDestroy() => EventBus<MainReady>.Unsubscribe(OnMainReady);

        private async void Start()
        {
            SetProgress(0f);
            var load = SceneManager.LoadSceneAsync(SceneNames.Main);
            if (load == null)
            {
                Debug.LogError($"[Splash] Scene '{SceneNames.Main}' is not in the Build Profiles scene list.");
                return;
            }

            while (!load.isDone)
            {
                SetProgress(load.progress / 0.9f);
                await Awaitable.NextFrameAsync();
            }
            SetProgress(1f);

            while (!_mainReady)
            {
                if (this == null) return;
                await Awaitable.NextFrameAsync();
            }

            await TweenPresets.FadeOut(_group);
            if (this != null) Destroy(gameObject);
        }

        private void OnMainReady(MainReady _) => _mainReady = true;

        private void SetProgress(float value)
        {
            if (_progressFill != null) _progressFill.fillAmount = Mathf.Clamp01(value);
        }
    }
}
