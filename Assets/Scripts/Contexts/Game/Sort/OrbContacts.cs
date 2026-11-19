using System;
using System.Collections.Generic;

namespace Project.Contexts.Game.Sort
{
    public enum ContactKind
    {
        Select,
        // Dropped back into its own tube (same tube tapped again, or an invalid target).
        Return,
        Transfer
    }

    public readonly struct OrbContact
    {
        // 1-based, in contact order.
        public readonly int Ball;
        public readonly string Sound;
        public readonly bool Haptic;
        public readonly float GainDb;

        public OrbContact(int ball, string sound, bool haptic, float gainDb)
        {
            Ball = ball;
            Sound = sound;
            Haptic = haptic;
            GainDb = gainDb;
        }
    }

    // Each entry plays on its ball's own animation frame, never as a burst.
    public static class OrbContacts
    {
        public const int MaxBalls = 16;
        public const float FirstOfFourDb = -1.4f;
        public const float OtherDb = -2.8f;
        private const int LongRun = 5;

        // Sound families only; Plan picks the context variants.
        public static List<OrbContact> Basic(ContactKind kind, int selected, int moved = 0)
        {
            Validate(kind, selected, moved);
            var count = kind == ContactKind.Transfer ? moved : selected;
            var contacts = new List<OrbContact>(count);
            for (var i = 0; i < count; i++)
            {
                var last = i == count - 1;
                if (kind == ContactKind.Select) contacts.Add(new OrbContact(i + 1, "select", i == 0, 0f));
                else contacts.Add(new OrbContact(i + 1, last ? "land_last" : "land", kind == ContactKind.Transfer || last, 0f));
            }
            return contacts;
        }

        // empty: target was empty before the move. tower: target is a long tube (8, 12 or 16 slots).
        public static List<OrbContact> Plan(ContactKind kind, int selected, int moved = 0, bool empty = false, bool tower = false)
        {
            var contacts = Basic(kind, selected, moved);
            for (var i = 0; i < contacts.Count; i++)
            {
                var c = contacts[i];
                var sound = c.Sound;
                if (kind == ContactKind.Select)
                    sound = selected == 1 ? "select_single" : i == 0 ? "select" : i == selected - 1 ? "select_tail" : "select_group";
                else if (kind == ContactKind.Transfer)
                {
                    if (i == moved - 1 && moved < selected) sound = "land_partial";
                    else if (i == 0 && empty) sound = "land_empty";
                    else if (i < moved - 1) sound = tower ? "land_tower" : "land_stack";
                }
                contacts[i] = new OrbContact(c.Ball, sound, c.Haptic, Gain(kind, i, contacts.Count));
            }
            return contacts;
        }

        private static float Gain(ContactKind kind, int index, int count)
        {
            // Runs of 5+ are pulled down in groups of four; the last landing closes at full level.
            if (count < LongRun) return 0f;
            if (kind != ContactKind.Select && index == count - 1) return 0f;
            return index % 4 == 0 ? FirstOfFourDb : OtherDb;
        }

        private static void Validate(ContactKind kind, int selected, int moved)
        {
            if (selected < 1 || selected > MaxBalls) throw new ArgumentOutOfRangeException(nameof(selected));
            if (kind == ContactKind.Transfer && (moved < 1 || moved > selected)) throw new ArgumentOutOfRangeException(nameof(moved));
        }
    }
}
