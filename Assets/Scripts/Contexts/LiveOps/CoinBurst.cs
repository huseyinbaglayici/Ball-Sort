using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Project.Contexts.LiveOps
{
    // A few coins (or other collectables) fly from a reward to their counter, then land with a punch.
    public static class CoinBurst
    {
        private const float Stagger = 0.05f;
        private const float SpreadTime = 0.22f;
        private const float HomeTime = 0.38f;

        // From the first coin leaving to the last one landing.
        public static float Duration(int count) => Mathf.Max(count - 1, 0) * Stagger + SpreadTime + HomeTime;

        public static void Play(RectTransform from, RectTransform to, Sprite coin, int count = 7, float size = 90f,
            Action<int> landed = null)
        {
            if (from == null || to == null || coin == null) return;
            var layer = (RectTransform)to.root;
            Fly(layer, layer.InverseTransformPoint(from.TransformPoint(from.rect.center)), to, coin, count, size, landed);
        }

        // From a point in the world (seen by camera) to a counter on a screen canvas.
        public static void PlayFromWorld(Vector3 world, Camera camera, RectTransform to, Sprite coin, int count = 7,
            float size = 90f, Action<int> landed = null)
        {
            if (camera == null || to == null || coin == null) return;
            var layer = (RectTransform)to.root;
            var canvas = layer.GetComponent<Canvas>();
            var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, camera.WorldToScreenPoint(world), uiCamera, out var start);
            Fly(layer, start, to, coin, count, size, landed);
        }

        // landed gets each coin's index as it arrives.
        private static void Fly(RectTransform layer, Vector3 start, RectTransform to, Sprite coin, int count, float size,
            Action<int> landed)
        {
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
                var delay = i * Stagger;
                var index = i;
                var last = i == count - 1;
                Sequence.Create(useUnscaledTime: true)
                    .Chain(Tween.LocalPosition(rect, start + spread, SpreadTime, Ease.OutCubic, startDelay: delay))
                    .Chain(Tween.LocalPosition(rect, end, HomeTime, Ease.InCubic))
                    .OnComplete(() =>
                    {
                        Object.Destroy(go);
                        landed?.Invoke(index);
                        if (last && to != null) Tween.PunchScale(to, Vector3.one * 0.12f, 0.2f, useUnscaledTime: true);
                    });
            }
        }
    }
}
