using Project.Contexts.Application;
using Project.Contexts.Application.Economy;
using Project.Contexts.Game.Items;
using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Sort;
using Project.Contexts.Main;
using Project.Core.Audio;
using Project.Core.Debugging;
using Project.Core.Events;
using Project.Core.Haptics;
using Project.Core.Localization;
using Project.Core.Pooling;
using Project.Core.Services;
using Project.Core.StateMachine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Project.Common.UI;
using Project.Common.UI.PopUps;

namespace Project.Contexts.Game
{
    // A try with a move that ends unwon costs a life; the fail screen doesn't, since Undo or Continue can resume it.
    public sealed class GameController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private ItemRegistry _items;
        [SerializeField] private LevelCatalog _catalog;
        [SerializeField] private LevelPalette _palette;

        [Header("Scene")]
        [SerializeField] private BoardView _board;

        [Header("UI")]
        [SerializeField] private GameObject _hud;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _movesText;
        [SerializeField] private ResultPopup _resultPopup;
        [SerializeField] private MessagePopup _messagePopup;
        [SerializeField] private Button _homeButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private BoosterBar _boosters;
        [SerializeField] private DifficultyTag _difficulty;
        [SerializeField] private SettingsMenu _settings;

        // Empty two-slot tube, at most two per try.
        private const int ExtraTubeCapacity = 2;
        private const int MaxExtraTubes = 2;
        // Undo's feel: a clear hit as the balls settle back, then a light one.
        private const float UndoSecondHitDelay = 0.08f;

        private readonly StateMachine<GameState> _states = new();
        // Mechanic outcome sounds of the move that just landed (filled by MechanicSounds, played in order).
        private readonly System.Collections.Generic.List<string> _soundEvents = new();
        private ItemFactory _factory;
        private ProgressService _progress;
        private LivesService _lives;
        private MonetizationService _ads;
        private LocalizationService _loc;
        private AudioManager _audio;
        private HapticModule _haptics;
        private SortBoard _model;
        private BottleItem _selected;
        // The winning move is made in the model; input waits until its last ball lands.
        private bool _winPending;
        // Set when a move's last ball lands; checked once the board is still. Undo keeps it.
        private bool _stuckCheckPending;
        private int _level;
        // At least one move was made in this try (undo does not clear it); ending the try then costs a life.
        private bool _tryHasMoves;
        private bool _rewardedWatched;
        private BoosterInventory _inventory;
        private int _extraTubes;
        // Armed Free Move: the next transfer onto a different open color uses it and pays only then.
        private bool _freeArmed;
        private ContinueService _continue;
        private PopupService _popups;
        // The last transfer made, so a Continue that rewinds an explosion can fly its balls back.
        private MoveResult _lastMove;
        // The next PlayingState.Enter resumes this try (after a fail-screen Undo or Continue) instead of a new one.
        private bool _resumeTry;
        private System.Threading.CancellationTokenSource _rescueSearch;

        private void Awake()
        {
            _progress = Services.Get<ProgressService>();
            _inventory = Services.Get<BoosterInventory>();
            if (_boosters != null) _boosters.Pressed += OnBoosterPressed;
            _lives = Services.Get<LivesService>();
            _ads = Services.Get<MonetizationService>();
            _loc = Services.Get<LocalizationService>();
            _continue = Services.Get<ContinueService>();
            _popups = Services.Get<PopupService>();
            if (_homeButton != null) _homeButton.onClick.AddListener(Quit);
            if (_restartButton != null) _restartButton.onClick.AddListener(Restart);
            if (_settings != null) _settings.ExitPressed += RequestQuit;
            _audio = Services.Get<AudioManager>();
            _haptics = Services.Get<HapticModule>();
            _factory = new ItemFactory(Services.Get<PoolService>(), _items);
            _factory.Prewarm();

            _states.Add(GameState.Idle, new IdleState(this))
                   .Add(GameState.Playing, new PlayingState(this))
                   .Add(GameState.Won, new WonState(this))
                   .Add(GameState.Lost, new LostState(this));
            _states.ChangeState(GameState.Idle);
        }

