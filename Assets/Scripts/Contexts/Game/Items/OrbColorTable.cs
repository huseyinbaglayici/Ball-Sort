using UnityEngine;

namespace Project.Contexts.Game.Items
{
    // The art's color table: Surface tints obstacle parts drawn in white.
    public static class OrbColorTable
    {
        public readonly struct Entry
        {
            public readonly string Name;
            public readonly Color Surface;
            // Lightened when it tints a small part, so it still reads.
            public readonly bool Dark;
            // Orb shader brightness and saturation scales, so neighbouring hues keep apart in luminance too.
            public readonly float Shade;
            public readonly float Saturation;

            public Entry(string name, string surface, bool dark, float shade = 1f, float saturation = 1f)
            {
                Name = name;
                Surface = ColorUtility.TryParseHtmlString("#" + surface, out var color) ? color : Color.magenta;
                Dark = dark;
                Shade = shade;
                Saturation = saturation;
            }
        }

        // Lock colors are handed out in this order.
        public static readonly Entry[] Entries =
        {
            new("LightBlue", "5DC1FF", false, 0.92f), new("DarkBlue", "2955DD", true), new("Orange", "EC7C25", false, 1.08f),
            new("LightGreen", "A0C338", false), new("DarkGreen", "2F6016", true), new("White", "8086B9", false, 1.35f, 0.75f),
            new("Wine", "8E1667", true, 0.82f), new("Brown", "A44734", true, 0.68f, 0.85f), new("Yellow", "FFD553", false),
            new("Purple", "963FCC", true, 1.18f), new("Pink", "F5A6DA", false), new("Red", "E23936", false)
        };

        // Palette names that differ from the table's.
        public static string Canonical(string name) => name == "Violet" ? "Purple" : name;

        public static int IndexOf(string name)
        {
            name = Canonical(name);
            for (var i = 0; i < Entries.Length; i++)
                if (Entries[i].Name == name) return i;
            return -1;
        }
    }
}
