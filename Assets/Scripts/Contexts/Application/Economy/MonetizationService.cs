using System;
using UnityEngine;

namespace Project.Contexts.Application.Economy
{
    // Local ads stand-in (no SDK): ads complete at once unless a debug failure is set; every step is logged.
    public sealed class MonetizationService
    {
        private readonly PlayerData _data;
        private readonly LivesService _lives;
        private double _activeSince = -1;

        public MonetizationService(PlayerData data, LivesService lives)
        {
            _data = data;
            _lives = lives;
        }

        // Debug: rewarded ads fail and interstitials don't load, to check the no-reward paths.
        public bool SimulateRewardedFailure { get; set; }
        public bool SimulateInterstitialFailure { get; set; }

        // No ad network in this build; logs mark every event as demo.
        public bool IsDemo => true;

        public bool NoAds => _data.Model.NoAds;

        public AdGroup Group => CampaignRules.AdGroups[_data.Model.AdGroup];

        // Debug: there is no automatic A/B group assignment locally.
        public void SetGroup(int index)
        {
            _data.Model.AdGroup = Math.Clamp(index, 0, CampaignRules.AdGroups.Length - 1);
            _data.Save();
        }

        // Only a completed ad earns; a repeated completion callback grants nothing.
        public void ShowRewarded(string placement, int level, Action<bool> done)
        {
            Log("opportunity", placement, level);
            var earned = !SimulateRewardedFailure;
            Log(earned ? "impression" : "error", placement, level);
            var delivered = false;
            void Complete(bool ok)
            {
                if (delivered) return;
                delivered = true;
                if (ok) Log("reward", placement, level);
                done?.Invoke(ok);
            }
            Complete(earned);
        }

        public bool CanOfferRewardedLife => !_lives.IsUnlimited && _lives.Count == 0;

        // The natural refill keeps its clock.
        public void ShowRewardedLife(int level, Action<bool> done)
        {
            if (!CanOfferRewardedLife)
            {
                done?.Invoke(false);
                return;
            }

            ShowRewarded("rewarded_life", level, earned =>
            {
                if (earned) _lives.Add(CampaignRules.RewardedLifeGrant);
                done?.Invoke(earned);
            });
        }

        // The try's time counts toward the interstitial frequency.
        public void BeginActivePlay() => _activeSince = Time.realtimeSinceStartupAsDouble;

        // Win, loss, restart, leaving the level or the app going to the background.
        public void EndActivePlay()
        {
            if (_activeSince < 0) return;
            _data.Model.AdActiveSeconds += (float)(Time.realtimeSinceStartupAsDouble - _activeSince);
            _activeSince = -1;
            _data.Save();
        }

        // A closed loss or a restart after a move; a level's losses add at most the cap together.
        public void CountLoss(int level)
        {
            var model = _data.Model;
            if (model.AdLossLevel != level)
            {
                model.AdLossLevel = level;
                model.AdLossPoints = 0f;
            }
            var points = Mathf.Min(CampaignRules.LossAdPoint, CampaignRules.LossAdPointCap - model.AdLossPoints);
            if (points <= 0f) return;
            model.AdLossPoints += points;
            model.AdPoints += points;
            _data.Save();
        }

        // Counts an eligible first win, shows an interstitial once the frequency is met, then runs done.
        public void ShowLevelEndInterstitial(int level, bool firstWin, Action done)
        {
            var model = _data.Model;
            if (!firstWin || !CampaignRules.InterstitialEligible(level, Group, NoAds))
            {
                done?.Invoke();
                return;
            }

            model.AdPoints += CampaignRules.WinAdPoint;
            var due = !model.FirstInterstitialSeen ||
                      (model.AdPoints >= Group.Points && model.AdActiveSeconds >= Group.ActiveSeconds);
            _data.Save();
            if (!due)
            {
                done?.Invoke();
                return;
            }
            ShowInterstitial("level_end", level, done);
        }

        // Only a shown interstitial resets the frequency and opens the No Ads offer.
        public void ShowInterstitial(string placement, int level, Action done)
        {
            if (NoAds)
            {
                done?.Invoke();
                return;
            }

            Log("opportunity", placement, level);
            if (SimulateInterstitialFailure)
            {
                Log("error", placement, level);
                done?.Invoke();
                return;
            }

            Log("impression", placement, level);
            var model = _data.Model;
            model.AdPoints = 0f;
            model.AdActiveSeconds = 0f;
            model.FirstInterstitialSeen = true;
            _data.Save();
            done?.Invoke();
        }

        // Debug stand-in for a completed No Ads purchase.
        public void GrantNoAds()
        {
            _data.Model.NoAds = true;
            _data.Save();
        }

        private void Log(string step, string placement, int level) =>
            Debug.Log($"[Ads] {step} placement={placement} level={level} try={_data.Model.TryId} " +
                      $"group={Group.Name} mode={(IsDemo ? "demo" : "live")}");
    }
}
