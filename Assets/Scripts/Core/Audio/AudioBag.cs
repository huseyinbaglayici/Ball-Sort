using System;
using System.Collections.Generic;

namespace Project.Core.Audio
{
    // No variant repeats until all have played, even across bags; pure C# so it is testable without Unity.
    public sealed class AudioBag<T>
    {
        private readonly IReadOnlyList<T> _pool;
        private readonly Func<int, int> _random;
        private readonly List<T> _bag = new();
        private T _last;
        private bool _hasLast;

        // random: returns 0 up to, not including, the given value.
        public AudioBag(IReadOnlyList<T> pool, Func<int, int> random)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public int Count => _pool.Count;

        public T Next()
        {
            if (_pool.Count == 0) throw new InvalidOperationException("The sound has no variants.");
            if (_bag.Count == 0) Refill();
            var end = _bag.Count - 1;
            _last = _bag[end];
            _bag.RemoveAt(end);
            _hasLast = true;
            return _last;
        }

        private void Refill()
        {
            _bag.AddRange(_pool);
            for (var i = _bag.Count - 1; i > 0; i--)
            {
                var j = _random(i + 1);
                (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
            }
            // Picks come from the end, so it must not hold the one that just played.
            var end = _bag.Count - 1;
            if (_hasLast && _bag.Count > 1 && EqualityComparer<T>.Default.Equals(_bag[end], _last))
                (_bag[0], _bag[end]) = (_bag[end], _bag[0]);
        }
    }
}
