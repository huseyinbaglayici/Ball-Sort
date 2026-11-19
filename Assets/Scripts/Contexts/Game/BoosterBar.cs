using System;
using Project.Common.Tween;
using Project.Contexts.Application.Economy;
using Project.Core.Events;
using Project.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Contexts.Game
{
    // Only reports presses; GameController decides what they do.
    public sealed class BoosterBar : MonoBehaviour
    {
        [Serializable]
        private sealed class Slot
        {
            public BoosterType Type;
            public Button Button;
            public TMP_Text Badge;
            public GameObject Lock;
            public TMP_Text LockLevel;
            // Free Move only: shown while the mode is on.
            public GameObject Armed;
        }

        [SerializeField] private Slot[] _slots = Array.Empty<Slot>();

        private BoosterInventory _inventory;
        private int _level;
        private bool _canUndo;
        private bool _canAddTube;
        private bool _freeArmed;

        public event Action<BoosterType> Pressed;

        private void Awake()
        {
            foreach (var slot in _slots)
            {
                var type = slot.Type;
                if (slot.Button != null) slot.Button.onClick.AddListener(() => Pressed?.Invoke(type));
            }
        }

        private void OnEnable()
        {
            EventBus<BoosterStockChanged>.Subscribe(OnStockChanged);
            EventBus<CoinsChanged>.Subscribe(OnCoinsChanged);
            Render();
        }

        private void OnDisable()
        {
            EventBus<BoosterStockChanged>.Unsubscribe(OnStockChanged);
            EventBus<CoinsChanged>.Unsubscribe(OnCoinsChanged);
        }

        public void Refresh(int level, bool canUndo, bool canAddTube, bool freeArmed)
        {
            _level = level;
            _canUndo = canUndo;
            _canAddTube = canAddTube;
            _freeArmed = freeArmed;
            Render();
        }

        // Refused press: the button gives a short shake.
        public void Refuse(BoosterType type)
        {
            foreach (var slot in _slots)
                if (slot.Type == type && slot.Button != null) _ = TweenPresets.Punch(slot.Button.transform);
        }

        private void OnStockChanged(BoosterStockChanged _) => Render();
        private void OnCoinsChanged(CoinsChanged _) => Render();

        private void Render()
        {
            if (_inventory == null && !Services.TryGet(out _inventory)) return;
            foreach (var slot in _slots)
            {
                var type = slot.Type;
                var unlocked = BoosterInventory.IsUnlocked(type, _level);
                var stock = _inventory.Stock(type);
                if (slot.Lock != null) slot.Lock.SetActive(!unlocked);
                if (slot.LockLevel != null) slot.LockLevel.text = $"Lv {CampaignRules.UnlockLevel(type)}";
                if (slot.Badge != null)
                {
                    slot.Badge.gameObject.SetActive(unlocked);
                    slot.Badge.text = stock > 0 ? stock.ToString() : BoosterInventory.Price(type).ToString();
                }
                if (slot.Armed != null) slot.Armed.SetActive(type == BoosterType.FreeMove && _freeArmed);
                if (slot.Button != null) slot.Button.interactable = unlocked && IsAvailable(type);
            }
        }

        private bool IsAvailable(BoosterType type) => type switch
        {
            BoosterType.Undo => _canUndo,
            BoosterType.ExtraTube => _canAddTube,
            _ => true
        };
    }
}
