using System;
using System.Collections.Generic;
using Project.Contexts.Game.Items;
using Project.Contexts.Game.Levels;
using UnityEngine;

namespace Project.Contexts.Game.Sort
{
    // Obstacle art kept in step with the model; what a move removes stays until its landing plays it out.
    public sealed class ObstacleMarkers
    {
        // How far a dark color's tint moves toward white on a small part.
        private const float DarkLighten = 0.25f;

        private sealed class CurtainView
        {
            public FrozenTubeItem Item;
            public int Bottle;
            // Lifted by a move still in flight.
            public bool Held;
            public bool Playing;
            // Ice gone: the tube is open again while the melt art fades.
            public bool Cleared;
        }

        private sealed class BoxView
        {
            public readonly List<ShutterItem> Items = new();
            public readonly List<int> Bottles = new();
            public string Id;
            public bool Held;
            public bool Playing;
            public bool Cleared;
            public int Running;
            // The key on its way to the lock; the shutters open when it turns.
            public KeyFlightFx Flight;
        }

        private readonly Dictionary<int, CurtainView> _curtains = new();
        private readonly Dictionary<int, BoxView> _boxes = new();
        // Bombs defused by a move still in flight.
        private readonly HashSet<int> _heldBombs = new();
        private readonly List<BombBlastFx> _blasts = new();
        private readonly Dictionary<string, Color> _lockColors = new();
        private ItemFactory _factory;
        private Transform _host;
        private BallAnimationConfig _config;
        private LevelPalette _palette;
        private Action<int> _onUncovered;
        private SortBoard _model;
        private IReadOnlyList<BottleItem> _bottles;

        // host: the board, parent of shutters and blasts. onUncovered gets a tube whose cover just went.
        public void Bind(ItemFactory factory, Transform host, BallAnimationConfig config, LevelPalette palette,
            Action<int> onUncovered)
        {
            _factory = factory;
            _host = host;
            _config = config;
            _palette = palette;
            _onUncovered = onUncovered;
        }

        // forward: the move just made, whose removed obstacles wait for Play; null shows the model as it is (Undo, Build).
        public void Refresh(SortBoard board, IReadOnlyList<BottleItem> bottles, MoveResult forward)
        {
            _model = board;
            _bottles = bottles;
            if (_factory == null) return;
            if (_lockColors.Count == 0 && board.LockboxCount > 0) AssignLockColors(board);
            if (forward == null) ReleaseBlasts();

            for (var i = 0; i < board.CurtainCount; i++)
            {
                _curtains.TryGetValue(i, out var view);
                if (board.IsCurtainActive(i))
                {
                    if (view == null) _curtains[i] = NewCurtain(board, i);
                    else if (view.Held || view.Playing) Rest(view);
                }
                else if (view != null)
                {
                    if (forward != null && forward.CurtainsLifted.Contains(i) && !view.Playing) view.Held = true;
                    if (!view.Held && !view.Playing) ReleaseCurtain(i);
                }
            }

            for (var i = 0; i < board.LockboxCount; i++)
            {
                _boxes.TryGetValue(i, out var view);
                if (board.IsLockboxActive(i))
                {
                    if (view == null) _boxes[i] = NewBox(board, i);
                    else if (view.Held || view.Playing) Rest(view);
                }
                else if (view != null)
                {
                    if (forward != null && forward.LockboxesOpened.Contains(i) && !view.Playing) view.Held = true;
                    if (!view.Held && !view.Playing) ReleaseBox(i);
                }
            }

            if (forward != null)
                foreach (var bomb in forward.BombsDefused) _heldBombs.Add(bomb);

            for (var b = 0; b < board.BottleCount && b < bottles.Count; b++)
            {
                var balls = bottles[b].Balls;
                for (var s = 0; s < board.Count(b) && s < balls.Count; s++) RefreshBall(board, balls[s], b, s);
            }
        }

