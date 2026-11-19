using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Project.Core.Audio
{
    // Gains in dB are applied per play, never baked into the clips.
    public sealed class AudioManager : MonoBehaviour
    {
        // The same event within this many frames plays once.
        public const int DefaultFrameThreshold = 1;

        [SerializeField] private AudioLibrary _library;
        [SerializeField] private AudioMixerGroup _soundMixerGroup;
        [SerializeField] private AudioMixerGroup _musicMixerGroup;
        [SerializeField, Range(1, 32)] private int _soundSourceCount = 16;

        private readonly HashSet<string> _warnedIds = new();
        private readonly Dictionary<string, AudioEvent> _clipDictionary = new();
        private AudioSource _musicSource;
        private AudioSource[] _soundSources;
        private int _nextSource;
        private string _currentMusicId;

        public bool MusicMuted { get; private set; }
        public bool SoundMuted { get; private set; }

        private sealed class AudioEvent
        {
            public readonly string Bus;
            public int LastPlayedFrame = int.MinValue;
            private readonly AudioBag<AudioClip> _bag;

            public AudioEvent(AudioLibrary.EventEntry entry)
            {
                Bus = entry.Bus;
                _bag = new AudioBag<AudioClip>(entry.Clips ?? System.Array.Empty<AudioClip>(), n => Random.Range(0, n));
            }

            public bool HasClips => _bag.Count > 0;

            public AudioClip GetNext() => _bag.Next();
        }

        public void Initialize()
        {
            _musicSource = CreateSource("Music", loop: true, _musicMixerGroup);
            _soundSources = new AudioSource[_soundSourceCount];
            for (var i = 0; i < _soundSources.Length; i++) _soundSources[i] = CreateSource($"Sound {i}", loop: false, _soundMixerGroup);
            BuildClipDictionary();
        }

        // Volume is linear; non-event ids fall back to the library's single-clip entries.
        public void PlayOneShotSound(string clipName, float volume = 1f, int frameThreshold = DefaultFrameThreshold)
        {
            if (SoundMuted || _soundSources == null || string.IsNullOrEmpty(clipName)) return;

            AudioClip clip;
            if (_clipDictionary.TryGetValue(clipName, out var audioEvent) && audioEvent.HasClips)
            {
                if (Time.frameCount - audioEvent.LastPlayedFrame < frameThreshold) return;
                audioEvent.LastPlayedFrame = Time.frameCount;
                clip = audioEvent.GetNext();
            }
            else if (TryEntry(clipName, out var entry))
            {
                clip = entry.Clip;
                if (entry.Volume > 0f) volume *= entry.Volume;
            }
            else return;

            var source = _soundSources[_nextSource];
            _nextSource = (_nextSource + 1) % _soundSources.Length;
            source.pitch = 1f;
            source.PlayOneShot(clip, volume);
        }

        public static float DecibelsToVolume(float decibels) => Mathf.Pow(10f, decibels / 20f);

        public void PlayMusic(string id)
        {
            if (_musicSource == null || id == _currentMusicId) return;
            _currentMusicId = id;
            if (!TryEntry(id, out var entry))
            {
                _musicSource.Stop();
                return;
            }
            _musicSource.clip = entry.Clip;
            _musicSource.volume = entry.Volume <= 0f ? 1f : entry.Volume;
            _musicSource.Play();
        }

        public void SetMusicMuted(bool muted)
        {
            MusicMuted = muted;
            if (_musicSource != null) _musicSource.mute = muted;
        }

        public void SetSoundMuted(bool muted) => SoundMuted = muted;

        private void BuildClipDictionary()
        {
            _clipDictionary.Clear();
            if (_library == null) return;
            foreach (var entry in _library.Events)
            {
                if (string.IsNullOrEmpty(entry.Id)) continue;
                _clipDictionary[entry.Id] = new AudioEvent(entry);
                // Load now so the first play has no hitch.
                if (entry.Clips == null) continue;
                foreach (var clip in entry.Clips)
                    if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            }
        }

        private bool TryEntry(string id, out AudioLibrary.Entry entry)
        {
            entry = default;
            if (_library != null && _library.TryGet(id, out entry)) return entry.Clip != null;
            if (_warnedIds.Add(id)) Debug.LogWarning($"[Audio] '{id}' is neither a sound event nor an AudioLibrary entry.");
            return false;
        }

        private AudioSource CreateSource(string sourceName, bool loop, AudioMixerGroup group)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.pitch = 1f;
            source.outputAudioMixerGroup = group;
            return source;
        }
    }
}
