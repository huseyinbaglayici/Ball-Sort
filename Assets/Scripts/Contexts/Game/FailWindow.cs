using System;
using Project.Contexts.Game.Sort;

namespace Project.Contexts.Game
{
    // Fail panel gate: opens once the board stayed readable long enough and the rescue search ended.
    public sealed class FailWindow
    {
        private float _left;

        public FailWindow(int tryId, int version, float readableLeft)
        {
            TryId = tryId;
            Version = version;
            _left = Math.Max(0f, readableLeft);
        }

        public int TryId { get; }
        public int Version { get; }
        public bool Searched { get; private set; }
        public bool Opened { get; private set; }
        public float Left => _left;

        // Pass 0 for the frame the app resumes on, so background time never counts.
        public void Tick(float deltaTime) => _left = Math.Max(0f, _left - Math.Max(0f, deltaTime));

        public void SearchDone() => Searched = true;

        // True once, on the first frame the panel may open for this try and board.
        public bool TryOpen(int tryId, int version)
        {
            if (Opened || _left > 0f || !Searched || tryId != TryId || version != Version) return false;
            Opened = true;
            return true;
        }

        // Time of the last meaningful change the fail board shows: landing, reveal, blast or opened obstacle.
        public static float ReadableAt(float landedAt, float movedAt, MoveResult move, BallAnimationConfig config)
        {
            var at = landedAt;
            if (move == null) return at;
            if (move.Revealed.Count > 0) at = Math.Max(at, movedAt + config.HiddenReadableTime);
            if (move.LockboxesOpened.Count > 0) at = Math.Max(at, landedAt + config.ShutterClearTime);
            if (move.CurtainsLifted.Count > 0) at = Math.Max(at, landedAt + config.MeltDuration + config.MeltOutDuration);
            if (move.Exploded) at = Math.Max(at, landedAt + config.ExplodeReadableTime);
            return at;
        }
    }
}