        private void RefreshBall(SortBoard board, BallItem ball, int bottle, int slot)
        {
            var key = board.KeyAt(bottle, slot);
            if (key != null) ball.SetKey(key, LockColor(key));
            else if (ball.KeyId == null || !IsKeyHeld(ball.KeyId)) ball.SetKey(null, default);

            var bomb = board.BombAt(bottle, slot);
            if (bomb < 0) ball.SetBomb(BallItem.BombLook.None, 0, default, _config);
            else if (board.IsBombDefused(bomb))
            {
                if (!_heldBombs.Contains(bomb)) ball.SetBomb(BallItem.BombLook.None, 0, default, _config);
            }
            else
            {
                _heldBombs.Remove(bomb);
                var look = board.IsBombLit(bomb) ? BallItem.BombLook.Lit : BallItem.BombLook.Passive;
                ball.SetBomb(look, board.BombFuse(bomb), RingColor(ball.ColorIndex), _config);
            }
        }

        // Plays what the landed move removed: melts, shutter openings, used keys and defuses.
        public void Play(MoveResult move)
        {
            if (_model == null || _factory == null) return;
            foreach (var i in move.CurtainsLifted)
                if (_curtains.TryGetValue(i, out var view) && view.Held) Melt(i, view);
            foreach (var i in move.LockboxesOpened)
                if (_boxes.TryGetValue(i, out var view) && view.Held) Open(i, view);
            foreach (var bomb in move.BombsDefused)
            {
                if (!_heldBombs.Remove(bomb)) continue;
                var ball = BombBall(bomb);
                if (ball != null) ball.PlayDefuse(_config);
            }
        }

        // Every bomb whose fuse ran out blows up.
        public void Explode()
        {
            if (_model == null || _factory == null) return;
            for (var bomb = 0; bomb < _model.BombCount; bomb++)
            {
                if (!_model.IsBombLit(bomb) || _model.IsBombDefused(bomb) || _model.BombFuse(bomb) > 0) continue;
                var ball = BombBall(bomb);
                if (ball != null) ball.PlayExplode(_config, Blast);
            }
        }

        // A cover still shows over the tube (held or not yet cleared), so it can't be used yet.
        public bool IsCovered(int bottle)
        {
            foreach (var view in _curtains.Values)
                if (view.Bottle == bottle && (view.Held || view.Playing && !view.Cleared)) return true;
            foreach (var view in _boxes.Values)
                if ((view.Held || view.Playing && !view.Cleared) && view.Bottles.Contains(bottle)) return true;
            return false;
        }

        // Tapped while locked: its shutter ticks.
        public void Rattle(int bottle)
        {
            foreach (var view in _boxes.Values)
                if (!view.Held && !view.Playing && view.Bottles.Contains(bottle))
                    foreach (var shutter in view.Items) shutter.Rattle(_config);
        }

        public void Clear()
        {
            foreach (var i in new List<int>(_curtains.Keys)) ReleaseCurtain(i);
            foreach (var i in new List<int>(_boxes.Keys)) ReleaseBox(i);
            ReleaseBlasts();
            _heldBombs.Clear();
            _lockColors.Clear();
            _model = null;
        }

        private CurtainView NewCurtain(SortBoard board, int index)
        {
            var (bottle, color, _) = board.Curtain(index);
            var tube = _bottles[bottle];
            var item = _factory.Get<FrozenTubeItem>(ItemType.FrozenTube, tube.Art);
            item.Setup(_palette.GetOrb(color), tube.Rise, _config);
            return new CurtainView { Item = item, Bottle = bottle };
        }

        private void Rest(CurtainView view)
        {
            view.Item.Stop();
            view.Held = view.Playing = view.Cleared = false;
        }

        private void Melt(int index, CurtainView view)
        {
            view.Held = false;
            view.Playing = true;
            view.Item.PlayMelt(_config, () =>
            {
                view.Cleared = true;
                Uncovered(view.Bottle, _config.MeltHopDuration, _config.MeltHopStagger);
            }, () =>
            {
                if (_curtains.TryGetValue(index, out var current) && current == view) ReleaseCurtain(index);
            });
        }

        private void ReleaseCurtain(int index)
        {
            var view = _curtains[index];
            _curtains.Remove(index);
            view.Item.Stop();
            _factory.Release(view.Item);
        }

