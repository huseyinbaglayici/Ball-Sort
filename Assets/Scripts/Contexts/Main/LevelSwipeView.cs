using System;
using PrimeTween;
using Project.Contexts.Game.Sort;
using UnityEngine;

namespace Project.Contexts.Main
{
    // Level to level: the finished board slides off to the left and the next one slides in from the right; HUD and background stay.
    public sealed class LevelSwipeView : MonoBehaviour
    {
        [SerializeField] private LevelSwipeSettings _settings;
        [SerializeField] private BoardView _board;

        public bool IsRunning { get; private set; }

        // buildNext builds the next level while the screen is empty; its own entrance gives way to the slide.
        public async Awaitable PlayAsync(Action buildNext)
        {
            IsRunning = true;
            var board = _board.transform;
            var built = false;
            try
            {
                var s = _settings;
                var travel = ScreenWidth() * s.Distance;
                var x = board.position.x;
                await Tween.PositionX(board, x, x - travel, s.OutTime, s.OutEase);

                built = true;
                buildNext();
                _board.SkipEntrance(s.InTime);
                x = board.position.x;
                // Placed off-screen now, so the built board never shows a frame in its seat.
                board.position = new Vector3(x + travel, board.position.y, board.position.z);
                await Tween.PositionX(board, x + travel, x, s.InTime, s.InEase);
            }
            finally
            {
                // A swipe cut short still leaves the next level built and seated.
                if (!built) buildNext();
                Tween.StopAll(board);
                var camera = Camera.main;
                if (camera != null) board.position = new Vector3(camera.transform.position.x, board.position.y, board.position.z);
                IsRunning = false;
            }
        }

        private static float ScreenWidth()
        {
            var camera = Camera.main;
            return camera != null ? camera.orthographicSize * 2f * camera.aspect : 10f;
        }
    }
}
