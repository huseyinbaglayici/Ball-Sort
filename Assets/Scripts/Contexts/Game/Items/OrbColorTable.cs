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

            public Entry(string name, string surface, bool dark)
            {
                Name = name;
                Surface = ColorUtility.TryParseHtmlString("#" + surface, out var color) ? color : Color.magenta;
                Dark = dark;
            }
        }

        // Lock colors are handed out in this order.
        public static readonly Entry[] Entries =
        {
            new("LightBlue", "5DC1FF", false), new("DarkBlue", "2955DD", true), new("Orange", "EC7C25", false),
            new("LightGreen", "A0C338", false), new("DarkGreen", "2F6016", true), new("White", "8086B9", false),
            new("Wine", "8E1667", true), new("Brown", "A44734", true), new("Yellow", "FFD553", false),
            new("Purple", "963FCC", true), new("Pink", "F5A6DA", false), new("Red", "E23936", false)
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