        // Adjacent tubes of the box in one row share a shutter.
        private BoxView NewBox(SortBoard board, int index)
        {
            var view = new BoxView { Id = board.LockboxId(index) };
            view.Bottles.AddRange(board.LockboxBottles(index));
            var color = LockColor(view.Id);
            foreach (var run in Runs(view.Bottles))
            {
                var first = _bottles[run[0]];
                var last = _bottles[run[run.Count - 1]];
                var shutter = _factory.Get<ShutterItem>(ItemType.Shutter, _host);
                shutter.Setup(first.Art, last.Art, first.Rise, color);
                view.Items.Add(shutter);
            }
            SetGlow(view, false);
            return view;
        }

        private List<List<int>> Runs(List<int> members)
        {
            var rows = new Dictionary<int, List<int>>();
            foreach (var bottle in members)
            {
                var key = RowKey(bottle);
                if (!rows.TryGetValue(key, out var row)) rows[key] = row = new List<int>();
                row.Add(bottle);
            }

            var runs = new List<List<int>>();
            foreach (var row in rows.Values)
            {
                row.Sort((a, b) => X(a).CompareTo(X(b)));
                var run = new List<int> { row[0] };
                for (var i = 1; i < row.Count; i++)
                {
                    if (HasTubeBetween(row[i - 1], row[i], members))
                    {
                        runs.Add(run);
                        run = new List<int>();
                    }
                    run.Add(row[i]);
                }
                runs.Add(run);
            }
            return runs;
        }

        private bool HasTubeBetween(int left, int right, List<int> members)
        {
            var row = RowKey(left);
            for (var b = 0; b < _bottles.Count; b++)
                if (!members.Contains(b) && RowKey(b) == row && X(b) > X(left) && X(b) < X(right)) return true;
            return false;
        }

        private int RowKey(int bottle) => Mathf.RoundToInt(_bottles[bottle].transform.localPosition.y * 100f);

        private float X(int bottle) => _bottles[bottle].transform.localPosition.x;

        private void Rest(BoxView view)
        {
            StopFlight(view);
            foreach (var shutter in view.Items) shutter.Stop();
            view.Held = view.Playing = view.Cleared = false;
            view.Running = 0;
            SetGlow(view, false);
        }

        private void Open(int index, BoxView view)
        {
            view.Held = false;
            view.Playing = true;
            BallItem carrier = null;
            foreach (var bottle in _bottles)
                foreach (var ball in bottle.Balls)
                    if (ball.KeyId == view.Id)
                    {
                        if (carrier == null && ball.KeyMark != null) carrier = ball;
                        else ball.PlayKeyUse(_config);
                    }
            if (carrier == null || view.Items.Count == 0)
            {
                OpenShutters(index, view);
                return;
            }
            // The key leaves its ball, shows itself at the screen's centre, then turns in the first padlock.
            var from = _host.InverseTransformPoint(carrier.KeyMark.position);
            var size = Mathf.Max(carrier.KeyWorldWidth, 0.1f) / Mathf.Max(_host.lossyScale.x, 0.0001f);
            var camera = Camera.main;
            var centre = camera != null ? _host.InverseTransformPoint(camera.transform.position) : Vector3.zero;
            centre.z = 0f;
            var lockAt = _host.InverseTransformPoint(view.Items[0].LockWorld);
            var lockWidth = view.Items[0].LockWorldWidth / Mathf.Max(_host.lossyScale.x, 0.0001f);
            carrier.ReleaseKey();
            var flight = _factory.Get<KeyFlightFx>(ItemType.KeyFlightFx, _host);
            view.Flight = flight;
            flight.Play(from, centre, lockAt, size, lockWidth, LockColor(view.Id), _config, () =>
            {
                if (_boxes.TryGetValue(index, out var current) && current == view) OpenShutters(index, view);
            }, done =>
            {
                if (view.Flight == done) view.Flight = null;
                _factory.Release(done);
            });
        }

