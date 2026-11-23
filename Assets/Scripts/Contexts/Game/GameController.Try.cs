using Project.Common.UI.PopUps;
using Project.Contexts.Application.Economy;
using Project.Contexts.Main;
using Project.Core.Services;

namespace Project.Contexts.Game
{
    // Ending a try early: restart, quit to Home and the out-of-lives stop.
    public sealed partial class GameController
    {
        // Costs a life when the try had a move.
        private void Restart()
        {
            if (_states.CurrentKey != GameState.Playing || _winPending || _leaving) return;
            if (_tryHasMoves && !_lives.TrySpend())
            {
                ShowOutOfLives();
                return;
            }
            _ads.EndActivePlay();
            if (_tryHasMoves)
            {
                _progress.FailAttempt();
                _ads.CountLoss(_level);
                _audio.PlayOneShotSound("retry");
            }
            _states.ChangeState(GameState.Playing);
        }

        // Leaving now would cost a life, so it asks first.
        private void RequestQuit()
        {
            // The winning move is already made; its result comes next.
            if (_states.CurrentKey == GameState.Playing && _winPending) return;
            var inTry = _states.CurrentKey is GameState.Playing or GameState.Lost;
            var costsLife = inTry && _tryHasMoves && !_lives.IsUnlimited && _lives.Count > 0;
            if (!costsLife)
            {
                Quit();
                return;
            }

            var difficulty = CampaignRules.DifficultyOf(_level);
            _popups.Show(_messagePopup, new MessageArgs("quit.title", _loc.Get("quit.body"),
                "quit.exit", Quit, closable: true, header: EconomyText.Difficulty(difficulty, _loc),
                headerMarks: EconomyText.DifficultyMarks(difficulty), heart: true));
        }

        // A try with a move costs a life if one is left.
        private void Quit()
        {
            if (_states.CurrentKey == GameState.Lost)
            {
                _lost.Leave();
                return;
            }
            if (_states.CurrentKey == GameState.Playing)
            {
                if (_winPending || _leaving) return;
                _leaving = true;
                if (_tryHasMoves)
                {
                    _lives.TrySpend();
                    _progress.FailAttempt();
                }
            }
            _ads.EndActivePlay();
            _ = Services.Get<MainFlow>().GoHomeAsync();
        }

        private void ShowOutOfLives()
        {
            _popups.Show(_messagePopup, new MessageArgs("lives.out_title",
                _loc.Format("lives.out_body", EconomyText.Timer(_lives.NextLifeIn)), "common.home",
                () =>
                {
                    _leaving = true;
                    _ = Services.Get<MainFlow>().GoHomeAsync();
                }));
        }
    }
}
