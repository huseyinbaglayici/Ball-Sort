using System;
using Project.Core.Audio;
using Project.Core.Haptics;
using Project.Core.Localization;
using Project.Core.Save;

namespace Project.Core.Settings
{
    [Serializable]
    public sealed class SettingsModel
    {
        public int Version = 1;
        public bool MusicMuted;
        public bool SfxMuted;
        public bool HapticsEnabled = true;
        public string Language = "";
    }

    // Applies saved preferences on start; each change is applied and saved at once.
    public sealed class SettingsService
    {
        private const string Key = "settings";
        private readonly SaveService _save;
        private readonly AudioManager _audio;
        private readonly HapticModule _haptics;
        private readonly LocalizationService _localization;
        private readonly SettingsModel _model;

        public SettingsService(SaveService save, AudioManager audio, HapticModule haptics, LocalizationService localization)
        {
            _save = save;
            _audio = audio;
            _haptics = haptics;
            _localization = localization;
            _model = save.Load<SettingsModel>(Key);

            _audio.SetMusicMuted(_model.MusicMuted);
            _audio.SetSoundMuted(_model.SfxMuted);
            _haptics.Enabled = _model.HapticsEnabled;
            // With no saved choice, keep the device language.
            if (!string.IsNullOrEmpty(_model.Language)) _localization.SetLanguage(_model.Language);
        }

        public bool SoundMuted => _model.SfxMuted;
        public bool MusicMuted => _model.MusicMuted;
        public bool HapticsEnabled => _model.HapticsEnabled;

        public void SetSoundMuted(bool muted)
        {
            _model.SfxMuted = muted;
            _audio.SetSoundMuted(muted);
            Save();
        }

        public void SetMusicMuted(bool muted)
        {
            _model.MusicMuted = muted;
            _audio.SetMusicMuted(muted);
            Save();
        }

        public void SetHapticsEnabled(bool enabled)
        {
            _model.HapticsEnabled = enabled;
            _haptics.Enabled = enabled;
            Save();
        }

        public void SetLanguage(string code)
        {
            _localization.SetLanguage(code);
            _model.Language = _localization.LanguageCode;
            Save();
        }

        private void Save() => _save.Save(Key, _model);
    }
}
