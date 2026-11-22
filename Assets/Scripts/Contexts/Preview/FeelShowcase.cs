#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Project.Common.UI;
using Project.Common.UI.PopUps;
using Project.Contexts.Application;
using Project.Contexts.Application.Economy;
using Project.Contexts.Game;
using Project.Contexts.Home;
using Project.Contexts.LiveOps;
using Project.Contexts.Main;
using Project.Core.Debugging;
using Project.Core.Save;
using Project.Core.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Contexts.Preview
{
    // Editor scene driver: boots the real game on a throwaway save and plays any feel from one panel.
    public sealed class FeelShowcase : MonoBehaviour
    {
        private const string BoardScenePath = "Assets/Scenes/Debug/TubeFxPreview.unity";
        private const string MainScene = "Main";
        private const string PendingKey = "FeelShowcase.PendingRestore";
        private const int SeedLevel = 40;
        private const float GuiHeight = 1280f;
        private const float SlowMotion = 0.25f;
        private const float Settle = 0.35f;
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly List<(string Title, List<(string Label, Func<IEnumerator> Run)> Items)> _sections = new();
        private readonly List<GameObject> _hidden = new();
        private Camera _mainCamera;
        private Scene _boardScene;
        private bool _ready;
        private bool _open = true;
        private bool _boardMode;
        private bool _busy;
        private bool _slow;
        private GUIStyle _button, _header, _status;
        private Texture2D _backing;

        private static string SaveFolder => Path.Combine(UnityEngine.Application.persistentDataPath, "save");
        private static string BackupFolder => Path.Combine(UnityEngine.Application.persistentDataPath, "save_showcase_backup");

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SwapInSeedSave();
            BuildSections();
            SceneManager.LoadScene(0);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (_backing != null) Destroy(_backing);
        }

        private void Update()
        {
            if (_ready || !Services.TryGet<MainFlow>(out var flow) || flow.Current == MainState.None) return;
            _ready = true;
            Services.Get<OrbitRushService>().Join();
        }

        private void BuildSections()
        {
            Add("Home",
                ("Settings", () => Home(() => Click(Find<HomeScreen>(), "_settingsButton"))),
                ("Language", () => SettingsPage("_languageRow")),
                ("How to play", () => SettingsPage("_helpRow")),
                ("Version toast", () => SettingsPage("_footer")),
                ("Daily reward", DailyReward),
                ("Info popup", () => Home(() => Click(Find<HomeScreen>(), "_challengeButton"))));
            Add("Orbit Rush",
                ("Race screen", () => Home(() => Call(Find<LiveOpsHome>(), "ShowOrbitRush"))),
                ("Points fly in", RushCollect),
                ("Stage won", RushStageWon));
            Add("Transitions",
                ("Home > level", () => Switch(MainState.Home, MainState.Game)),
                ("Level > home", () => Switch(MainState.Game, MainState.Home)),
                ("Board entrance", BoardEntrance));
            Add("Level",
                ("Win", () => Game(() => DebugPanel.Run("Win level"))),
                ("Lose + peek", () => Game(() => DebugPanel.Run("Lose level"))),
                ("Side menu", () => Game(() => Field<SettingsMenu>(Find<GameController>(), "_settings")?.SetOpen(true))));
            Add("Board",
                ("Board FX scene", EnterBoard));
        }

        private void Add(string title, params (string, Func<IEnumerator>)[] items) =>
            _sections.Add((title, new List<(string, Func<IEnumerator>)>(items)));

        private IEnumerator Home(Action action)
        {
            yield return Ensure(MainState.Home);
            action();
        }

        // A level that already ended starts over first, as its debug actions only run mid-level.
        private IEnumerator Game(Action action)
        {
            yield return Ensure(MainState.Game);
            if (!IsPlaying())
            {
                StartLevel();
                yield return new WaitForSecondsRealtime(1f);
            }
            action();
        }

        private IEnumerator BoardEntrance()
        {
            yield return Ensure(MainState.Game);
            StartLevel();
        }

        private static object LevelStates() => Field<object>(Find<GameController>(), "_states");

        private static bool IsPlaying()
        {
            var states = LevelStates();
            return states?.GetType().GetProperty("CurrentKey")?.GetValue(states)?.ToString() == "Playing";
        }

        // Entering Playing again builds the level and plays its entrance, with no life spent.
        private static void StartLevel()
        {
            var states = LevelStates();
            if (states == null) return;
            var key = Enum.Parse(states.GetType().GetGenericArguments()[0], "Playing");
            states.GetType().GetMethod("ChangeState")?.Invoke(states, new[] { key });
        }

        private IEnumerator Switch(MainState from, MainState to)
        {
            yield return Ensure(from);
            var flow = Services.Get<MainFlow>();
            _ = to == MainState.Game ? flow.GoToGameAsync() : flow.GoHomeAsync();
        }

        // Closes popups and waits until the wanted screen is open and still.
        private static IEnumerator Ensure(MainState state)
        {
            var flow = Services.Get<MainFlow>();
            Services.Get<PopupService>().CloseAll();
            if (flow.Current != state && !flow.IsSwitching)
                _ = state == MainState.Game ? flow.GoToGameAsync() : flow.GoHomeAsync();
            while (flow.IsSwitching || flow.Current != state) yield return null;
            yield return new WaitForSecondsRealtime(Settle);
        }

        private IEnumerator SettingsPage(string row)
        {
            yield return Home(() => Click(Find<HomeScreen>(), "_settingsButton"));
            yield return new WaitForSecondsRealtime(0.6f);
            Click(Find<SettingsPopup>(), row);
        }

        // Yesterday was claimed, so today's cell is ready and the streak goes on.
        private IEnumerator DailyReward()
        {
            var daily = Services.Get<PlayerData>().Model.Daily;
            var today = DailyRewardService.DayNumber(DateTime.Now);
            if (daily.LastClaimDay >= today) daily.LastClaimDay = today - 1;
            yield return Home(() => Click(Find<LiveOpsHome>(), "_daily"));
        }

        private IEnumerator RushCollect()
        {
            yield return Ensure(MainState.Home);
            var rush = Racing();
            rush.RecordWin();
            rush.RecordWin();
            Call(Find<LiveOpsHome>(), "PlayCollect");
        }

        private IEnumerator RushStageWon()
        {
            yield return Ensure(MainState.Home);
            var rush = Racing();
            for (var i = 0; i < 30 && rush.State == OrbitRushState.Racing; i++) rush.RecordWin();
            rush.MarkCollected();
            Call(Find<LiveOpsHome>(), "ShowOrbitRush");
        }

        // Any other state starts the race over from its first stage.
        private static OrbitRushService Racing()
        {
            var rush = Services.Get<OrbitRushService>();
            if (rush.State == OrbitRushState.Racing) return rush;
            Services.Get<PlayerData>().Model.OrbitRush = new OrbitRushModel();
            rush.MarkIntroShown();
            rush.Join();
            return rush;
        }

        // The tube effects scene opens over Main with Main's screens hidden.
        private IEnumerator EnterBoard()
        {
            yield return Ensure(MainState.Home);
            _hidden.Clear();
            foreach (var root in SceneManager.GetSceneByName(MainScene).GetRootGameObjects())
                if (root.activeSelf && root.GetComponent<Canvas>() != null) Hide(root);
            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                foreach (Transform child in _mainCamera.transform) Hide(child.gameObject);
                _mainCamera.enabled = false;
            }
            SceneManager.sceneLoaded += OnBoardLoaded;
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(BoardScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!load.isDone) yield return null;
            _boardMode = true;
        }

        // Before its Start, so the objects it makes land in its own scene.
        private void OnBoardLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != BoardScenePath) return;
            SceneManager.sceneLoaded -= OnBoardLoaded;
            _boardScene = scene;
            SceneManager.SetActiveScene(scene);
        }

        private IEnumerator LeaveBoard()
        {
            var unload = SceneManager.UnloadSceneAsync(_boardScene);
            while (unload != null && !unload.isDone) yield return null;
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(MainScene));
            foreach (var go in _hidden)
                if (go != null) go.SetActive(true);
            _hidden.Clear();
            if (_mainCamera != null) _mainCamera.enabled = true;
            Time.timeScale = _slow ? SlowMotion : 1f;
            _boardMode = false;
        }

        private void Hide(GameObject go)
        {
            go.SetActive(false);
            _hidden.Add(go);
        }

        private void Run(Func<IEnumerator> run)
        {
            if (_busy) return;
            _open = false;
            StartCoroutine(Guard(run()));
        }

        private IEnumerator Guard(IEnumerator routine)
        {
            _busy = true;
            yield return routine;
            _busy = false;
        }

        private void OnGUI()
        {
            if (!_ready) return;
            var scale = Screen.height / GuiHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            _button ??= new GUIStyle(GUI.skin.button) { fontSize = 22 };
            _header ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
            _status ??= new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

            if (_boardMode)
            {
                if (!_busy && GUI.Button(new Rect(20f, 86f, 260f, 50f), "< Back to game", _button)) Run(LeaveBoard);
                return;
            }

            if (GUI.Button(new Rect(width - 200f, 4f, 180f, 44f), _open ? "Feels ^" : "Feels v", _button)) _open = !_open;
            SetUiInput(!_open);
            if (!_open) return;

            const float columns = 3f;
            const float row = 58f;
            var height = 70f;
            foreach (var section in _sections) height += 34f + Mathf.Ceil(section.Items.Count / columns) * row;
            height += row;
            var panel = new Rect(12f, 54f, width - 24f, height);
            _backing ??= Backing();
            GUI.DrawTexture(panel, _backing);

            var flow = Services.Get<MainFlow>();
            var cell = (panel.width - 24f) / columns;
            var y = panel.y + 10f;
            GUI.Label(new Rect(panel.x, y, panel.width, 30f), $"{flow.Current}   level {Services.Get<ProgressService>().Level}   speed {Time.timeScale:0.##}x", _status);
            y += 40f;
            foreach (var (title, items) in _sections)
            {
                GUI.Label(new Rect(panel.x + 14f, y, panel.width, 30f), title, _header);
                y += 34f;
                for (var i = 0; i < items.Count; i++)
                {
                    var rect = new Rect(panel.x + 12f + cell * (i % 3), y + row * (i / 3), cell - 8f, row - 8f);
                    if (GUI.Button(rect, items[i].Label, _button)) Run(items[i].Run);
                }
                y += Mathf.Ceil(items.Count / columns) * row;
            }
            var half = (panel.width - 24f) / 2f;
            if (GUI.Button(new Rect(panel.x + 12f, y, half - 8f, row - 8f), _slow ? "[0.25x]" : "0.25x", _button))
            {
                _slow = !_slow;
                Time.timeScale = _slow ? SlowMotion : 1f;
            }
            if (GUI.Button(new Rect(panel.x + 12f + half, y, half - 8f, row - 8f), "Close popups", _button))
                Services.Get<PopupService>().CloseAll();
        }

        private static Texture2D Backing()
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, new Color(0.07f, 0.05f, 0.16f, 0.94f));
            texture.Apply();
            return texture;
        }

        // Taps on the open panel must not reach the game's buttons under it.
        private static void SetUiInput(bool on)
        {
            var events = EventSystem.current;
            if (events != null && events.enabled != on) events.enabled = on;
        }

        private static T Find<T>() where T : UnityEngine.Object => FindAnyObjectByType<T>(FindObjectsInactive.Include);

        private static T Field<T>(object owner, string name) where T : class =>
            owner?.GetType().GetField(name, Private)?.GetValue(owner) as T;

        private static void Click(object owner, string field)
        {
            var button = Field<Button>(owner, field);
            if (button == null) Debug.LogWarning($"[FeelShowcase] No button '{field}' on {owner?.GetType().Name}.");
            else button.onClick.Invoke();
        }

        private static void Call(object owner, string method) => owner?.GetType().GetMethod(method, Private)?.Invoke(owner, null);

        // The real save is set aside for the session and comes back when Play stops.
        private static void SwapInSeedSave()
        {
            if (SessionState.GetBool(PendingKey, false)) RestoreSave();
            if (Directory.Exists(BackupFolder)) Directory.Delete(BackupFolder, true);
            Directory.CreateDirectory(SaveFolder);
            CopyFolder(SaveFolder, BackupFolder);
            SessionState.SetBool(PendingKey, true);
            foreach (var file in Directory.GetFiles(SaveFolder, PlayerData.Key + ".json*")) File.Delete(file);

            var model = new ProgressModel { Level = SeedLevel, Coins = 5000, BoosterStock = new[] { 5, 5, 5 } };
            model.Lives.UnlimitedUntilTicks = DateTime.UtcNow.AddDays(7).Ticks;
            var today = DailyRewardService.DayNumber(DateTime.Now);
            model.Daily.Streak = 2;
            model.Daily.LastClaimDay = today - 1;
            model.Daily.LastAutoShownDay = today;
            new SaveService().Save(PlayerData.Key, model);
        }

        private static void RestoreSave()
        {
            SessionState.SetBool(PendingKey, false);
            if (!Directory.Exists(BackupFolder)) return;
            if (Directory.Exists(SaveFolder)) Directory.Delete(SaveFolder, true);
            Directory.Move(BackupFolder, SaveFolder);
            Debug.Log("[FeelShowcase] Your save is back.");
        }

        private static void CopyFolder(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        [InitializeOnLoadMethod]
        private static void WatchPlayMode()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(PendingKey, false)) RestoreSave();
            };
            if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(PendingKey, false)) RestoreSave();
        }
    }
}
#endif
