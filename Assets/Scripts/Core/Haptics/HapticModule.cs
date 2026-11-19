using UnityEngine;

namespace Project.Core.Haptics
{
    // One vibration per frame, strongest wins; iOS only has Handheld.Vibrate, so only Heavy uses it.
    public sealed class HapticModule
    {
        public enum HapticType
        {
            Light,
            Medium,
            Heavy
        }

        public bool Enabled { get; set; }

        private int _playedFrame = -1;
        private HapticType _playedType;

#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly AndroidJavaObject _vibrator;
        private readonly AndroidJavaClass _effectClass;
        private readonly bool _supportsEffects;
#endif

        public HapticModule()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                _supportsEffects = version.GetStatic<int>("SDK_INT") >= 26;
                if (_supportsEffects) _effectClass = new AndroidJavaClass("android.os.VibrationEffect");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Haptics] Vibrator unavailable: {e.Message}");
            }
#endif
        }

        public void Haptic(HapticType type)
        {
            if (!Enabled) return;
            if (_playedFrame == Time.frameCount && type <= _playedType) return;
            _playedFrame = Time.frameCount;
            _playedType = type;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_vibrator == null) return;
            var (ms, amplitude) = type switch
            {
                HapticType.Light => (15L, 60),
                HapticType.Medium => (25L, 140),
                _ => (40L, 255)
            };
            if (_supportsEffects)
            {
                using var effect = _effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude);
                _vibrator.Call("vibrate", effect);
            }
            else
            {
                _vibrator.Call("vibrate", ms);
            }
#elif UNITY_IOS && !UNITY_EDITOR
            if (type == HapticType.Heavy) Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID
        // Never called; Unity adds the VIBRATE permission only if Handheld.Vibrate is referenced.
        private static void KeepVibratePermission() => Handheld.Vibrate();
#endif
    }
}
