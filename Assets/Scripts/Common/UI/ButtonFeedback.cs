using Project.Core.Audio;
using Project.Core.Haptics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Common.UI
{
    // Press: snaps to the press scale with a haptic. Release: snaps back; a click also plays the sound. No tweening.
    public sealed class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private string _clickSfx = "ui_click";
        [SerializeField] private HapticModule.HapticType _haptic = HapticModule.HapticType.Light;
        [SerializeField] private float _pressScale = 0.97f;

        private Selectable _selectable;

        private void Awake() => _selectable = GetComponent<Selectable>();

        private bool Enabled => _selectable == null || _selectable.IsInteractable();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Enabled) return;
            transform.localScale = Vector3.one * _pressScale;
            if (Core.Services.Services.TryGet<HapticModule>(out var haptics)) haptics.Haptic(_haptic);
        }

        public void OnPointerUp(PointerEventData eventData) => transform.localScale = Vector3.one;

        // Sliding off cancels: back to full size, no click.
        public void OnPointerExit(PointerEventData eventData) => transform.localScale = Vector3.one;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Enabled) return;
            if (Core.Services.Services.TryGet<AudioManager>(out var audio)) audio.PlayOneShotSound(_clickSfx);
        }

        private void OnDisable() => transform.localScale = Vector3.one;
    }
}
