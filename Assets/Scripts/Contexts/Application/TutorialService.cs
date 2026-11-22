using Project.Contexts.Application.Economy;
using Project.Contexts.Game.Levels;

namespace Project.Contexts.Application
{
    // Which first-time intro a level opens with; each one is shown once and saved before it opens.
    public sealed class TutorialService
    {
        // Levels whose board teaches the basics on every play.
        public const int TapLevel = 1;
        public const int SameColorLevel = 2;

        // Obstacles first, the tall tube after them.
        public static readonly Mechanic[] IntroOrder =
            { Mechanic.HiddenBall, Mechanic.FrozenTube, Mechanic.ShutterKey, Mechanic.Bomb, Mechanic.Tower };

        private readonly PlayerData _data;

        public TutorialService(PlayerData data) => _data = data;

        private TutorialModel Model => _data.Model.Tutorial;

        public static bool HasBoardTutorial(int level) => level == TapLevel || level == SameColorLevel;

        public bool IsIntroduced(Mechanic mechanic) => (Model.Mechanics & (int)mechanic) == (int)mechanic;

        public bool IsIntroduced(BoosterType type) => Model.Boosters[(int)type];

        // First new mechanic on the board; met is what earlier levels already had, so older saves skip theirs.
        public Mechanic NextMechanic(Mechanic onBoard, Mechanic met)
        {
            foreach (var mechanic in IntroOrder)
                if ((onBoard & mechanic) != 0 && (met & mechanic) == 0 && !IsIntroduced(mechanic)) return mechanic;
            return Mechanic.None;
        }

        // First booster unlocked by this level and not yet shown.
        public bool TryNextBooster(int level, out BoosterType type)
        {
            foreach (var booster in CampaignRules.AllBoosters)
            {
                if (!BoosterInventory.IsUnlocked(booster, level) || IsIntroduced(booster)) continue;
                type = booster;
                return true;
            }
            type = default;
            return false;
        }

        // A board lesson or a mechanic intro takes the level; a booster waits for the next free one.
        public Intro Pick(int level, Mechanic onBoard, Mechanic met)
        {
            if (!CampaignRules.IsCampaignLevel(level) || HasBoardTutorial(level)) return Intro.None;
            var mechanic = NextMechanic(onBoard, met);
            if (mechanic != Mechanic.None) return Intro.Of(mechanic);
            return TryNextBooster(level, out var type) ? Intro.Of(type) : Intro.None;
        }

        public void MarkShown(Intro intro)
        {
            if (intro.Mechanic != Mechanic.None) Model.Mechanics |= (int)intro.Mechanic;
            else if (intro.IsBooster) Model.Boosters[(int)intro.Booster] = true;
            else return;
            _data.Save();
        }
    }

    public readonly struct Intro
    {
        public readonly Mechanic Mechanic;
        public readonly bool IsBooster;
        public readonly BoosterType Booster;

        private Intro(Mechanic mechanic, bool isBooster, BoosterType booster)
        {
            Mechanic = mechanic;
            IsBooster = isBooster;
            Booster = booster;
        }

        public static Intro None => default;
        public static Intro Of(Mechanic mechanic) => new(mechanic, false, default);
        public static Intro Of(BoosterType type) => new(Mechanic.None, true, type);

        public bool IsNone => Mechanic == Mechanic.None && !IsBooster;
    }
}
