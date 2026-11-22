using Project.Common.UI.PopUps;
using Project.Contexts.Application;
using Project.Contexts.Application.Economy;
using Project.Contexts.LiveOps;
using Project.Contexts.Main;
using Project.Core.Haptics;
using Project.Core.Services;
using Project.Core.StateMachine;
using UnityEngine;

namespace Project.Contexts.Game
{
    // The win: celebration, coins into the HUD, the chapter note, then the next level or Home.
    public sealed partial class GameController
    {
        // No result panel: the win's coins fly into the HUD counter, then the next level (or Home) follows by itself.
        private sealed class WonState : State
        {
            // Coins on screen per win, and the beat after the last one lands before moving on.
            private const int MinFlyingCoins = 4;
            private const int MaxFlyingCoins = 10;
            private const float AfterCollect = 0.35f;

            private readonly GameController _c;
            private LevelResult _result;
            public WonState(GameController controller) => _c = controller;

            public override void Enter()
            {
                _c.Deselect();
                _c._ads.EndActivePlay();
                _c._board.PlayResult(true);
                _result = _c._progress.CompleteLevel();
                // The counter leaves the new coins out until they land.
                if (_c._coinHud != null) _c._coinHud.Hold(_result.TotalCoins);
                // Win cue and coins wait for the celebration to reach its ribbon.
                PrimeTween.Tween.Delay(this, _c._board.WinPopupDelay, state => state.Collect());
            }

            public override void Exit()
            {
                PrimeTween.Tween.StopAll(this);
                if (_c._coinHud != null) _c._coinHud.Hold(0);
            }

            private void Collect()
            {
                _c._audio.PlayOneShotSound(WinSounds.For(_result.Difficulty));
                _c._haptics.Haptic(HapticModule.HapticType.Heavy);
                var coins = _result.TotalCoins;
                var camera = Camera.main;
                if (coins <= 0 || _c._coinTarget == null || _c._coinSprite == null || camera == null)
                {
                    PlayRewardSound(_result);
                    if (_c._coinHud != null) _c._coinHud.Hold(0);
                    PrimeTween.Tween.Delay(this, AfterCollect, state => state.ShowMetaTransition());
                    return;
                }

                var count = Mathf.Clamp(coins / 10, MinFlyingCoins, MaxFlyingCoins);
                var landed = 0;
                // From the middle of the screen, where the ribbon sits; each landing adds its share to the counter.
                CoinBurst.PlayFromWorld(camera.transform.position, camera, _c._coinTarget, _c._coinSprite, count, 110f, i =>
                {
                    if (landed++ == 0) PlayRewardSound(_result);
                    if (_c._coinHud != null) _c._coinHud.Release(coins * (i + 1) / count - coins * i / count);
                    _c._haptics.Haptic(HapticModule.HapticType.Light);
                });
                var done = CoinBurst.Duration(count) + AfterCollect;
                PrimeTween.Tween.Delay(this, done, state => state.ShowMetaTransition());
            }

            private void PlayRewardSound(LevelResult result)
            {
                var sound = WinSounds.Reward(result);
                if (sound != null) _c._audio.PlayOneShotSound(sound);
            }

            // Order after a win: coins, then the meta transition, then the road (Home) or the next chain level.
            private void ShowMetaTransition()
            {
                var result = _result;
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
                var flow = Services.Get<MainFlow>();
                void Next() => _ = CampaignRules.ChainsAfterWin(result.Level) ? flow.NextLevelAsync() : flow.GoHomeAsync();
                _c._ads.ShowLevelEndInterstitial(result.Level, result.FirstWin, Next);
            }
        }
    }
}
