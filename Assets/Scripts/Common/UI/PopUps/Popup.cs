using System;
using Project.Common.Tween;
using UnityEngine;

namespace Project.Common.UI.PopUps
{
    public enum PopupEntrance
    {
        // Appears as is.
        None,
        // Panel settles from slightly large.
        Settle,
        // Panel bursts in from small, for wins.
        Burst
    }

    // Root needs a CanvasGroup and a "content" child; dim and panel show and hide at once, only the panel scale moves.
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class Popup : MonoBehaviour
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private PopupEntrance _entrance = PopupEntrance.Settle;

        // Subclasses may pick per show, inside OnShow.
        protected PopupEntrance Entrance { get; set; }
        protected float EntranceDelay { get; set; }

        private CanvasGroup _group;

        public bool IsOpen { get; private set; }
        public event Action<Popup> Closed;

        protected virtual void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_content == null) _content = (RectTransform)transform;
        }

        internal void Show(object args)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            IsOpen = true;
            Entrance = _entrance;
            EntranceDelay = 0f;
            OnShow(args);
            PrimeTween.Tween.StopAll(_content);
            _content.localScale = Vector3.one;
            if (Entrance == PopupEntrance.Settle) TweenPresets.PopupSettle(_content, EntranceDelay);
            else if (Entrance == PopupEntrance.Burst) TweenPresets.PopupBurst(_content);
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
        }

        // Safe to call twice.
        public void Close()
        {
            if (!IsOpen) return;
            CloseImmediately();
        }

        internal void CloseImmediately()
        {
            if (!IsOpen) return;
            IsOpen = false;
            PrimeTween.Tween.StopAll(_content);
            _content.localScale = Vector3.one;
            gameObject.SetActive(false);
            OnHidden();
            Closed?.Invoke(this);
        }

        protected virtual void OnShow(object args) { }
        protected virtual void OnHidden() { }
    }
}
