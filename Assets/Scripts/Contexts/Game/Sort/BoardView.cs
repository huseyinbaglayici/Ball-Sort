using System;
using System.Collections.Generic;
using Project.Contexts.Game.Items;
using Project.Contexts.Game.Levels;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    /// <summary>
    /// Shows a <see cref="SortBoard"/>: spawns the bottles and ball units from the pool, scales the layout
    /// to fit the screen and animates moves. It never decides what is allowed, SortBoard does.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float MoveDuration = 0.2f;
        private const float Stagger = 0.06f;

        [SerializeField, Min(1f)] private float _maxWidth = 9.6f;
        [SerializeField, Min(1f)] private float _maxHeight = 13f;
        [SerializeField] private Vector2 _center = new(0f, -0.5f);

        private readonly List<BottleItem> _bottles = new();
        private ItemFactory _factory;
        private LevelPalette _palette;

        public BottleItem Bottle(int index) => _bottles[index];

        public void Build(LevelData level, SortBoard board, ItemFactory factory, LevelPalette palette)
        {
            Clear();
            _factory = factory;
            _palette = palette;

            var size = BottleItem.SizeFor(level.Capacity);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var bottle in level.Bottles)
            {
                min = Vector2.Min(min, new Vector2(bottle.Position.x - size.x * 0.5f, bottle.Position.y));
                max = Vector2.Max(max, new Vector2(bottle.Position.x + size.x * 0.5f, bottle.Position.y + size.y));
            }

            var span = max - min;
            var scale = Mathf.Min(1f, _maxWidth / Mathf.Max(span.x, 0.01f), _maxHeight / Mathf.Max(span.y, 0.01f));
            var middle = (min + max) * 0.5f;
            transform.localScale = Vector3.one * scale;
            transform.localPosition = new Vector3(_center.x, _center.y, 0f);

            for (var b = 0; b < level.Bottles.Count; b++)
            {
                var bottle = _factory.Get<BottleItem>(ItemType.Bottle, transform);
                bottle.Setup(b, level.Capacity, level.Bottles[b].Position - middle);
                _bottles.Add(bottle);

                for (var s = 0; s < board.Count(b); s++)
                {
                    var color = board.ColorAt(b, s);
                    var type = board.IsSpecial(b, s) ? ItemType.QuestionMark : ItemType.Ball;
                    var ball = _factory.Get<BallItem>(type, bottle.transform);
                    ball.Setup(color, _palette.Get(color), hidden: !board.IsKnown(b, s));
                    bottle.Push(ball, snap: true);
                }
            }
        }

        /// <summary>Returns every spawned item to the pool.</summary>
        public void Clear()
        {
            foreach (var bottle in _bottles)
            {
                while (bottle.BallCount > 0) _factory.Release(bottle.Pop());
                _factory.Release(bottle);
            }
            _bottles.Clear();
        }

        /// <summary>Moves the top <paramref name="count"/> ball units of one bottle onto another.</summary>
        public void Transfer(int from, int to, int count, Action onDone)
        {
            var source = _bottles[from];
            var target = _bottles[to];
            if (count <= 0)
            {
                onDone?.Invoke();
                return;
            }

            for (var i = 0; i < count; i++)
            {
                var ball = source.Pop();
                var slot = target.BallCount;
                target.Push(ball, snap: false);
                var last = i == count - 1;

                PrimeTween.Tween.Position(ball.transform, target.SlotWorldPosition(slot), MoveDuration,
                        PrimeTween.Ease.OutQuad, startDelay: i * Stagger)
                    .OnComplete(() =>
                    {
                        ball.transform.localPosition = target.SlotLocalPosition(slot);
                        if (last) onDone?.Invoke();
                    });
            }
        }

        public void SetCompleted(int bottle, bool completed) => _bottles[bottle].SetCompleted(completed);

        public void Reveal(int bottle, int slot)
        {
            var ball = _bottles[bottle].Balls[slot];
            ball.Reveal(_palette.Get(ball.ColorIndex));
        }
    }
}
