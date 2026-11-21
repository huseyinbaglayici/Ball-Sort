using Project.Common.UI;
using Project.Common.UI.PopUps;
using Project.Contexts.Game;
using Project.Contexts.Home;
using Project.Core.Audio;
using Project.Core.Events;
using Project.Core.Pooling;
using Project.Core.Services;
using Project.Core.StateMachine;
using UnityEngine;

namespace Project.Contexts.Main
{
    public enum MainState
    {
        None,
        Home,
        Game
    }

    // Raised once, when Main has opened its first screen.
    public readonly struct MainReady : IEvent { }

    // Home and Game are states of one scene; a switch happens under the level-open portal, or the loading overlay without one.
    public sealed class MainFlow : MonoBehaviour
    {
        [SerializeField] private HomeScreen _home;
        [SerializeField] private GameController _game;
        [SerializeField] private LevelOpenView _levelOpen;

        private readonly StateMachine<MainState> _states = new();
        private LoadingOverlay _overlay;
        private PopupService _popups;
        private PoolService _pools;
        private AudioManager _audio;
        private bool _busy;

        public MainState Current => _states.CurrentKey ?? MainState.None;

        private void Awake()
        {
            _overlay = Services.Get<LoadingOverlay>();
            _popups = Services.Get<PopupService>();
            _pools = Services.Get<PoolService>();
            _audio = Services.Get<AudioManager>();

            _states.Add(MainState.Home, new HomeState(this))
                   .Add(MainState.Game, new GameState(this));
            Services.Register(this);
        }

        // No loading overlay: the splash still covers the screen.
        public void Begin(MainState first)
        {
            if (Current != MainState.None) return;
            _states.ChangeState(first);
            EventBus<MainReady>.Raise(default);
        }

        public Awaitable GoHomeAsync() => SwitchAsync(MainState.Home);

        public Awaitable GoToGameAsync() => SwitchAsync(MainState.Game);

        private async Awaitable SwitchAsync(MainState next)
        {
            if (_busy || Current == next) return;

            _busy = true;
            try
            {
                _popups.CloseAll();
                // Without the portal (or an orb to grow from) the overlay still covers the switch.
                if (_levelOpen == null || _levelOpen.IsRunning || _home.Orb == null)
                {
                    await _overlay.ShowAsync();
                    _states.ChangeState(next);
                    await _overlay.HideAsync();
                }
                else if (next == MainState.Game)
                {
                    _home.Leave();
                    await _levelOpen.OpenAsync(_home.Orb, () => _states.ChangeState(next));
                }
                else await _levelOpen.CloseAsync(_home.Orb, () => _states.ChangeState(next));
            }
            finally
            {
                _busy = false;
            }
        }

        private sealed class HomeState : State
        {
            private readonly MainFlow _f;
            public HomeState(MainFlow flow) => _f = flow;

            public override void Enter()
            {
                _f._game.Exit();
                _f._pools.DespawnAll();
                _f._home.SetOpen(true);
                _f._audio.PlayMusic("music_home");
            }
        }

        private sealed class GameState : State
        {
            private readonly MainFlow _f;
            public GameState(MainFlow flow) => _f = flow;

            public override void Enter()
            {
                _f._home.SetOpen(false);
                _f._game.Enter();
                _f._audio.PlayMusic("music_game");
            }
        }
    }
}
