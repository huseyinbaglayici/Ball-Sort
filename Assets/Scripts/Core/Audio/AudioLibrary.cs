using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.Audio
{
    // Events are rebuilt from AudioEvents.json by the editor importer; empty hand-set entries play nothing.
    [CreateAssetMenu(menuName = "Project/Audio Library", fileName = "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Id;
            public AudioClip Clip;
            [Range(0f, 1f)] public float Volume;
        }

        [Serializable]
        public struct EventEntry
        {
            public string Id;
            public string Bus;
            public AudioClip[] Clips;
        }

        [SerializeField] private List<Entry> _entries = new();
        [SerializeField] private List<EventEntry> _events = new();

        private Dictionary<string, Entry> _lookup;

        public IReadOnlyList<EventEntry> Events => _events;

        public bool TryGet(string id, out Entry entry)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, Entry>(_entries.Count);
                foreach (var e in _entries)
                    if (!string.IsNullOrEmpty(e.Id)) _lookup[e.Id] = e;
            }
            return _lookup.TryGetValue(id, out entry);
        }

#if UNITY_EDITOR
        // Editor importer only.
        public void SetEvents(List<EventEntry> events) => _events = events;
#endif

        private void OnValidate() => _lookup = null;
    }
}
