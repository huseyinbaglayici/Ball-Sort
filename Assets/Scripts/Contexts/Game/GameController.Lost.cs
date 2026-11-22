using Project.Contexts.Application.Economy;
using Project.Contexts.Game.Sort;
using Project.Contexts.LiveOps;
using Project.Contexts.Main;
using Project.Core.Haptics;
using Project.Core.Services;
using Project.Core.StateMachine;
using UnityEngine;

namespace Project.Contexts.Game
{
    // The fail screen: a readable pause, a rescue search, then Undo, Continue, Retry or Home.
    public sealed partial class GameController
    {
        private void CancelRescueSearch()
        {
            if (_rescueSearch == null) return;
            _rescueSearch.Cancel();
            _rescueSearch.Dispose();
            _rescueSearch = null;
        }

        // Doesn't end the try or cost a life; a search on a board copy decides the Undo and Continue offers.
        private sealed class LostState : State
        {
            private readonly GameController _c;
            private RescueReport _report;
            private FailWindow _window;
            private float _enteredAt;
            private float _searchMs;
            public LostState(GameController controller) => _c = controller;

            // Input and the play clock stop here; the panel waits for the readable window and the search, in parallel.
            public override void Enter()
            {
                _c.Deselect();
                _c._ads.EndActivePlay();
                // The explosion's own frame gets the one strong pulse; a stuck board's comes with the panel.
                if (_c._model.IsFailed) _c._haptics.Haptic(HapticModule.HapticType.Heavy);
                _report = null;
                _enteredAt = Time.time;
                _c._resumed = false;
                var left = _c._readableAt + _c._board.Animation.FailReadableWindow - Time.time;
                _window = new FailWindow(_c._progress.TryId, _c._model.Version, left);
                Analyze();
            }

            public override void Tick(float deltaTime)
            {
                if (_window == null) return;
                _window.Tick(_c._resumed ? 0f : deltaTime);
                _c._resumed = false;
                if (!_window.TryOpen(_c._progress.TryId, _c._model.Version)) return;
                var bomb = _c._model.IsFailed;
                if (!bomb)
                {
                    _c._audio.PlayOneShotSound("fail");
                    _c._haptics.Haptic(HapticModule.HapticType.Heavy);
                    _c._board.PlayResult(false);
                }
                if (Debug.isDebugBuild)
                    Debug.Log($"[Fail] cause={(bomb ? "bomb" : "no_move")} last={LastMechanic(_c._lastMove)} " +
                              $"wait={(Time.time - _enteredAt) * 1000f:0}ms search={_searchMs:0}ms frame={Time.frameCount}");
                Show();
            }

            public override void Exit()
            {
                _window = null;
                _c.CancelRescueSearch();
            }

            private static string LastMechanic(MoveResult move) =>
                move == null ? "none" : move.Exploded ? "blast" : move.LockboxesOpened.Count > 0 ? "box"
                : move.CurtainsLifted.Count > 0 ? "curtain" : move.Revealed.Count > 0 ? "hidden" : "landing";

            private async void Analyze()
            {
                _c.CancelRescueSearch();
                var search = new System.Threading.CancellationTokenSource();
                _c._rescueSearch = search;
                RescueReport report;
                var started = Time.realtimeSinceStartup;
                var window = _window;
                try
                {
                    report = await RescueAdvisor.AnalyzeAsync(_c._model, RescueAdvisor.DefaultTimeoutMs, search.Token);
                }
                catch (System.OperationCanceledException)
                {
                    return;
                }
                if (search.IsCancellationRequested || _c._states.CurrentKey != GameState.Lost || window != _window) return;
                _searchMs = (Time.realtimeSinceStartup - started) * 1000f;
                _report = report;
                window.SearchDone();
            }

