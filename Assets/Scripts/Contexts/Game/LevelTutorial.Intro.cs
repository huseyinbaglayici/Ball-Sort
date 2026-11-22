using Project.Common.UI.PopUps;
using Project.Contexts.Application;
using Project.Contexts.Application.Economy;
using Project.Contexts.Game.Levels;
using Project.Core.Services;

namespace Project.Contexts.Game
{
    // The one-time card for a new item or booster: which card a level gets and what it says.
    public sealed partial class LevelTutorial
    {
        public void ShowIntro(Intro intro)
        {
            if (intro.IsNone || !Services.TryGet<PopupService>(out var popups)) return;
            popups.Show(_introPopup, intro.IsBooster ? BoosterCard(intro.Booster) : MechanicCard(intro.Mechanic));
        }

        private static IntroArgs MechanicCard(Mechanic mechanic) => mechanic switch
        {
            Mechanic.HiddenBall => new IntroArgs("intro.new_item", "intro.hidden", "help.hidden", "Hidden"),
            Mechanic.FrozenTube => new IntroArgs("intro.new_item", "intro.frozen", "help.frozen", "Frozen"),
            Mechanic.ShutterKey => new IntroArgs("intro.new_item", "intro.shutter", "help.shutter", "Shutter"),
            Mechanic.Bomb => new IntroArgs("intro.new_item", "intro.bomb", "help.bomb", "Bomb"),
            _ => new IntroArgs("intro.new_item", "intro.tower", "help.tower", "Tower")
        };

        private static IntroArgs BoosterCard(BoosterType type) => type switch
        {
            BoosterType.Undo => new IntroArgs("intro.new_booster", "booster.undo", "intro.undo", "Undo"),
            BoosterType.ExtraTube => new IntroArgs("intro.new_booster", "booster.extra_tube", "intro.extra_tube", "ExtraTube"),
            _ => new IntroArgs("intro.new_booster", "booster.free_move", "intro.free_move", "FreeMove")
        };
    }
}
