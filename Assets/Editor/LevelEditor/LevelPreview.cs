using Project.Contexts.Game.Levels;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.LevelEditor
{
    // Draws the layout as the game reads it: tube heights, hidden balls, keys, bombs, frozen tubes and lockboxes.
    public static class LevelPreview
    {
        public readonly struct SlotHit
        {
            public readonly int Bottle;
            // -1 when the tube was picked without painting (right click).
            public readonly int Slot;

            public SlotHit(int bottle, int slot)
            {
                Bottle = bottle;
                Slot = slot;
            }

            public bool Paints => Slot >= 0;
        }

        private const float BottleWidth = 1.6f;
        private const float SlotHeight = 1f;
        private const float Padding = 18f;

        private static readonly Color Background = new(0.16f, 0.16f, 0.18f);
        private static readonly Color Glass = new(0.32f, 0.32f, 0.36f);
        private static readonly Color Selected = new(1f, 0.85f, 0.2f);
        private static readonly Color HiddenTint = new(0f, 0f, 0f, 0.55f);
        private static readonly Color Ice = new(0.55f, 0.85f, 1f, 0.35f);
        private static readonly Color Box = new(0.1f, 0.05f, 0.2f, 0.55f);

        private static GUIStyle _centered;
        private static GUIStyle _tag;

        public static SlotHit? Draw(Rect area, LevelData level, LevelPalette palette, int selectedBottle)
        {
            EditorGUI.DrawRect(area, Background);
            var bottles = level.Bottles;
            if (bottles.Count == 0) return null;

            _centered ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _tag ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

            // Positions are the tube bottoms; each tube rises by its own capacity.
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var bottle in bottles)
            {
                min = Vector2.Min(min, bottle.Position - new Vector2(BottleWidth * 0.5f, 0.6f));
                max = Vector2.Max(max, bottle.Position + new Vector2(BottleWidth * 0.5f, Height(bottle) + 0.6f));
            }
            var size = max - min;
            var center = (min + max) * 0.5f;
            var scale = Mathf.Min((area.width - Padding * 2f) / size.x, (area.height - Padding * 2f) / size.y);

            SlotHit? hit = null;
            var evt = Event.current;
            for (var b = 0; b < bottles.Count; b++)
            {
                var bottle = bottles[b];
                var height = Height(bottle) * scale;
                var bottom = new Vector2(area.center.x + (bottle.Position.x - center.x) * scale, area.center.y - (bottle.Position.y - center.y) * scale);
                var bottleRect = new Rect(bottom.x - BottleWidth * scale * 0.5f, bottom.y - height, BottleWidth * scale, height);

                if (b == selectedBottle) EditorGUI.DrawRect(Inflate(bottleRect, 3f), Selected);
                EditorGUI.DrawRect(bottleRect, Glass);

                var inner = Inflate(bottleRect, -0.1f * scale);
                var slots = bottle.Slots;
                var slotPx = inner.height / Mathf.Max(1, slots.Count);
                for (var s = 0; s < slots.Count; s++)
                {
                    var slotRect = new Rect(inner.x, inner.yMax - (s + 1) * slotPx, inner.width, slotPx);
                    DrawSlot(Inflate(slotRect, -1f), slots[s], palette);
                    if (evt.type == EventType.MouseDown && evt.button == 0 && slotRect.Contains(evt.mousePosition))
                    {
                        hit = new SlotHit(b, s);
                        evt.Use();
                    }
                }

                DrawObstacles(level, b, bottleRect, palette);
                var label = new Rect(bottleRect.x - 10f, bottleRect.yMax, bottleRect.width + 20f, 16f);
                GUI.Label(label, $"{b + 1} · {bottle.Capacity}", EditorStyles.centeredGreyMiniLabel);
                if (evt.type == EventType.MouseDown && evt.button == 1 && Inflate(bottleRect, 4f).Contains(evt.mousePosition))
                {
                    hit = new SlotHit(b, -1);
                    evt.Use();
                }
            }
            return hit;
        }

        private static float Height(BottleData bottle) => bottle.Capacity * SlotHeight + 0.4f;

        // Ice over a frozen tube with what melts it; a lid over a lockbox tube with its id.
        private static void DrawObstacles(LevelData level, int bottle, Rect rect, LevelPalette palette)
        {
            foreach (var curtain in level.Curtains)
            {
                if (curtain.Bottle != bottle) continue;
                EditorGUI.DrawRect(rect, Ice);
                var badge = new Rect(rect.x, rect.y - 18f, rect.width, 16f);
                EditorGUI.DrawRect(badge, curtain.Color >= 0 && curtain.Color < palette.Count ? palette.Get(curtain.Color) : Color.gray);
                GUI.Label(badge, $"ICE {curtain.Need}", _tag);
            }
            foreach (var box in level.Lockboxes)
            {
                if (!box.Bottles.Contains(bottle)) continue;
                EditorGUI.DrawRect(rect, Box);
                GUI.Label(new Rect(rect.x, rect.y + 2f, rect.width, 16f), $"BOX {box.Id}", _tag);
            }
        }

        private static void DrawSlot(Rect rect, SlotData slot, LevelPalette palette)
        {
            if (slot.IsEmpty) return;
            EditorGUI.DrawRect(rect, slot.Color < palette.Count ? palette.Get(slot.Color) : Color.magenta);
            if (slot.Kind == SlotKind.Special || slot.Hidden)
            {
                EditorGUI.DrawRect(rect, HiddenTint);
                GUI.Label(rect, "?", _centered);
            }
            if (slot.IsKey) GUI.Label(rect, "K" + slot.Key, _centered);
            if (slot.IsBomb) GUI.Label(rect, slot.FuseLit ? $"B{slot.Fuse}!" : $"B{slot.Fuse}", _centered);
        }

        private static Rect Inflate(Rect rect, float amount) =>
            new(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);
    }
}
