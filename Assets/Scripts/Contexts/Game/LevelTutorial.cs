using System.Collections;
using System.Collections.Generic;
using Project.Common.UI;
using Project.Common.UI.PopUps;
using Project.Contexts.Application;
using Project.Contexts.Application.Economy;
using Project.Contexts.Game.Levels;
using Project.Contexts.Game.Sort;
using Project.Contexts.Main;
using Project.Core.Events;
using Project.Core.Services;
using UnityEngine;

namespace Project.Contexts.Game
{
    // First-time teaching over the board: the tap lesson, the same-colour marks and the new item and booster cards.
    public sealed class LevelTutorial : MonoBehaviour
    {
        // Hand spawn: grows from 0.25 to 0.55 of its size in 0.4 s, OutBack.
        private const float SpawnFrom = 0.25f / 0.55f;
        private const float SpawnDuration = 0.4f;
        // Tap loop on the target.
        private const float PressScale = 0.86f;
        private const float PressDuration = 0.14f;
        private const float ReleaseDuration = 0.2f;
        private const float TapRest = 0.55f;
        // Fingertip on the tube body, a little right of its middle.
        private static readonly Vector2 FingerOnTube = new(0.12f, 0.45f);
        // Line under the lowest tube and marks over each mouth, in canvas units.
        private const float LineGap = 110f;
        private const float MarkGap = 95f;

        [SerializeField] private BoardView _board;
        [SerializeField] private LevelCatalog _catalog;
        [SerializeField] private RectTransform _finger;
        [SerializeField] private RectTransform _line;
        [SerializeField] private LocalizedText _lineText;
        [SerializeField] private RectTransform _yes;
        [SerializeField] private RectTransform _no;
        [SerializeField] private IntroPopup _introPopup;

        private enum Lesson
        {
            None,
            Tap,
            SameColor
        }

        private readonly List<(RectTransform Mark, int Bottle, bool Yes)> _marks = new();
        private readonly Stack<RectTransform> _freeYes = new();
        private readonly Stack<RectTransform> _freeNo = new();
        private Canvas _canvas;
        private TutorialService _tutorial;
        private SortBoard _model;
        private Lesson _lesson;
        private int _fingerOn = -1;
        private int _left = -1;
        private int _right = -1;
        private Coroutine _start;
        private PrimeTween.Tween _spawn;
        private PrimeTween.Sequence _tap;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            Services.TryGet(out _tutorial);
            _yes.gameObject.SetActive(false);
            _no.gameObject.SetActive(false);
            HideAll();
        }

        private void OnEnable()
        {
            EventBus<LevelStarted>.Subscribe(OnLevelStarted);
            EventBus<BottleSelected>.Subscribe(OnSelected);
            EventBus<BottleDeselected>.Subscribe(OnDeselected);
            EventBus<MoveMade>.Subscribe(OnMoveMade);
            EventBus<LevelLeft>.Subscribe(OnLevelLeft);
            EventBus<LevelCompleted>.Subscribe(OnLevelCompleted);
        }

        private void OnDisable()
        {
            EventBus<LevelStarted>.Unsubscribe(OnLevelStarted);
            EventBus<BottleSelected>.Unsubscribe(OnSelected);
            EventBus<BottleDeselected>.Unsubscribe(OnDeselected);
            EventBus<MoveMade>.Unsubscribe(OnMoveMade);
            EventBus<LevelLeft>.Unsubscribe(OnLevelLeft);
            EventBus<LevelCompleted>.Unsubscribe(OnLevelCompleted);
            Stop();
        }

        private void LateUpdate()
        {
            if (_model == null) return;
            if (_fingerOn >= 0) _finger.anchoredPosition = OnCanvas(_board.TubePoint(_fingerOn, FingerOnTube.x, FingerOnTube.y));
            if (_line.gameObject.activeSelf) _line.anchoredPosition = UnderBoard();
            foreach (var (mark, bottle, _) in _marks) mark.anchoredPosition = OnCanvas(_board.TubePoint(bottle, 0f, 1f)) + Vector2.up * MarkGap;
        }

