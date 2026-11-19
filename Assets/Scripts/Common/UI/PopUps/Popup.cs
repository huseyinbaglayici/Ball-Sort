using System;
using Project.Common.Tween;
using UnityEngine;

namespace Project.Common.UI.PopUps
{
    // The prefab root needs a CanvasGroup and a "content" child that scales in.
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class Popup : MonoBehaviour
    {
        [SerializeField] private RectTransform _content;

        private CanvasGroup _group;

        public bool IsOpen { get; private set; }
        public event Action<Popup> Closed;

        protected virtual void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_content == null) _content = (RectTransform)transform;
        }

        internal async Awaitable ShowAsync(object args)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            IsOpen = true;
            _group.interactable = false;
            OnShow(args);
            _ = TweenPresets.PopIn(_content);
            await TweenPresets.FadeIn(_group);
            _group.interactable = true;
            _group.blocksRaycasts = true;
        }

        // Safe to call twice.
        public async void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _group.interactable = false;
            _ = TweenPresets.PopOut(_content);
            await TweenPresets.FadeOut(_group);
            gameObject.SetActive(false);
            OnHidden();
            Closed?.Invoke(this);
        }

        internal void CloseImmediately()
        {
            if (!IsOpen) return;
            IsOpen = false;
            gameObject.SetActive(false);
            OnHidden();
            Closed?.Invoke(this);
        }

        protected virtual void OnShow(object args) { }
        protected virtual void OnHidden() { }
    }
}
