using Project.Common.Tween;
using UnityEngine;

namespace Project.Common.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LoadingOverlay : MonoBehaviour
    {
        private CanvasGroup _group;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        public async Awaitable ShowAsync()
        {
            gameObject.SetActive(true);
            _group.blocksRaycasts = true;
            await TweenPresets.FadeIn(_group);
        }

        public async Awaitable HideAsync()
        {
            await TweenPresets.FadeOut(_group);
            _group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }
    }
}