        private void OnLevelStarted(LevelStarted evt)
        {
            Stop();
            _model = evt.Model;
            if (_tutorial == null) return;
            var lesson = evt.Level == TutorialService.TapLevel ? Lesson.Tap
                : evt.Level == TutorialService.SameColorLevel ? Lesson.SameColor
                : Lesson.None;
            var met = LevelMechanics.Seen(_catalog, evt.Level - 1);
            var intro = _tutorial.Pick(evt.Level, LevelMechanics.Of(evt.Data), met);
            if (lesson == Lesson.None && intro.IsNone) return;
            // Saved before it opens, so quitting on the card never shows it twice.
            _tutorial.MarkShown(intro);
            _start = StartCoroutine(Begin(lesson, intro));
        }

        // Waits for the switch and the board entrance to finish.
        private IEnumerator Begin(Lesson lesson, Intro intro)
        {
            Services.TryGet<MainFlow>(out var flow);
            while ((flow != null && flow.IsSwitching) || _board.IsAnimating) yield return null;
            _start = null;
            if (!intro.IsNone) ShowIntro(intro);
            _lesson = lesson;
            if (lesson == Lesson.Tap) StartTap();
            else if (lesson == Lesson.SameColor) ShowLine("tutorial.same");
        }

        public void ShowIntro(Intro intro)
        {
            if (intro.IsNone || !Services.TryGet<PopupService>(out var popups)) return;
            popups.Show(_introPopup, intro.IsBooster ? BoosterCard(intro.Booster) : MechanicCard(intro.Mechanic));
        }

        private static IntroArgs MechanicCard(Mechanic mechanic) => mechanic switch
        {
            Mechanic.HiddenBall => new IntroArgs("intro.new_item", "intro.hidden", "help.hidden", "Hidden"),
            Mechanic.FrozenTube => new IntroArgs("intro.new_item", "intro.frozen", "help.frozen", "Frozen"),
            Mechanic.ShutterKey => new IntroArgs("intro.new_item", "intro.shutter", "help.shutter", "Shutter"),
            Mechanic.Bomb => new IntroArgs("intro.new_item", "intro.bomb", "help.bomb", "Bomb"),
            _ => new IntroArgs("intro.new_item", "intro.tower", "help.tower", "Tower")
        };

        private static IntroArgs BoosterCard(BoosterType type) => type switch
        {
            BoosterType.Undo => new IntroArgs("intro.new_booster", "booster.undo", "intro.undo", "Undo"),
            BoosterType.ExtraTube => new IntroArgs("intro.new_booster", "booster.extra_tube", "intro.extra_tube", "ExtraTube"),
            _ => new IntroArgs("intro.new_booster", "booster.free_move", "intro.free_move", "FreeMove")
        };

        // Lesson one: lift from the bottom-left tube, then drop on its right neighbour.
        private void StartTap()
        {
            FindBottomPair(out _left, out _right);
            if (_left < 0) return;
            ShowLine("tutorial.pick");
            PointAt(_left);
        }

        private void OnSelected(BottleSelected evt)
        {
            if (_lesson == Lesson.Tap && _left >= 0)
            {
                var onLeft = evt.Bottle == _left && _right >= 0;
                ShowLine(onLeft ? "tutorial.drop_right" : "tutorial.drop_left");
                PointAt(onLeft ? _right : _left);
            }
            else if (_lesson == Lesson.SameColor) ShowMarks(evt.Bottle);
        }

        private void OnDeselected(BottleDeselected evt)
        {
            if (_lesson == Lesson.Tap) StartTap();
            else if (_lesson == Lesson.SameColor) ClearMarks();
        }

        private void OnMoveMade(MoveMade evt)
        {
            if (_lesson == Lesson.Tap)
            {
                _lesson = Lesson.None;
                HideAll();
            }
            else if (_lesson == Lesson.SameColor)
            {
                ClearMarks();
                if (_model != null && _model.IsSolved()) Stop();
            }
        }

        private void OnLevelLeft(LevelLeft _) => Stop();

        // A win by any path (debug included) ends the lesson.
        private void OnLevelCompleted(LevelCompleted _) => Stop();

