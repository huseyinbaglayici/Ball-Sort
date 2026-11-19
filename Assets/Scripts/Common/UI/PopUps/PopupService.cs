using System.Collections.Generic;
using Project.Core.Audio;
using UnityEngine;

namespace Project.Common.UI.PopUps
{
    // Each prefab is instantiated once and reused; one popup shows at a time and the rest queue.
    public sealed class PopupService
    {
        private readonly Transform _root;
        private readonly AudioManager _audio;
        private readonly Dictionary<Popup, Popup> _instances = new();
        private readonly Queue<(Popup Prefab, object Args)> _queue = new();
        private Popup _current;

        public PopupService(Transform root, AudioManager audio)
        {
            _root = root;
            _audio = audio;
        }

        public void Show(Popup prefab, object args = null)
        {
            if (prefab == null) return;
            if (_current != null)
            {
                _queue.Enqueue((prefab, args));
                return;
            }
            Open(prefab, args);
        }

        public void CloseAll()
        {
            _queue.Clear();
            if (_current != null) _current.CloseImmediately();
        }

        private async void Open(Popup prefab, object args)
        {
            if (!_instances.TryGetValue(prefab, out var popup) || popup == null)
            {
                popup = Object.Instantiate(prefab, _root, false);
                popup.name = prefab.name;
                popup.Closed += OnClosed;
                _instances[prefab] = popup;
            }

            _current = popup;
            _audio.PlayOneShotSound("open");
            await popup.ShowAsync(args);
        }

        private void OnClosed(Popup popup)
        {
            if (popup != _current) return;
            _current = null;
            _audio.PlayOneShotSound("close");
            if (_queue.Count > 0)
            {
                var (prefab, args) = _queue.Dequeue();
                Open(prefab, args);
            }
        }
    }
}
