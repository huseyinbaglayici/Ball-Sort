using System.Collections.Generic;
using Project.Contexts.Game.Items;
using Project.Contexts.Game.Levels;
using TMPro;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    // Rebuilt from the model after every change; ball badges are parented to the balls to travel with them.
    public sealed class ObstacleMarkers
    {
        // Above the balls and tube glass (TubeArt orders).
        private const int PanelOrder = 80;
        private const int PanelLabelOrder = 81;
        // Above a ball body, below the front glass.
        private const int BadgeOrder = 49;
        private static readonly Color CurtainColor = new(0.32f, 0.34f, 0.4f, 1f);
        // Slightly larger than a ball's core.
        private const float CurtainOrbScale = 1.2f;
        private static readonly Color BoxColor = new(0.12f, 0.12f, 0.16f, 0.92f);
        private static readonly Color KeyColor = new(1f, 0.85f, 0.2f);
        private static readonly Color LitColor = new(1f, 0.25f, 0.2f);
        private static readonly Color PassiveColor = new(0.75f, 0.75f, 0.75f);
        private static readonly Color DefusedColor = new(0.4f, 1f, 0.5f);
        private static Sprite _white;

        private readonly List<GameObject> _spawned = new();

        public void Refresh(SortBoard board, IReadOnlyList<BottleItem> bottles, LevelPalette palette)
        {
            Clear();
            for (var i = 0; i < board.CurtainCount; i++)
            {
                if (!board.IsCurtainActive(i)) continue;
                var (bottle, color, _) = board.Curtain(i);
                Curtain(bottles[bottle], palette.GetOrb(color).Core);
            }

            for (var i = 0; i < board.LockboxCount; i++)
            {
                if (!board.IsLockboxActive(i)) continue;
                foreach (var bottle in board.LockboxBottles(i)) Panel(bottles[bottle], BoxColor, board.LockboxId(i), Vector3.zero);
            }

            for (var b = 0; b < board.BottleCount && b < bottles.Count; b++)
            {
                var balls = bottles[b].Balls;
                for (var s = 0; s < board.Count(b) && s < balls.Count; s++)
                {
                    var key = board.KeyAt(b, s);
                    if (key != null) Badge(balls[s].transform, key, KeyColor);
                    var bomb = board.BombAt(b, s);
                    if (bomb < 0) continue;
                    if (board.IsBombDefused(bomb)) Badge(balls[s].transform, "OK", DefusedColor);
                    else Badge(balls[s].transform, board.BombFuse(bomb).ToString(), board.IsBombLit(bomb) ? LitColor : PassiveColor);
                }
            }
        }

        public void Clear()
        {
            foreach (var marker in _spawned)
                if (marker != null) Object.Destroy(marker);
            _spawned.Clear();
        }

        // "x1": one completed tube of the curtain's color lifts it.
        private void Curtain(BottleItem bottle, Sprite orb)
        {
            var panel = Panel(bottle, CurtainColor, "x1", new Vector3(0f, -0.5f, 0f));
            var icon = new GameObject("Marker_Orb").AddComponent<SpriteRenderer>();
            icon.transform.SetParent(panel, false);
            icon.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            icon.sprite = orb;
            icon.sortingOrder = PanelLabelOrder;
            icon.transform.localScale = Vector3.one * CurtainOrbScale;
        }

        private Transform Panel(BottleItem bottle, Color color, string text, Vector3 labelPosition)
        {
            var size = bottle.Size;
            var panel = new GameObject("Marker_Panel").AddComponent<SpriteRenderer>();
            panel.transform.SetParent(bottle.transform, false);
            panel.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            panel.sprite = White;
            panel.drawMode = SpriteDrawMode.Sliced;
            panel.size = size;
            panel.color = color;
            panel.sortingOrder = PanelOrder;
            _spawned.Add(panel.gameObject);
            Label(panel.transform, labelPosition, text, Color.white, 8f, PanelLabelOrder);
            return panel.transform;
        }

        private void Badge(Transform ball, string text, Color color) =>
            _spawned.Add(Label(ball, new Vector3(0.3f, 0.3f, 0f), text, color, 4f, BadgeOrder));

        private static GameObject Label(Transform parent, Vector3 localPosition, string text, Color color, float fontSize, int order)
        {
            var label = new GameObject("Marker_Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = localPosition;
            label.rectTransform.sizeDelta = new Vector2(2f, 1f);
            label.text = text;
            label.color = color;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.outlineWidth = 0.25f;
            label.outlineColor = Color.black;
            label.sortingOrder = order;
            return label.gameObject;
        }

        private static Sprite White
        {
            get
            {
                if (_white == null)
                    // Full rect: the panel is drawn sliced.
                    _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f,
                        0, SpriteMeshType.FullRect);
                return _white;
            }
        }
    }
}
