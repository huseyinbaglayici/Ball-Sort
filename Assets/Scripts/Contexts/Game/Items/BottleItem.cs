using System.Collections.Generic;
using Project.Common.Tween;
using Project.Core.Events;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.Contexts.Game.Items
{
    public readonly struct BottleTapped : IEvent
    {
        public readonly BottleItem Bottle;
        public BottleTapped(BottleItem bottle) => Bottle = bottle;
    }

    /// <summary>
    /// A bottle on the board: the glass, its tap area and the stack of ball units shown inside it.
    /// It only holds views. The puzzle rules and the real contents live in SortBoard.
    /// Taps arrive through the camera's Physics2DRaycaster, so a Collider2D is required.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class BottleItem : Item, IPointerClickHandler
    {
        public const float SlotWidth = 1.1f;
        public const float SlotHeight = 1.05f;
        public const float Padding = 0.18f;

        /// <summary>World-space diameter of one ball; slightly smaller than a slot so stacked balls do not touch.</summary>
        public const float BallDiameter = 0.96f;
        private const float LiftHeight = 0.7f;

        public static readonly Color NormalGlassColor = new(0.82f, 0.92f, 1f, 0.35f);
        private static readonly Color CompletedGlassColor = new(0.5f, 0.95f, 0.6f, 0.6f);

        [SerializeField] private SpriteRenderer _glass;
        [SerializeField] private BoxCollider2D _collider;

        private readonly List<BallItem> _balls = new();
        private float _homeY;
        private bool _selected;

        public override ItemType Type => ItemType.Bottle;

        public int Index { get; private set; }
        public int Capacity { get; private set; }
        public IReadOnlyList<BallItem> Balls => _balls;
        public int BallCount => _balls.Count;

        public static Vector2 SizeFor(int capacity) =>
            new(SlotWidth + Padding * 2f, capacity * SlotHeight + Padding * 2f);

        public void Setup(int index, int capacity, Vector2 localPosition)
        {
            Index = index;
            Capacity = capacity;
            _selected = false;
            _homeY = localPosition.y;
            transform.localPosition = localPosition;
            transform.localScale = Vector3.one;
            _balls.Clear();

            var size = SizeFor(capacity);
            _glass.color = NormalGlassColor;
            _glass.size = size;
            _glass.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            _collider.size = size;
            _collider.offset = new Vector2(0f, size.y * 0.5f);
        }

        public Vector3 SlotLocalPosition(int slot) =>
            new(0f, Padding + SlotHeight * (slot + 0.5f), 0f);

        public Vector3 SlotWorldPosition(int slot) => transform.TransformPoint(SlotLocalPosition(slot));

        /// <summary>Adds the unit on top of the stack and places it in its slot (no animation).</summary>
        public void Push(BallItem ball, bool snap)
        {
            var slot = _balls.Count;
            _balls.Add(ball);
            ball.transform.SetParent(transform, !snap);
            if (snap) ball.transform.localPosition = SlotLocalPosition(slot);
        }

        public BallItem Pop()
        {
            var top = _balls[_balls.Count - 1];
            _balls.RemoveAt(_balls.Count - 1);
            return top;
        }

        /// <summary>A completed bottle is locked by the rules; this only shows it.</summary>
        public void SetCompleted(bool completed)
        {
            _glass.color = completed ? CompletedGlassColor : NormalGlassColor;
            if (completed) TweenPresets.Punch(transform, 0.06f);
        }

        public void SetSelected(bool selected)
        {
            if (_selected == selected) return;
            _selected = selected;
            PrimeTween.Tween.StopAll(transform);
            PrimeTween.Tween.LocalPositionY(transform, _homeY + (selected ? LiftHeight : 0f), 0.12f, PrimeTween.Ease.OutQuad);
        }

        public void OnPointerClick(PointerEventData eventData) =>
            EventBus<BottleTapped>.Raise(new BottleTapped(this));

        public override void OnDespawn()
        {
            PrimeTween.Tween.StopAll(transform);
            _selected = false;
            _balls.Clear();
        }
    }
}