        private void OnDestroy()
        {
            if (_boosters != null) _boosters.Pressed -= OnBoosterPressed;
            if (_homeButton != null) _homeButton.onClick.RemoveListener(Quit);
            if (_restartButton != null) _restartButton.onClick.RemoveListener(Restart);
            if (_settings != null) _settings.ExitPressed -= RequestQuit;
        }

        public void Enter()
        {
            _hud.SetActive(true);
            _states.ChangeState(GameState.Playing);
        }

        public void Exit() => _states.ChangeState(GameState.Idle);

        private void OnEnable()
        {
            EventBus<BottleTapped>.Subscribe(OnBottleTapped);
            DebugPanel.AddAction("Undo move", Undo);
            DebugPanel.AddAction("Restart level", Restart);
            DebugPanel.AddAction("Win level", Win);
            DebugPanel.AddAction("Lose level", Lose);
        }

        private void OnDisable()
        {
            EventBus<BottleTapped>.Unsubscribe(OnBottleTapped);
            DebugPanel.RemoveAction("Undo move");
            DebugPanel.RemoveAction("Restart level");
            DebugPanel.RemoveAction("Win level");
            DebugPanel.RemoveAction("Lose level");
        }

        private void Update() => _states.Tick(Time.deltaTime);

        private void BeginLevel()
        {
            _level = _progress.Level;
            // Each retry shows the next layout variant.
            var data = _catalog != null ? _catalog.Get(_level, _progress.PickVariant(_catalog.VariantCount(_level))) : null;
            if (data == null)
            {
                Debug.LogError("[Game] No level found. Import levels, then run Tools > Project > Rebuild Level Catalog.");
                return;
            }

            _model = new SortBoard(data);
            _selected = null;
            _winPending = false;
            _stuckCheckPending = false;
            _tryHasMoves = false;
            _extraTubes = 0;
            _freeArmed = false;
            _lastMove = null;
            _board.Build(data, _level, _model, _factory, _palette);
            _ads.BeginActivePlay();
            RefreshHud();
        }

        private void OnBottleTapped(BottleTapped evt)
        {
            if (_winPending || _model == null || _states.CurrentKey != GameState.Playing) return;

            var tapped = evt.Bottle;
            if (_selected == null)
            {
                if (CanPick(tapped.Index)) Select(tapped);
                else _board.Refuse(tapped.Index);
                return;
            }

            if (_selected == tapped)
            {
                Deselect();
                return;
            }

            var from = _selected.Index;
            var to = tapped.Index;
            // With Free Move on, a target the normal rules allow still takes a normal move, which uses no charge.
            var free = _freeArmed && !_model.CanMove(from, to) && _model.CanFreeMove(from, to);
            if (_board.IsLocked(to) || !(free || _model.CanMove(from, to)))
            {
                // Illegal target: the group drops back and the tube shakes. Not a move; an armed Free Move stays armed.
                Deselect();
                _board.Reject(to);
                return;
            }

            // Free Move is paid only now, once the board accepts the transfer.
            if (free && !_inventory.TryUse(BoosterType.FreeMove, _level, out _))
            {
                Deselect();
                _freeArmed = false;
                ShowNotEnoughCoins(BoosterType.FreeMove);
                RefreshHud();
                return;
            }

            // The model moves at once; the view catches up. The lifted balls keep their pose and fly from there.
            _selected = null;
            MoveResult move;
            if (free) _model.TryFreeMove(from, to, out move);
            else _model.TryMove(from, to, out move);
            if (free) _freeArmed = false;
            _tryHasMoves = true;
            _lastMove = move;
            _board.Sync(_model);
            var reveal = MechanicSounds.OnMove(move);
            if (reveal != null) _audio.PlayOneShotSound(reveal);
            _winPending = _model.IsSolved();
            // Sounds and haptics of the move are played by the board, one per ball on its own landing frame.
            _board.Transfer(move.From, move.To, move.Count, () =>
            {
                if (free) _audio.PlayOneShotSound("free_commit");
                OnMoveLanded(move);
            });
            _board.AssertMatches(_model);
            RefreshHud();
        }