            // Also shown again after a refused payment.
            private void Show()
            {
                if (_c._states.CurrentKey != GameState.Lost) return;
                var loc = _c._loc;
                var lives = _c._lives;
                var bomb = _c._model.IsFailed;
                var options = new System.Collections.Generic.List<ResultOption>();
                // One short reason line; the buttons carry prices and costs.
                var note = new System.Text.StringBuilder(loc.Get(bomb ? "fail.bomb" : "fail.no_move"));

                if (bomb && _report != null && _report.UndoWins)
                {
                    var stock = _c._inventory.Stock(BoosterType.Undo);
                    options.Add(new ResultOption(loc.Get("booster.undo"), UndoLastMove, _c._inventory.CanUse(BoosterType.Undo, _c._level),
                        price: stock > 0 ? 0 : BoosterInventory.Price(BoosterType.Undo), badge: stock));
                }

                var offer = _report?.Offer;
                if (offer != null && !_c._model.ContinueUsed && !_c._continue.UsedInTry(_c._progress.TryId))
                {
                    options.Add(new ResultOption(loc.Get("common.continue"), () => BuyContinue(offer), _c._continue.CanAfford,
                        price: ContinueService.Price));
                    note.Append('\n').Append(loc.Format("continue.gives", ContinueService.Bombs(offer, _c._model, loc)));
                }

                if (lives.CanPlay)
                    options.Add(new ResultOption(loc.Get(lives.IsUnlimited ? "common.retry" : "game.restart"), Retry,
                        lifeCost: !lives.IsUnlimited));
                else
                {
                    note.Append('\n').Append(loc.Format("lives.out_body", EconomyText.Timer(lives.NextLifeIn)));
                    if (_c._ads.CanOfferRewardedLife)
                        options.Add(new ResultOption(loc.Get("lives.ad"), () => _c._ads.ShowRewardedLife(_ => Show())));
                }

                // The event race warns that leaving without a win drops its streak gear.
                var rush = Services.Get<OrbitRushService>();
                if (rush.State == OrbitRushState.Racing && rush.Gear > 0)
                    note.Append('\n').Append(loc.Format("rush.lose_streak", rush.Multiplier));

                // Home is the popup's X; the first option is the main button.
                _c._popups.Show(_c._resultPopup, new ResultArgs(options, note.ToString(), Leave));
            }

            // Bomb fail: pay one Undo (stock, else coins), then take back the exploding transfer.
            private void UndoLastMove()
            {
                if (!_c.TryPay(BoosterType.Undo))
                {
                    Show();
                    return;
                }
                _c._resumeTry = true;
                _c._states.ChangeState(GameState.Playing);
                _c.UndoMove(_c.PlayUndoFeedback);
            }

            private void BuyContinue(ContinueOffer offer)
            {
                var last = _c._lastMove;
                var result = _c._continue.TryBuy(_c._model, offer, _c._level, _c._progress.TryId, out var tube);
                if (result != ContinueResult.Applied)
                {
                    if (result == ContinueResult.Stale) _report = null;
                    Show();
                    return;
                }

                _c._audio.PlayOneShotSound("continue");
                _c._resumeTry = true;
                _c._states.ChangeState(GameState.Playing);
                // A rewound explosion flies its balls back first; then the tube is laid out, which also syncs the obstacles.
                void AddTube() => _c._board.AddBottle(_c._model, tube, null);
                if (offer.Rewind && last != null)
                {
                    _c._board.Transfer(last.To, last.From, last.Count, AddTube, silent: true);
                    // As in Undo: the rewound balls show their restored state while they fly back.
                    _c._board.Sync(_c._model);
                }
                else
                {
                    AddTube();
                }
                _c._lastMove = null;
                _c.RefreshHud();
            }

            private void Retry()
            {
                if (!_c._lives.TrySpend())
                {
                    _c.ShowOutOfLives();
                    return;
                }
                _c._progress.FailAttempt();
                _c._audio.PlayOneShotSound("retry");
                _c._states.ChangeState(GameState.Playing);
            }

            // Once only: a second tap while Home opens must not take another life.
            private void Leave()
            {
                if (_c._leaving) return;
                _c._leaving = true;
                if (_c._tryHasMoves) _c._lives.TrySpend();
                _c._progress.FailAttempt();
                _ = Services.Get<MainFlow>().GoHomeAsync();
            }
        }
    }
}
