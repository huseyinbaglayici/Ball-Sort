using Project.Common.Tween;
using Project.Core.Audio;
using Project.Core.Haptics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.Common.UI
{
    public sealed class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [SerializeField] private string _clickSfx = "ui_click";
        [SerializeField] private HapticModule.HapticType _haptic = HapticModule.HapticType.Light;

        public void OnPointerDown(PointerEventData eventData) => TweenPresets.Press(transform);

        public void OnPointerUp(PointerEventData eventData) => TweenPresets.Release(transform);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Core.Services.Services.TryGet<AudioManager>(out var audio)) audio.PlayOneShotSound(_clickSfx);
            if (Core.Services.Services.TryGet<HapticModule>(out var haptics)) haptics.Haptic(_haptic);
        }

        private void OnDisable() => transform.localScale = Vector3.one;
    }
}
