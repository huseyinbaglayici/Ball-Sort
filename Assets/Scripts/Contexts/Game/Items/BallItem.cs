using Project.Common.Tween;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    /// <summary>
    /// One unit of colored ball. The same component serves the Ball and QuestionMark prefabs:
    /// a QuestionMark unit hides its color behind a "?" until <see cref="Reveal"/> is called.
    /// </summary>
    public sealed class BallItem : Item
    {
        [SerializeField] private ItemType _type = ItemType.Ball;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private GameObject _questionMark;
        [SerializeField] private Color _hiddenColor = new(0.32f, 0.34f, 0.42f);

        public override ItemType Type => _type;

        /// <summary>Palette index of the real color (also while it is hidden).</summary>
        public int ColorIndex { get; private set; }

        public bool IsHidden { get; private set; }

        public void Setup(int colorIndex, Color color, bool hidden)
        {
            ColorIndex = colorIndex;
            IsHidden = hidden && _questionMark != null;
            // Scale from the sprite's own size, so any circle sprite (any pixels-per-unit) ends up the ball diameter wide.
            var spriteWidth = _renderer.sprite != null ? _renderer.sprite.bounds.size.x : 1f;
            _renderer.transform.localScale = Vector3.one * (BottleItem.BallDiameter / spriteWidth);
            _renderer.color = IsHidden ? _hiddenColor : color;
            if (_questionMark != null) _questionMark.SetActive(IsHidden);
            transform.localScale = Vector3.one;
        }

        public void Reveal(Color color)
        {
            if (!IsHidden) return;
            IsHidden = false;
            _renderer.color = color;
            if (_questionMark != null) _questionMark.SetActive(false);
            TweenPresets.Punch(transform, 0.2f);
        }

        public override void OnDespawn() => PrimeTween.Tween.StopAll(transform);
    }
}
