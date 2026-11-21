using System;
using Project.Common.UI;
using Project.Contexts.Application.Economy;
using Project.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.Home
{
    // A stretch of the campaign road around the current level: won, current and coming nodes on a winding path.
    public sealed class JourneyRoad : MonoBehaviour
    {
        [Serializable]
        private sealed class Node
        {
            public RectTransform Rect;
            public Image Body;
            public TMP_Text Number;
            public GameObject Check;
            public DifficultyTag Tag;
        }

        [SerializeField] private Node[] _nodes = Array.Empty<Node>();
        // Index of the node that shows the current level; lower indices are won levels.
        [SerializeField] private int _currentIndex;
        [SerializeField] private float _nodeSize = 172f;
        [SerializeField] private float _currentSize = 200f;
        [SerializeField] private RectTransform _mascot;
        [SerializeField] private Vector2 _mascotOffset = new(200f, 0f);
        [SerializeField] private RectTransform _chest;
        [SerializeField] private Vector2 _chestOffset = new(-180f, 0f);
        [SerializeField] private GameObject _currentGlow;
        // Coming nodes whose top would pass this element's bottom are hidden (short screens).
        [SerializeField] private RectTransform _ceiling;
        // Link from node i to node i + 1; shown when both ends are.
        [SerializeField] private GameObject[] _segments = Array.Empty<GameObject>();
        [SerializeField] private Color _won = new(0.78f, 0.76f, 1f);
        [SerializeField] private Color _current = Color.white;
        [SerializeField] private Color _coming = new(0.74f, 0.72f, 0.84f);
        [SerializeField] private Color _hard = new(1f, 0.62f, 0.25f);
        [SerializeField] private Color _superHard = new(0.78f, 0.24f, 0.52f);
        [SerializeField] private Color _ink = new(0.12f, 0.10f, 0.25f);

        private int _shown;
        private LocalizationService _loc;
        private Vector2Int _screen;

        // A rotated or resized screen moves the ceiling: lay the column out again.
        private void LateUpdate()
        {
            if (_loc == null || (_screen.x == Screen.width && _screen.y == Screen.height)) return;
            Show(_shown, _loc);
        }

        public void Show(int current, LocalizationService loc)
        {
            _shown = current;
            _loc = loc;
            _screen = new Vector2Int(Screen.width, Screen.height);
            var road = CampaignRules.NextRoadLevel(current);
            var chestShown = false;
            for (var i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                var level = current - _currentIndex + i;
                var visible = level >= 1 && level <= CampaignRules.MaxLevel;
                node.Rect.gameObject.SetActive(visible);
                if (!visible) continue;

                var won = level < current;
                var isCurrent = level == current;
                var hardness = won ? LevelDifficulty.Normal : CampaignRules.DifficultyOf(level);
                node.Body.color = won ? _won
                    : hardness == LevelDifficulty.SuperHard ? _superHard
                    : hardness == LevelDifficulty.Hard ? _hard
                    : isCurrent ? _current : _coming;
                node.Number.text = level.ToString();
                node.Number.color = hardness == LevelDifficulty.SuperHard ? Color.white : _ink;
                if (node.Check != null) node.Check.SetActive(won);
                if (node.Tag != null)
                {
                    var difficulty = won ? LevelDifficulty.Normal : CampaignRules.DifficultyOf(level);
                    node.Tag.Set(EconomyText.Difficulty(difficulty, loc), EconomyText.DifficultyMarks(difficulty));
                }
                var size = isCurrent ? _currentSize : _nodeSize;
                node.Rect.sizeDelta = new Vector2(size, size);

                if (isCurrent)
                {
                    PlaceBy(_mascot, node.Rect, _mascotOffset);
                    if (_currentGlow != null) PlaceBy((RectTransform)_currentGlow.transform, node.Rect, Vector2.zero);
                }
                if (level == road && _chest != null)
                {
                    PlaceBy(_chest, node.Rect, _chestOffset);
                    chestShown = true;
                }
            }
            if (_chest != null) _chest.gameObject.SetActive(chestShown);
            HideUnderCeiling();
            for (var i = 0; i < _segments.Length && i + 1 < _nodes.Length; i++)
                _segments[i].SetActive(_nodes[i].Rect.gameObject.activeSelf && _nodes[i + 1].Rect.gameObject.activeSelf);
        }

        private void HideUnderCeiling()
        {
            if (_ceiling == null) return;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            _ceiling.GetWorldCorners(corners);
            var limit = corners[0].y;
            for (var i = _currentIndex + 1; i < _nodes.Length; i++)
            {
                var rect = _nodes[i].Rect;
                if (!rect.gameObject.activeSelf) continue;
                rect.GetWorldCorners(corners);
                if (corners[1].y <= limit) continue;
                rect.gameObject.SetActive(false);
                if (_chest != null && _chest.anchoredPosition.y >= rect.anchoredPosition.y - 1f) _chest.gameObject.SetActive(false);
            }
        }

        private static void PlaceBy(RectTransform follower, RectTransform node, Vector2 offset)
        {
            if (follower == null) return;
            follower.anchorMin = node.anchorMin;
            follower.anchorMax = node.anchorMax;
            follower.anchoredPosition = node.anchoredPosition + offset;
        }
    }
}