        // Never runs for a move whose flight was cancelled.
        private void OnMoveLanded(MoveResult move)
        {
            foreach (var bottle in move.Completed)
                if (_model.IsCompleted(bottle)) _board.SetCompleted(bottle, true);
            // The moment's mechanic sounds; Undo never comes through here, so rewound effects stay silent.
            _soundEvents.Clear();
            MechanicSounds.OnLanded(move, _model, _winPending, _soundEvents);
            foreach (var id in _soundEvents) _audio.PlayOneShotSound(id);
            if (_winPending) _states.ChangeState(GameState.Won);
            // A bomb whose fuse ran out ends the try once the move is seen.
            else if (_model.IsFailed) _states.ChangeState(GameState.Lost);
            else _stuckCheckPending = true;
        }

        // IsLocked: a ball is flying into or out of it.
        private bool CanPick(int bottle) => _model.CanSelectAsOrigin(bottle) && !_board.IsLocked(bottle);

        private void Select(BottleItem bottle)
        {
            _selected = bottle;
            // The board plays the lift sounds and the one selection haptic.
            _board.Select(bottle.Index, _model.TopRun(bottle.Index));
        }

        private void Deselect()
        {
            if (_selected == null) return;
            _board.Deselect(_selected.Index);
            _selected = null;
        }

        // The debug panel's free Undo.
        private void Undo()
        {
            if (CanUseBoosters) UndoMove(null);
        }

        // Works mid-flight: flying balls turn back from where they are; rewound obstacles don't replay effects.
        private void UndoMove(System.Action onSettled)
        {
            Deselect();
            if (!_model.TryUndo(out var move)) return;

            // Undo never replays the move's landing sounds or haptics; its own feedback comes from onSettled.
            _board.Transfer(move.To, move.From, move.Count, onSettled, silent: true);
            _board.Sync(_model);
            _board.AssertMatches(_model);
            RefreshHud();
        }


        private bool CanUseBoosters => !_winPending && _model != null && _states.CurrentKey == GameState.Playing;

        private void OnBoosterPressed(BoosterType type)
        {
            if (!CanUseBoosters || !BoosterInventory.IsUnlocked(type, _level)) return;
            switch (type)
            {
                case BoosterType.Undo: UseUndo(); break;
                case BoosterType.ExtraTube: UseExtraTube(); break;
                case BoosterType.FreeMove: ToggleFreeMove(); break;
            }
            RefreshHud();
        }

        // Stock first, then coins; nothing is paid when the board can't take the action.
        private bool TryPay(BoosterType type)
        {
            if (_inventory.TryUse(type, _level, out _)) return true;
            ShowNotEnoughCoins(type);
            return false;
        }

        private void UseUndo()
        {
            if (!_model.CanUndo)
            {
                _boosters.Refuse(BoosterType.Undo);
                return;
            }
            if (!TryPay(BoosterType.Undo)) return;
            UndoMove(PlayUndoFeedback);
        }

        private async void PlayUndoFeedback()
        {
            _audio.PlayOneShotSound("undo");
            _haptics.Haptic(HapticModule.HapticType.Medium);
            await Awaitable.WaitForSecondsAsync(UndoSecondHitDelay);
            _haptics.Haptic(HapticModule.HapticType.Light);
        }

        // Waits until no ball is flying, since the board is laid out again; undo history restarts.
        private void UseExtraTube()
        {
            if (_extraTubes >= MaxExtraTubes || _board.IsAnimating)
            {
                _boosters.Refuse(BoosterType.ExtraTube);
                return;
            }
            if (!TryPay(BoosterType.ExtraTube)) return;

            Deselect();
            var index = _model.AddBottle(ExtraTubeCapacity);
            _extraTubes++;
            _board.AddBottle(_model, index, () =>
            {
                _audio.PlayOneShotSound("extra_tube");
                _haptics.Haptic(HapticModule.HapticType.Medium);
            });
            _board.AssertMatches(_model);
        }

