using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.Home
{
    // Full-screen chapter poster behind Home; a new chapter fades in over the old one and moves the hand orb.
    public sealed class HomePoster : MonoBehaviour
    {
        [SerializeField] private ChapterArt _art;
        [SerializeField] private AspectRatioFitter _fitter;
        [SerializeField] private Image _back;
        [SerializeField] private Image _front;
        [SerializeField] private RectTransform _orb;
        [SerializeField] private float _fadeTime = 0.8f;

        private int _shown = -1;

        public void Show(int level)
        {
            var index = _art != null ? _art.IndexOf(level) : -1;
            if (index < 0 || index == _shown) return;
            var chapter = _art[index];
            var first = _shown < 0;
            _shown = index;

            PrimeTween.Tween.StopAll(_front);
            _back.sprite = first ? chapter.Poster : _front.sprite;
            _front.sprite = chapter.Poster;
            var aspect = chapter.Poster.rect.width / chapter.Poster.rect.height;
            _fitter.aspectRatio = aspect;
            PlaceOrb(chapter, aspect);

            if (first) return;
            _front.color = new Color(1f, 1f, 1f, 0f);
            PrimeTween.Tween.Alpha(_front, 1f, _fadeTime, PrimeTween.Ease.InOutSine, useUnscaledTime: true);
        }

        private void PlaceOrb(ChapterArt.Chapter chapter, float aspect)
        {
            var half = new Vector2(chapter.OrbSize, chapter.OrbSize * aspect) * 0.5f;
            var centre = new Vector2(chapter.Orb.x, 1f - chapter.Orb.y);
            _orb.anchorMin = centre - half;
            _orb.anchorMax = centre + half;
            _orb.offsetMin = _orb.offsetMax = Vector2.zero;
        }
    }
}