        private void OpenShutters(int index, BoxView view)
        {
            view.Running = view.Items.Count;
            foreach (var shutter in view.Items)
                shutter.PlayOpen(_config, () =>
                {
                    if (view.Cleared) return;
                    view.Cleared = true;
                    SetGlow(view, true);
                    foreach (var bottle in view.Bottles)
                        Uncovered(bottle, _config.ShutterHopDuration, _config.ShutterHopStagger);
                }, () =>
                {
                    if (--view.Running > 0) return;
                    if (_boxes.TryGetValue(index, out var current) && current == view) ReleaseBox(index);
                });
        }

        // An undo or restart mid-flight: the key is gone with no open.
        private void StopFlight(BoxView view)
        {
            if (view.Flight == null) return;
            var flight = view.Flight;
            view.Flight = null;
            flight.Stop();
            _factory.Release(flight);
        }

        private void ReleaseBox(int index)
        {
            var view = _boxes[index];
            _boxes.Remove(index);
            StopFlight(view);
            foreach (var shutter in view.Items)
            {
                shutter.Stop();
                _factory.Release(shutter);
            }
            SetGlow(view, true);
        }

        private void SetGlow(BoxView view, bool visible)
        {
            foreach (var bottle in view.Bottles)
                if (bottle < _bottles.Count) _bottles[bottle].SetGlowVisible(visible);
        }

        // Balls hop bottom to top once their tube opens.
        private void Uncovered(int bottle, float hop, float stagger)
        {
            _onUncovered?.Invoke(bottle);
            var balls = _bottles[bottle].Balls;
            for (var i = 0; i < balls.Count; i++) balls[i].Hop(_config.ObstacleHopHeight, hop, i * stagger);
        }

        private bool IsKeyHeld(string id)
        {
            foreach (var view in _boxes.Values)
                if (view.Held && view.Id == id) return true;
            return false;
        }

        private BallItem BombBall(int bomb)
        {
            for (var b = 0; b < _model.BottleCount && b < _bottles.Count; b++)
            {
                var balls = _bottles[b].Balls;
                for (var s = 0; s < _model.Count(b) && s < balls.Count; s++)
                    if (_model.BombAt(b, s) == bomb) return balls[s];
            }
            return null;
        }

        private void Blast(Vector3 position)
        {
            var fx = _factory.Get<BombBlastFx>(ItemType.BombBlast, _host);
            fx.transform.position = position;
            fx.transform.localScale = Vector3.one;
            _blasts.Add(fx);
            fx.Play(_config, done =>
            {
                if (_blasts.Remove(done)) _factory.Release(done);
            });
        }

        private void ReleaseBlasts()
        {
            foreach (var fx in _blasts) _factory.Release(fx);
            _blasts.Clear();
        }

        // Box ids in order take the table's colors, skipping colors of the board's balls while enough are left.
        private void AssignLockColors(SortBoard board)
        {
            var ids = new List<string>();
            for (var i = 0; i < board.LockboxCount; i++)
                if (!ids.Contains(board.LockboxId(i))) ids.Add(board.LockboxId(i));
            ids.Sort(string.CompareOrdinal);

            var used = new HashSet<int>();
            for (var b = 0; b < board.BottleCount; b++)
                for (var s = 0; s < board.Count(b); s++)
                    used.Add(OrbColorTable.IndexOf(_palette.Name(board.ColorAt(b, s)) ?? ""));
            var free = new List<OrbColorTable.Entry>();
            for (var i = 0; i < OrbColorTable.Entries.Length; i++)
                if (!used.Contains(i)) free.Add(OrbColorTable.Entries[i]);
            if (free.Count < ids.Count) free = new List<OrbColorTable.Entry>(OrbColorTable.Entries);
            for (var i = 0; i < ids.Count; i++) _lockColors[ids[i]] = free[i % free.Count].Surface;
        }

        private Color LockColor(string id) => _lockColors.TryGetValue(id, out var color) ? color : Color.white;

        private Color RingColor(int colorIndex)
        {
            var surface = _palette.GetSurface(colorIndex);
            var entry = OrbColorTable.IndexOf(_palette.Name(colorIndex) ?? "");
            return entry >= 0 && OrbColorTable.Entries[entry].Dark ? Color.Lerp(surface, Color.white, DarkLighten) : surface;
        }
    }
}