        // Arming pays nothing; the transfer that uses it does.
        private void ToggleFreeMove()
        {
            if (_freeArmed)
            {
                _freeArmed = false;
                _audio.PlayOneShotSound("free_cancel");
                return;
            }
            if (!_inventory.CanUse(BoosterType.FreeMove, _level))
            {
                ShowNotEnoughCoins(BoosterType.FreeMove);
                return;
            }
            _freeArmed = true;
            _audio.PlayOneShotSound("free_move");
            _haptics.Haptic(HapticModule.HapticType.Light);
        }

        private void ShowNotEnoughCoins(BoosterType type)
        {
            _popups.Show(_messagePopup, new MessageArgs("booster.no_coins_title",
                _loc.Format("booster.no_coins_body", BoosterInventory.Price(type)), "common.continue"));
        }

        // Costs a life when the try had a move.
        private void Restart()
        {
            if (_states.CurrentKey != GameState.Playing) return;
            if (_tryHasMoves && !_lives.TrySpend())
            {
                ShowOutOfLives();
                return;
            }
            _ads.EndActivePlay();
            _progress.FailAttempt();
            if (_tryHasMoves) _audio.PlayOneShotSound("retry");
            _states.ChangeState(GameState.Playing);
        }

        // Leaving now would cost a life, so it asks first.
        private void RequestQuit()
        {
            var costsLife = _states.CurrentKey == GameState.Playing && _tryHasMoves && !_lives.IsUnlimited &&
                            _lives.Count > 0;
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
            if (_states.CurrentKey == GameState.Playing && _tryHasMoves)
            {
                _lives.TrySpend();
                _progress.FailAttempt();
            }
            _ads.EndActivePlay();
            _ = Services.Get<MainFlow>().GoHomeAsync();
        }

        private void CancelRescueSearch()
        {
            if (_rescueSearch == null) return;
            _rescueSearch.Cancel();
            _rescueSearch.Dispose();
            _rescueSearch = null;
        }

        private void ShowOutOfLives()
        {
            _popups.Show(_messagePopup, new MessageArgs("lives.out_title",
                _loc.Format("lives.out_body", EconomyText.Timer(_lives.NextLifeIn)), "common.home",
                () => _ = Services.Get<MainFlow>().GoHomeAsync()));
        }

        private void Win()
        {
            if (_states.CurrentKey == GameState.Playing) _states.ChangeState(GameState.Won);
        }

        private void Lose()
        {
            if (_states.CurrentKey == GameState.Playing) _states.ChangeState(GameState.Lost);
        }

        private void RefreshHud()
        {
            _levelText.text = _loc.Format("home.level", _level);
            if (_movesText != null) _movesText.text = _model != null ? $"Moves {_model.MoveCount}" : string.Empty;
            if (_difficulty != null)
            {
                var difficulty = CampaignRules.DifficultyOf(_level);
                _difficulty.Set(EconomyText.Difficulty(difficulty, _loc), EconomyText.DifficultyMarks(difficulty));
            }
            if (_boosters != null)
                _boosters.Refresh(_level, _model != null && _model.CanUndo, _extraTubes < MaxExtraTubes, _freeArmed);
        }

        // No level shown: the board is empty and taps are ignored.
        private sealed class IdleState : State
        {
            private readonly GameController _c;
            public IdleState(GameController controller) => _c = controller;

            public override void Enter()
            {
                _c.CancelRescueSearch();
                _c._ads.EndActivePlay();
                _c._resumeTry = false;
                _c.Deselect();
                _c._board.Clear();
                _c._model = null;
                _c._winPending = false;
                _c._stuckCheckPending = false;
                _c._hud.SetActive(false);
            }
        }

        private sealed class PlayingState : State
        {
            private readonly GameController _c;
            public PlayingState(GameController controller) => _c = controller;

