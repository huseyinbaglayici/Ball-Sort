using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Tests
{
    // "capacity|balls" bottom to mouth: color index, ? hidden, kX key to box X, bN bomb fuse N (bN! lit).
    public sealed class BoardBuilder
    {
        private readonly LevelData _level = ScriptableObject.CreateInstance<LevelData>();

        public static BoardBuilder Of(params string[] bottles)
        {
            var builder = new BoardBuilder();
            foreach (var spec in bottles) builder.Add(spec);
            return builder;
        }

        public BoardBuilder Curtain(int bottle, int color, int need)
        {
            _level.Curtains.Add(new CurtainData { Bottle = bottle, Color = color, Need = need });
            return this;
        }

        public BoardBuilder Lockbox(string id, params int[] bottles)
        {
            _level.Lockboxes.Add(new LockboxData { Id = id, Bottles = new(bottles) });
            return this;
        }

        public LevelData Level => _level;

        public SortBoard Build() => new(_level);

        private void Add(string spec)
        {
            var parts = spec.Split('|');
            var bottle = BottleData.CreateEmpty(int.Parse(parts[0]), new Vector2(_level.Bottles.Count, 0f));
            var balls = parts.Length > 1 ? parts[1].Split(' ', System.StringSplitOptions.RemoveEmptyEntries) : new string[0];
            for (var s = 0; s < balls.Length; s++) bottle.Slots[s] = Parse(balls[s]);
            _level.Bottles.Add(bottle);
        }

        private static SlotData Parse(string token)
        {
            var slot = new SlotData();
            var i = 0;
            if (token[0] == '?')
            {
                slot.Kind = SlotKind.Special;
                i = 1;
            }
            var start = i;
            while (i < token.Length && char.IsDigit(token[i])) i++;
            slot.Color = int.Parse(token.Substring(start, i - start));
            while (i < token.Length)
            {
                var tag = token[i++];
                start = i;
                if (tag == 'k')
                {
                    while (i < token.Length && char.IsLetter(token[i]) && token[i] != 'b') i++;
                    slot.Key = token.Substring(start, i - start);
                }
                else if (tag == 'b')
                {
                    while (i < token.Length && char.IsDigit(token[i])) i++;
                    slot.Fuse = int.Parse(token.Substring(start, i - start));
                    if (i < token.Length && token[i] == '!')
                    {
                        slot.FuseLit = true;
                        i++;
                    }
                }
            }
            return slot;
        }
    }
}
