using Project.Contexts.Game.Sort;
using TMPro;
using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // The ribbon's title arrives one letter at a time: each pops in large with a small tilt, settles, then sways on the loop.
    // Drawn from the localized string, so every language keeps its own word.
    public sealed class TitleLetters : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;

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

        // Rebuilds the mesh, then moves each letter's four corners about its own middle.
        private void Apply()
        {
            _text.ForceMeshUpdate();
            var info = _text.textInfo;
            var visible = 0;
            for (var i = 0; i < info.characterCount; i++)
                if (info.characterInfo[i].isVisible) visible++;
            if (visible == 0) return;
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
                Letter(info, c, Pop(local, side, order));
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

        private static void Letter(TMP_TextInfo info, TMP_CharacterInfo c, (float scale, float angle, float lift, float alpha) pose)
        {
            var mesh = info.meshInfo[c.materialReferenceIndex];
            var vertices = mesh.vertices;
            var colors = mesh.colors32;
            var v = c.vertexIndex;
            var centre = (vertices[v] + vertices[v + 2]) * 0.5f;
            var height = vertices[v + 2].y - vertices[v].y;
            var rotation = Quaternion.Euler(0f, 0f, pose.angle);
            var lift = Vector3.up * (pose.lift * height);
            for (var k = 0; k < 4; k++)
            {
                vertices[v + k] = centre + rotation * ((vertices[v + k] - centre) * pose.scale) + lift;
                var color = colors[v + k];
                color.a = (byte)Mathf.RoundToInt(pose.alpha * 255f);
                colors[v + k] = color;
            }
        }

        private static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float InOutSine(float t) => 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(t));
    }
}