            public override void Enter()
            {
                if (!_c._resumeTry)
                {
                    _c.BeginLevel();
                    return;
                }

                // Back from the fail screen into the same try (Undo or Continue): no new board, no new try.
                _c._resumeTry = false;
                _c._freeArmed = false;
                _c._stuckCheckPending = false;
                _c._ads.BeginActivePlay();
                _c.RefreshHud();
            }

            public override void Tick(float deltaTime)
            {
                if (!_c._stuckCheckPending || _c._board.IsAnimating) return;
                _c._stuckCheckPending = false;
                if (_c._model != null && !_c._winPending && _c._model.HasNoMove()) _c._states.ChangeState(GameState.Lost);
            }
        }

        // Doesn't end the try or cost a life; a search on a board copy decides the Undo and Continue offers.
        private sealed class LostState : State
        {
            private readonly GameController _c;
            private RescueReport _report;
            public LostState(GameController controller) => _c = controller;

            public override void Enter()
            {
                _c.Deselect();
                _c._ads.EndActivePlay();
                // A bomb already sounded its explosion; only a stuck board gets the fail sting.
                if (!_c._model.IsFailed) _c._audio.PlayOneShotSound("fail");
                _c._haptics.Haptic(HapticModule.HapticType.Heavy);
                _report = null;
                Analyze();
            }

            public override void Exit() => _c.CancelRescueSearch();

            private async void Analyze()
            {
                _c.CancelRescueSearch();
                var search = new System.Threading.CancellationTokenSource();
                _c._rescueSearch = search;
                RescueReport report;
                try
                {
                    report = await RescueAdvisor.AnalyzeAsync(_c._model, RescueAdvisor.DefaultTimeoutMs, search.Token);
                }
                catch (System.OperationCanceledException)
                {
                    return;
                }
                if (search.IsCancellationRequested || _c._states.CurrentKey != GameState.Lost) return;
                _report = report;
                Show();
            }

            // Also shown again after a refused payment.
            private void Show()
            {
                if (_c._states.CurrentKey != GameState.Lost) return;
                var loc = _c._loc;
                var lives = _c._lives;
                var bomb = _c._model.IsFailed;
                var options = new System.Collections.Generic.List<ResultOption>();
                var note = new System.Text.StringBuilder(loc.Get(bomb ? "fail.bomb" : "fail.no_move"));

                if (bomb && _report != null && _report.UndoWins)
                {
                    var stock = _c._inventory.Stock(BoosterType.Undo);
                    var label = stock > 0
                        ? loc.Format("undo.offer_stock", stock)
                        : loc.Format("undo.offer_coins", BoosterInventory.Price(BoosterType.Undo));
                    options.Add(new ResultOption(label, UndoLastMove, _c._inventory.CanUse(BoosterType.Undo, _c._level)));
                }

                var offer = _report?.Offer;
                if (offer != null && !_c._model.ContinueUsed && !_c._continue.UsedInTry(_c._progress.TryId))
                {
                    options.Add(new ResultOption(ContinueService.Describe(offer, _c._model, loc), () => BuyContinue(offer),
                        _c._continue.CanAfford));
                    if (!_c._continue.CanAfford) note.Append('\n').Append(loc.Format("continue.not_enough", ContinueService.Price));
                }
                else
                {
                    note.Append('\n').Append(loc.Get("fail.no_offer"));
                }

                if (lives.CanPlay)
                {
                    var restart = lives.IsUnlimited ? loc.Get("common.retry") : loc.Get("game.restart_life");
                    options.Add(new ResultOption(restart, Retry));
                    note.Append('\n').Append(EconomyText.Lives(lives, loc));
                }
                else
                {
                    note.Append('\n').Append(loc.Format("lives.out_body", EconomyText.Timer(lives.NextLifeIn)));
                    if (_c._ads.CanOfferRewardedLife)
                        options.Add(new ResultOption(loc.Get("lives.ad"), () => _c._ads.ShowRewardedLife(_ => Show())));
                }

                // Home is the popup's X; the last option (restart, or the rewarded life) is the main button.
                _c._popups.Show(_c._resultPopup, ResultArgs.Loss(_c._level, options, note.ToString(), Leave));
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

            private void Leave()
            {
                if (_c._tryHasMoves) _c._lives.TrySpend();
                _c._progress.FailAttempt();
                _ = Services.Get<MainFlow>().GoHomeAsync();
            }
        }

