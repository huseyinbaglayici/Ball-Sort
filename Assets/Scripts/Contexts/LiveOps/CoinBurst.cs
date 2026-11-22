using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.LiveOps
{
    // A few coins (or other collectables) fly from a reward to their counter, then land with a punch.
    public static class CoinBurst
    {
        public static void Play(RectTransform from, RectTransform to, Sprite coin, int count = 7, float size = 90f)
        {
            if (from == null || to == null || coin == null) return;
            var layer = (RectTransform)to.root;
            var start = layer.InverseTransformPoint(from.TransformPoint(from.rect.center));
            var end = layer.InverseTransformPoint(to.TransformPoint(to.rect.center));
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("Coin", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(layer, false);
                rect.sizeDelta = Vector2.one * size;
                var image = go.GetComponent<Image>();
                image.sprite = coin;
                image.raycastTarget = false;
                var spread = (Vector3)(Random.insideUnitCircle * 140f);
                rect.localPosition = start;
                var delay = i * 0.05f;
                var last = i == count - 1;
                Sequence.Create(useUnscaledTime: true)
                    .Chain(Tween.LocalPosition(rect, start + spread, 0.22f, Ease.OutCubic, startDelay: delay))
                    .Chain(Tween.LocalPosition(rect, end, 0.38f, Ease.InCubic))
                    .OnComplete(() =>
                    {
                        Object.Destroy(go);
                        if (last && to != null) Tween.PunchScale(to, Vector3.one * 0.12f, 0.2f, useUnscaledTime: true);
                    });
            }
        }
    }
}