        // A tick over every tube the lifted balls can go to, a cross over the rest.
        private void ShowMarks(int from)
        {
            ClearMarks();
            if (_model == null) return;
            for (var to = 0; to < _board.BottleCount && to < _model.BottleCount; to++)
            {
                if (to == from) continue;
                var yes = _model.CanMove(from, to);
                var free = yes ? _freeYes : _freeNo;
                var mark = free.Count > 0 ? free.Pop() : Instantiate(yes ? _yes : _no, _yes.parent);
                mark.gameObject.SetActive(true);
                mark.anchoredPosition = OnCanvas(_board.TubePoint(to, 0f, 1f)) + Vector2.up * MarkGap;
                _marks.Add((mark, to, yes));
            }
        }

        private void ClearMarks()
        {
            foreach (var (mark, _, yes) in _marks)
            {
                mark.gameObject.SetActive(false);
                (yes ? _freeYes : _freeNo).Push(mark);
            }
            _marks.Clear();
        }

        private void PointAt(int bottle)
        {
            _fingerOn = bottle;
            _finger.anchoredPosition = OnCanvas(_board.TubePoint(bottle, FingerOnTube.x, FingerOnTube.y));
            _finger.gameObject.SetActive(true);
            StopHand();
            _finger.localScale = Vector3.one * SpawnFrom;
            _spawn = PrimeTween.Tween.Scale(_finger, 1f, SpawnDuration, PrimeTween.Ease.OutBack)
                .OnComplete(this, tutorial => tutorial.StartTapLoop());
        }

        private void StartTapLoop() =>
            _tap = PrimeTween.Sequence.Create(cycles: -1)
                .ChainDelay(TapRest)
                .Chain(PrimeTween.Tween.Scale(_finger, PressScale, PressDuration, PrimeTween.Ease.OutQuad))
                .Chain(PrimeTween.Tween.Scale(_finger, 1f, ReleaseDuration, PrimeTween.Ease.OutQuad));

        private void StopHand()
        {
            _spawn.Stop();
            _tap.Stop();
        }

        private void ShowLine(string key)
        {
            _lineText.Key = key;
            _line.gameObject.SetActive(true);
            _line.anchoredPosition = UnderBoard();
        }

        private void Stop()
        {
            if (_start != null) StopCoroutine(_start);
            _start = null;
            _lesson = Lesson.None;
            _model = null;
            HideAll();
        }

        private void HideAll()
        {
            StopHand();
            _fingerOn = -1;
            _finger.gameObject.SetActive(false);
            _line.gameObject.SetActive(false);
            ClearMarks();
        }

        // Leftmost two tubes of the lowest row.
        private void FindBottomPair(out int left, out int right)
        {
            left = right = -1;
            var count = _model != null ? Mathf.Min(_board.BottleCount, _model.BottleCount) : 0;
            var bottom = float.MaxValue;
            for (var i = 0; i < count; i++) bottom = Mathf.Min(bottom, _board.TubePoint(i, 0f, 0f).y);
            const float sameRow = 0.1f;
            for (var i = 0; i < count; i++)
            {
                var point = _board.TubePoint(i, 0f, 0f);
                if (point.y > bottom + sameRow) continue;
                if (left < 0 || point.x < _board.TubePoint(left, 0f, 0f).x)
                {
                    right = left;
                    left = i;
                }
                else if (right < 0 || point.x < _board.TubePoint(right, 0f, 0f).x) right = i;
            }
        }

        private Vector2 UnderBoard()
        {
            var bottom = float.MaxValue;
            var count = _model != null ? Mathf.Min(_board.BottleCount, _model.BottleCount) : 0;
            for (var i = 0; i < count; i++) bottom = Mathf.Min(bottom, OnCanvas(_board.TubePoint(i, 0f, 0f)).y);
            return new Vector2(0f, (count > 0 ? bottom : 0f) - LineGap);
        }

        private Vector2 OnCanvas(Vector3 world)
        {
            var camera = Camera.main;
            if (camera == null) return Vector2.zero;
            var parent = (RectTransform)_finger.parent;
            var ui = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, camera.WorldToScreenPoint(world), ui, out var local);
            return local;
        }
    }
}