        private sealed class WonState : State
        {
            // 150-220 ms after the last physical contact.
            private const float WinCueDelay = 0.18f;

            private readonly GameController _c;
            public WonState(GameController controller) => _c = controller;

            public override void Enter()
            {
                _c.Deselect();
                _c._ads.EndActivePlay();
                _c._rewardedWatched = false;
                var result = _c._progress.CompleteLevel();
                // Win cue starts a beat after the last contact, delivery sound right after, so nothing lands on the final ball.
                PrimeTween.Tween.Delay(_c, WinCueDelay, controller =>
                {
                    controller._audio.PlayOneShotSound(WinSound(result.Difficulty));
                    controller._haptics.Haptic(HapticModule.HapticType.Heavy);
                    PlayRewardSound(result);
                });
                var options = new[] { new ResultOption(_c._loc.Get("common.continue"), () => ShowMetaTransition(result)) };
                _c._popups.Show(_c._resultPopup, ResultArgs.Win(result, options,
                    _c._progress.CanDoubleReward(result) ? (System.Action<System.Action<bool>>)(done => DoubleReward(result, done)) : null));
            }

            private static string WinSound(LevelDifficulty difficulty) => difficulty switch
            {
                LevelDifficulty.Hard => "win_hard",
                LevelDifficulty.SuperHard => "win_super",
                _ => "win"
            };

            // One delivery sound for what the win paid: coins, or the road reward's kind.
            private void PlayRewardSound(LevelResult result)
            {
                if (!result.FirstWin) return;
                var road = result.Road;
                var boosters = road.Undo + road.ExtraTube + road.FreeMove;
                var kinds = (road.Coins > 0 ? 1 : 0) + (boosters > 0 ? 1 : 0) + (road.UnlimitedLifeMinutes > 0 ? 1 : 0);
                string sound;
                if (kinds > 1) sound = "multi_grant";
                else if (boosters > 1) sound = "tool_pair";
                else if (boosters == 1) sound = "tool_grant";
                else if (road.UnlimitedLifeMinutes > 0) sound = "unlimited";
                else if (road.Coins > 0) sound = "road";
                else sound = "coin";
                _c._audio.PlayOneShotSound(sound);
            }

            private void DoubleReward(LevelResult result, System.Action<bool> done)
            {
                _c._ads.ShowRewarded("double_reward", earned =>
                {
                    var doubled = earned && _c._progress.ClaimDoubleReward(result);
                    _c._rewardedWatched |= doubled;
                    if (doubled) _c._audio.PlayOneShotSound("coin_double");
                    done(doubled);
                });
            }

            // Order after a win: result and reward, then the meta transition, then the road (Home).
            private void ShowMetaTransition(LevelResult result)
            {
                if (!result.MetaChanged)
                {
                    LeaveToRoad(result);
                    return;
                }

                var body = result.CampaignFinal
                    ? _c._loc.Get("meta.final_body")
                    : _c._loc.Format("meta.period_body", result.MetaPeriod);
                _c._audio.PlayOneShotSound("meta");
                _c._popups.Show(_c._messagePopup, new MessageArgs(
                    result.CampaignFinal ? "meta.final_title" : "meta.period_title", body, "common.continue",
                    () => LeaveToRoad(result)));
            }

            private void LeaveToRoad(LevelResult result)
            {
                void GoHome() => _ = Services.Get<MainFlow>().GoHomeAsync();
                _c._ads.ShowLevelEndInterstitial(result.Level, result.FirstWin, _c._rewardedWatched, GoHome);
            }
        }
    }
}
