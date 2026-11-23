using System.Collections.Generic;
using Project.Contexts.Game.Items;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    // Free Move targets: tap-me breath, landing-slot rims and glass focus on tubes that can take the lifted balls.
    public sealed partial class BoardView
    {
        // Target tubes for balls lifted from `from` breathe in a tap-me idle with their landing slots outlined.
        public void ShowTargets(int from, IReadOnlyList<int> targets)
        {
            HideTargets();
            foreach (var t in targets)
            {
                if (t < 0 || t >= _bottles.Count) continue;
                _idleTubes.Add(_bottles[t]);
                var landing = _model != null ? _model.MoveAmount(from, t) : 0;
                var first = _bottles[t].BallCount;
                for (var slot = first; slot < first + landing && slot < _bottles[t].Capacity; slot++) Outline(_bottles[t], slot);
            }
            if (_idleTubes.Count > 0) StartIdle();
            Focus(from, targets);
        }

        // Targets' glass eases bright, the rest but the source eases dim, so the eye goes to where the balls can go.
        private void Focus(int from, IReadOnlyList<int> targets)
        {
            var b = _animation.Boosters;
            _focused = true;
            for (var t = 0; t < _bottles.Count; t++)
            {
                if (t == from) continue;
                var target = false;
                foreach (var candidate in targets) target |= candidate == t;
                _bottles[t].SetTint(target ? b.FocusTargetTint : b.FocusDimTint, b.FocusTime);
            }
        }

        public void HideTargets()
        {
            StopIdle();
            FadeOutlines();
            if (!_focused) return;
            _focused = false;
            foreach (var bottle in _bottles) bottle.ResetTint(_animation.Boosters.FocusTime);
        }

        // Tap-me idle: target tubes breathe slightly; their balls and slot rims a bit more, bottom first.
        private void StartIdle()
        {
            var b = _animation.Boosters;
            var outlines = _slotOutlines.ToArray();
            var slots = _slotOf.ToArray();
            var shown = 0f;
            PrimeTween.Tween.Custom(_idleClock, 0f, 1f, Mathf.Max(b.IdlePeriod, 0.05f), (_, k) =>
            {
                shown = Mathf.Min(1f, shown + Time.deltaTime / Mathf.Max(b.HintFadeTime, 0.0001f));
                var eased = Mathf.SmoothStep(0f, 1f, shown);
                for (var i = 0; i < outlines.Length; i++)
                {
                    if (outlines[i] == null) continue;
                    var breath = Breath(k - b.IdleBallPhase * slots[i]);
                    ApplyOutline(outlines[i], eased * (1f - b.SlotBreathAlpha * (1f - breath)), 1f + b.SlotBreathScale * breath);
                }
                foreach (var tube in _idleTubes)
                {
                    tube.transform.localScale = Vector3.one * (1f + b.IdleTubeBreath * Breath(k));
                    var balls = tube.Balls;
                    for (var i = 0; i < balls.Count; i++)
                        if (!_flights.ContainsKey(balls[i]))
                            balls[i].transform.localScale = Vector3.one * (1f + b.IdleBallBreath * Breath(k - b.IdleBallPhase * i));
                }
            }, PrimeTween.Ease.Linear, cycles: -1);
        }

        // 0 to 1 and back, eased at both ends.
        private static float Breath(float k) => 0.5f - 0.5f * Mathf.Cos(Mathf.PI * 2f * k);

        private void StopIdle()
        {
            PrimeTween.Tween.StopAll(_idleClock);
            foreach (var tube in _idleTubes)
            {
                if (tube == null) continue;
                tube.transform.localScale = Vector3.one;
                foreach (var ball in tube.Balls)
                    if (!_flights.ContainsKey(ball)) ball.transform.localScale = Vector3.one;
            }
            _idleTubes.Clear();
        }

        // Empty slot drawn like an orb: violet inner glow from the lower right, crisp rim, gloss upper left.
        private void Outline(BottleItem tube, int slot)
        {
            var b = _animation.Boosters;
            var root = new GameObject("SlotRim");
            root.transform.SetParent(tube.transform, false);
            root.transform.localPosition = TubeArt.SlotLocalPosition(slot);
            OutlineLayer(root.transform, b.SlotInner, SlotInnerOrder, TubeArt.BallDiameter / SlotInnerEdge);
            OutlineLayer(root.transform, b.SlotRim, SlotRimOrder, TubeArt.BallDiameter / SlotRimEdge);
            OutlineLayer(root.transform, b.SlotGloss, SlotGlossOrder, TubeArt.BallDiameter / SlotInnerEdge);
            _slotOutlines.Add(root);
            _slotOf.Add(slot);
        }

        private static void OutlineLayer(Transform parent, Sprite sprite, int order, float size)
        {
            var go = new GameObject(sprite != null ? sprite.name : "SlotRimLayer");
            go.transform.SetParent(parent, false);
            var layer = go.AddComponent<SpriteRenderer>();
            layer.sprite = sprite;
            layer.sortingOrder = order;
            layer.color = Color.clear;
            if (sprite != null) go.transform.localScale = Vector3.one * (size / sprite.bounds.size.x);
        }

        private void FadeOutlines()
        {
            if (_slotOutlines.Count == 0) return;
            var outlines = _slotOutlines.ToArray();
            _slotOutlines.Clear();
            _slotOf.Clear();
            foreach (var outline in outlines)
            {
                if (outline == null) continue;
                var from = outline.transform.localScale.x;
                PrimeTween.Tween.Custom(outline.transform, 1f, 0f, Mathf.Max(_animation.Boosters.HintFadeTime, 0.0001f),
                        (t, a) => ApplyOutline(t.gameObject, a, from), PrimeTween.Ease.InQuad)
                    .OnComplete(outline, gone => Destroy(gone));
            }
        }

        private void ClearOutlines()
        {
            foreach (var outline in _slotOutlines)
                if (outline != null) Destroy(outline);
            _slotOutlines.Clear();
            _slotOf.Clear();
        }

        // Reused every frame of the idle, so it allocates nothing.
        private readonly List<SpriteRenderer> _outlineLayers = new();

        private void ApplyOutline(GameObject outline, float alpha, float scale)
        {
            var b = _animation.Boosters;
            outline.transform.localScale = Vector3.one * scale;
            outline.GetComponentsInChildren(_outlineLayers);
            if (_outlineLayers.Count < 3) return;
            _outlineLayers[0].color = Fade(b.SlotInnerColor, alpha);
            _outlineLayers[1].color = Fade(b.SlotRimColor, alpha);
            _outlineLayers[2].color = Fade(b.SlotGlossColor, alpha);
        }

        private static Color Fade(Color color, float alpha) => new(color.r, color.g, color.b, color.a * alpha);
    }
}
