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

    /// <summary>Raised once, when Main has opened its first screen.</summary>
    public readonly struct MainReady : IEvent { }

    /// <summary>
    /// The only place that decides what the Main scene shows. Home and Game are states of the one scene, not scenes:
    /// a switch covers the screen with the loading overlay, closes popups, opens one context, closes the other and
    /// swaps the music.
    /// </summary>
    public sealed class MainFlow : MonoBehaviour
    {
        [SerializeField] private HomeScreen _home;
        [SerializeField] private GameController _game;

        private readonly StateMachine<MainState> _states = new();
        private LoadingOverlay _overlay;
        private PopupService _popups;
        private PoolService _pools;
        private AudioService _audio;
        private bool _busy;

        public MainState Current => _states.CurrentKey ?? MainState.None;

        private void Awake()
        {
            _overlay = Services.Get<LoadingOverlay>();
            _popups = Services.Get<PopupService>();
            _pools = Services.Get<PoolService>();
            _audio = Services.Get<AudioService>();

            _states.Add(MainState.Home, new HomeState(this))
                   .Add(MainState.Game, new GameState(this));
            Services.Register(this);
        }

        /// <summary>Opens the first screen at once, without the loading overlay (the splash still covers it).</summary>
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
                await _overlay.ShowAsync();
                _states.ChangeState(next);
                await _overlay.HideAsync();
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
