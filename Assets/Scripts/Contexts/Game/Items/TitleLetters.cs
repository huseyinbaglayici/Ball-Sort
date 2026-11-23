using Project.Contexts.Game.Sort;
using TMPro;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // The ribbon's localized title, fitted to the banner face along its arch; letters pop in one by one, then sway.
    public sealed class TitleLetters : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        // Banner face in text units: size, arch rise per unit squared, end shrink and the share the title fills.
        [SerializeField] private Vector2 _face = new(4.8f, 1.27f);
        [SerializeField] private float _arch = 0.043f;
        [SerializeField] private float _falloff = 0.05f;
        [SerializeField] private Vector2 _fill = new(0.92f, 0.95f);

        private WinFxSettings _w;
        private float _startAt;
        private bool _playing;

        // delay: from now to the first letter.
        public void Play(float delay, WinFxSettings w)
        {
            _w = w;
            _startAt = Time.time + delay;
            _playing = true;
            Apply();
        }

        public void Stop() => _playing = false;

        private void LateUpdate()
        {
            if (_playing) Apply();
        }

        // Rebuilds the flat layout, then bends each letter onto the arch about its own middle.
        private void Apply()
        {
            _text.ForceMeshUpdate();
            var info = _text.textInfo;
            var visible = 0;
            var tallest = 0f;
            float left = float.MaxValue, right = float.MinValue;
            for (var i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                visible++;
                tallest = Mathf.Max(tallest, c.topRight.y - c.bottomLeft.y);
                left = Mathf.Min(left, c.bottomLeft.x);
                right = Mathf.Max(right, c.topRight.x);
            }
            if (visible == 0 || tallest <= 0f) return;
            // One scale for the whole word: as tall as the face allows, no wider than it.
            var fit = Mathf.Min(_face.y * _fill.y / tallest, _face.x * _fill.x / Mathf.Max(right - left, 0.0001f));

            var t = Time.time - _startAt;
            var stagger = visible > 1 ? Mathf.Max(_w.LettersTime - _w.LetterPopTime, 0f) / (visible - 1) : 0f;
            var order = 0;
            for (var i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                var local = t - order * stagger;
                var side = order % 2 == 0 ? 1f : -1f;
                order++;
                Letter(info, c, fit, Pop(local, side, order));
            }
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        // Scale, tilt, lift (as a share of the letter's height) and alpha of one letter at its own time.
        private (float scale, float angle, float lift, float alpha) Pop(float local, float side, int index)
        {
            if (local <= 0f) return (0f, 0f, 0f, 0f);
            var pop = Mathf.Max(_w.LetterPopTime, 0.0001f);
            var k = Mathf.Clamp01(local / pop);
            // Up past full size, then back: the overshoot of the arrival.
            var scale = k < 0.55f
                ? Mathf.Lerp(_w.LetterFrom, _w.LetterPeak, OutQuad(k / 0.55f))
                : Mathf.Lerp(_w.LetterPeak, 1f, InOutSine((k - 0.55f) / 0.45f));
            var angle = side * _w.LetterTilt * Mathf.Sin(Mathf.PI * k) * (1f - k);
            var alpha = Mathf.Clamp01(local / (pop * 0.3f));
            if (k < 1f) return (scale, angle, 0f, alpha);
            // Settled: a slow sway, each letter a little behind the one before.
            var period = Mathf.Max(_w.MascotBobPeriod, 0.2f);
            var phase = Mathf.PI * 2f * (local - pop) / period - index * 0.55f;
            return (1f + _w.LetterWave * 0.5f * Mathf.Sin(phase), 0f, _w.LetterWave * Mathf.Sin(phase + 1f), 1f);
        }

        // The letter's middle goes onto the arch at its scaled x and turns to the arch's slope there.
        private void Letter(TMP_TextInfo info, TMP_CharacterInfo c, float fit, (float scale, float angle, float lift, float alpha) pose)
        {
            var mesh = info.meshInfo[c.materialReferenceIndex];
            var vertices = mesh.vertices;
            var colors = mesh.colors32;
            var v = c.vertexIndex;
            var centre = (vertices[v] + vertices[v + 2]) * 0.5f;
            var height = vertices[v + 2].y - vertices[v].y;
            var x = centre.x * fit;
            var slope = -2f * _arch * x;
            var tangent = Mathf.Atan(slope) * Mathf.Rad2Deg;
            var shrink = 1f - _falloff * Mathf.Clamp01(Mathf.Abs(x) / (_face.x * 0.5f));
            var placed = new Vector3(x, centre.y * fit - _arch * x * x, centre.z);
            var rotation = Quaternion.Euler(0f, 0f, tangent + pose.angle);
            var scale = fit * shrink * pose.scale;
            var lift = rotation * Vector3.up * (pose.lift * height * fit);
            for (var k = 0; k < 4; k++)
            {
                vertices[v + k] = placed + rotation * ((vertices[v + k] - centre) * scale) + lift;
                var color = colors[v + k];
                color.a = (byte)Mathf.RoundToInt(pose.alpha * 255f);
                colors[v + k] = color;
            }
        }

        private static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float InOutSine(float t) => 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(t));
    }
}
