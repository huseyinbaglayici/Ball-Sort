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
            PrimeTween.Tween.StopAll(_group);
            OnShow(args);
            _ = TweenPresets.PopupScaleIn(_content);
            await TweenPresets.PopupDimIn(_group);
            if (!IsOpen) return;
            _group.interactable = true;
            _group.blocksRaycasts = true;
        }

        // Safe to call twice.
        public async void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _group.interactable = false;
            PrimeTween.Tween.StopAll(_content);
            PrimeTween.Tween.StopAll(_group);
            await TweenPresets.PopupDimOut(_group);
            gameObject.SetActive(false);
            OnHidden();
            Closed?.Invoke(this);
        }

        internal void CloseImmediately()
        {
            if (!IsOpen) return;
            IsOpen = false;
            PrimeTween.Tween.StopAll(_content);
            PrimeTween.Tween.StopAll(_group);
            gameObject.SetActive(false);
            OnHidden();
            Closed?.Invoke(this);
        }

        protected virtual void OnShow(object args) { }
        protected virtual void OnHidden() { }
    }
}
