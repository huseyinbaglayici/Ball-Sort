using Project.Contexts.Game.Levels;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.LevelEditor
{
    public static class LevelPreview
    {
        public readonly struct SlotHit
        {
            public readonly int Bottle;
            public readonly int Slot;

            public SlotHit(int bottle, int slot)
            {
                Bottle = bottle;
                Slot = slot;
            }
        }

        private const float BottleWidth = 1.6f;
        private const float SlotHeight = 1f;
        private const float Padding = 12f;

        private static readonly Color Background = new(0.16f, 0.16f, 0.18f);
        private static readonly Color Glass = new(0.32f, 0.32f, 0.36f);
        private static readonly Color Selected = new(1f, 0.85f, 0.2f);
        private static readonly Color HiddenTint = new(0f, 0f, 0f, 0.55f);

        private static GUIStyle _centered;

        public static SlotHit? Draw(Rect area, LevelData level, LevelPalette palette, int selectedBottle)
        {
            EditorGUI.DrawRect(area, Background);
            var bottles = level.Bottles;
            if (bottles.Count == 0) return null;

            _centered ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

            var bottleHeight = level.Capacity * SlotHeight + 0.4f;
            var min = bottles[0].Position;
            var max = min;
            foreach (var bottle in bottles)
            {
                min = Vector2.Min(min, bottle.Position);
                max = Vector2.Max(max, bottle.Position);
            }
            var size = max - min + new Vector2(BottleWidth * 1.3f, bottleHeight * 1.2f);
            var center = (min + max) * 0.5f;
            var scale = Mathf.Min((area.width - Padding * 2f) / size.x, (area.height - Padding * 2f) / size.y);

            SlotHit? hit = null;
            var evt = Event.current;
            for (var b = 0; b < bottles.Count; b++)
            {
                var pos = bottles[b].Position;
                var bottleRect = new Rect(0f, 0f, BottleWidth * scale, bottleHeight * scale)
                {
                    center = new Vector2(area.center.x + (pos.x - center.x) * scale, area.center.y - (pos.y - center.y) * scale)
                };

                if (b == selectedBottle) EditorGUI.DrawRect(Inflate(bottleRect, 3f), Selected);
                EditorGUI.DrawRect(bottleRect, Glass);

                var inner = Inflate(bottleRect, -0.1f * scale);
                var slots = bottles[b].Slots;
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

                GUI.Label(new Rect(bottleRect.x, bottleRect.yMax, bottleRect.width, 16f), (b + 1).ToString(), EditorStyles.centeredGreyMiniLabel);
            }
            return hit;
        }

        private static void DrawSlot(Rect rect, SlotData slot, LevelPalette palette)
        {
            if (slot.IsEmpty) return;
            EditorGUI.DrawRect(rect, palette.Get(slot.Color));
            if (slot.Hidden)
            {
                EditorGUI.DrawRect(rect, HiddenTint);
                GUI.Label(rect, "?", _centered);
            }
            else if (slot.Kind == SlotKind.Special)
            {
                GUI.Label(rect, "★", _centered);
            }
        }

        private static Rect Inflate(Rect rect, float amount) =>
            new(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);
    }
}
