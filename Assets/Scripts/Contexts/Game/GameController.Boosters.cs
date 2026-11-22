using System.Collections.Generic;
using Project.Common.UI.PopUps;
using Project.Contexts.Application.Economy;
using Project.Core.Haptics;
using UnityEngine;

namespace Project.Contexts.Game
{
    // Boosters in a level: Undo, Extra Tube and the armed Free Move with its target hints.
    public sealed partial class GameController
    {
        // Empty two-slot tube, at most two per try.
        private const int ExtraTubeCapacity = 2;
        private const int MaxExtraTubes = 2;
        // Undo's feel: a clear hit as the balls settle back, then a light one.
        private const float UndoSecondHitDelay = 0.08f;

        // With Free Move armed and balls lifted, every tube that can take them is marked; cleared otherwise.
        private void ShowFreeTargets()
        {
            if (!_freeArmed || _selected == null)
            {
                _board.HideTargets();
                return;
            }
            _freeTargets.Clear();
            var from = _selected.Index;
            for (var to = 0; to < _model.BottleCount; to++)
                if (to != from && !_board.IsLocked(to) && (_model.CanMove(from, to) || _model.CanFreeMove(from, to)))
                    _freeTargets.Add(to);
            _board.ShowTargets(from, _freeTargets);
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


        private bool CanUseBoosters => !_winPending && !_leaving && _model != null && _states.CurrentKey == GameState.Playing;

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
                _board.HideTargets();
                _audio.PlayOneShotSound("free_cancel");
                return;
            }
            if (!_inventory.CanUse(BoosterType.FreeMove, _level))
            {
                ShowNotEnoughCoins(BoosterType.FreeMove);
                return;
            }
            _freeArmed = true;
            ShowFreeTargets();
            _audio.PlayOneShotSound("free_move");
            _haptics.Haptic(HapticModule.HapticType.Light);
        }

        private void ShowNotEnoughCoins(BoosterType type)
        {
            _popups.Show(_messagePopup, new MessageArgs("booster.no_coins_title",
                _loc.Format("booster.no_coins_body", BoosterInventory.Price(type)), "common.continue"));
        }
    }
}
