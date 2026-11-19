using System;
using UnityEngine;

namespace Project.Contexts.Application.Economy
{
    // Local ads/store stand-in (no SDK): rewarded ads complete, interstitials only log, purchases are off.
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

        // Debug: makes rewarded ads fail, to check the no-reward path.
        public bool SimulateRewardedFailure { get; set; }

        public bool NoAds => _data.Model.NoAds;

        public AdGroup Group => CampaignRules.AdGroups[_data.Model.AdGroup];

        // Debug: there is no automatic A/B group assignment locally.
        public void SetGroup(int index)
        {
            _data.Model.AdGroup = Math.Clamp(index, 0, CampaignRules.AdGroups.Length - 1);
            _data.Save();
        }

        public void ShowRewarded(string placement, Action<bool> done)
        {
            var earned = !SimulateRewardedFailure;
            Debug.Log($"[Ads] Rewarded '{placement}' (local stub): {(earned ? "earned" : "failed")}.");
            done?.Invoke(earned);
        }


        public bool CanOfferRewardedLife => !_lives.IsUnlimited && _lives.Count == 0;

        // The natural refill keeps running.
        public void ShowRewardedLife(Action<bool> done)
        {
            if (!CanOfferRewardedLife)
            {
                done?.Invoke(false);
                return;
            }

            ShowRewarded("rewarded_life", earned =>
            {
                if (earned) _lives.Add(CampaignRules.RewardedLifeGrant);
                done?.Invoke(earned);
            });
        }


        // The try's time counts toward the interstitial frequency.
        public void BeginActivePlay() => _activeSince = Time.realtimeSinceStartupAsDouble;

        // Win, loss, restart or leaving the level.
        public void EndActivePlay()
        {
            if (_activeSince < 0) return;
            _data.Model.AdActiveSeconds += (float)(Time.realtimeSinceStartupAsDouble - _activeSince);
            _activeSince = -1;
            _data.Save();
        }

        // Counts an eligible first win, shows an interstitial once the frequency is met, then runs done.
        public void ShowLevelEndInterstitial(int level, bool firstWin, bool rewardedWatched, Action done)
        {
            var model = _data.Model;
            if (!firstWin || !CampaignRules.InterstitialEligible(level, Group, rewardedWatched, NoAds))
            {
                done?.Invoke();
                return;
            }

            model.AdPoints++;
            var due = !model.FirstInterstitialSeen ||
                      (model.AdPoints >= Group.Points && model.AdActiveSeconds >= Group.ActiveSeconds);
            if (!due)
            {
                _data.Save();
                done?.Invoke();
                return;
            }

            model.AdPoints = 0;
            model.AdActiveSeconds = 0f;
            ShowInterstitial("level_end", done);
        }

        public void ShowInterstitial(string placement, Action done)
        {
            if (NoAds)
            {
                done?.Invoke();
                return;
            }

            Debug.Log($"[Ads] Interstitial '{placement}' (local stub, group {Group.Name}).");
            _data.Model.FirstInterstitialSeen = true;
            _data.Save();
            done?.Invoke();
        }

        // Debug stand-in for a completed No Ads purchase.
        public void GrantNoAds()
        {
            _data.Model.NoAds = true;
            _data.Save();
        }
    }
}
