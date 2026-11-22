using System;
using System.IO;
using System.Reflection;
using Project.Contexts.Game;
using Project.Contexts.Main;
using Project.Core.Services;
using UnityEditor;
using UnityEngine;

namespace Project.Editor
{
    // In Play on Home: presses Play and saves one game-view frame every 50 ms of the level-open to Recordings/LevelOpen.
    public static class LevelOpenRecorder
    {
        private const string Folder = "Recordings/LevelOpen/v3";
        private const float Step = 0.05f;
        private const int Frames = 32;

        private static int _frame;
        private static int _lastFrameCount;
        private static string _prefix;
        private static float _start;

        [MenuItem("Tools/Project/Record Level Open (1170x2532)")]
        private static void RecordOpenPhone() => Record(1170, 2532, true);

        [MenuItem("Tools/Project/Record Level Open (1080x1920)")]
        private static void RecordOpenWide() => Record(1080, 1920, true);

        [MenuItem("Tools/Project/Record Level Close (1170x2532)")]
        private static void RecordClosePhone() => Record(1170, 2532, false);

        [MenuItem("Tools/Project/Record Level Close (1080x1920)")]
        private static void RecordCloseWide() => Record(1080, 1920, false);

        [MenuItem("Tools/Project/Record Level Restart (1170x2532)")]
        private static void RecordRestartPhone() => Record(1170, 2532, false, restart: true);

        // Open records Home to board from a Play press; close records board to Home; restart records the board rebuilding.
        private static void Record(int width, int height, bool open, bool restart = false)
        {
            var flow = EditorApplication.isPlaying ? Services.Get<MainFlow>() : null;
            if (flow == null || flow.Current != (open ? MainState.Home : MainState.Game))
            {
                Debug.LogError($"[LevelOpen] Play the game and stay on {(open ? "Home" : "the board")} first.");
                return;
            }
            SetGameViewSize(width, height);
            Directory.CreateDirectory(Folder);
            _prefix = $"{(restart ? "restart" : open ? "open" : "close")}_{width}x{height}_";
            foreach (var old in Directory.GetFiles(Folder, _prefix + "*.png")) File.Delete(old);
            _frame = 0;
            _lastFrameCount = Time.frameCount;
            _start = Time.unscaledTime;
            Time.captureDeltaTime = Step;
            EditorApplication.update += Tick;
            // The next frame starts the move, so frame 0 is the screen as pressed.
            EditorApplication.delayCall += () =>
            {
                if (restart) Restart();
                else _ = open ? flow.GoToGameAsync() : flow.GoHomeAsync();
            };
        }

        // The controller's own restart, as the HUD would call it.
        private static void Restart()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<GameController>();
            typeof(GameController).GetMethod("Restart", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
        }

        private static void Tick()
        {
            if (Time.frameCount == _lastFrameCount) return;
            _lastFrameCount = Time.frameCount;
            // Named by the frame's own unscaled time, which is what the tweens follow.
            ScreenCapture.CaptureScreenshot($"{Folder}/{_prefix}{_frame:000}_{(Time.unscaledTime - _start) * 1000f:0000}ms.png");
            if (++_frame < Frames) return;
            EditorApplication.update -= Tick;
            Time.captureDeltaTime = 0f;
            Debug.Log($"[LevelOpen] {Frames} frames saved under {Folder}.");
        }

        // Picks (adding if needed) a fixed game-view size, so captures match the device resolution.
        private static void SetGameViewSize(int width, int height)
        {
            try
            {
                var assembly = typeof(EditorWindow).Assembly;
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                var group = sizesType.GetProperty("currentGroup", BindingFlags.Public | BindingFlags.Instance).GetValue(sizes);
                var groupType = group.GetType();
                var count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
                var index = -1;
                for (var i = 0; i < count; i++)
                {
                    var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                    var w = (int)size.GetType().GetProperty("width").GetValue(size);
                    var h = (int)size.GetType().GetProperty("height").GetValue(size);
                    var type = (int)size.GetType().GetProperty("sizeType").GetValue(size);
                    if (w == width && h == height && type == 1)
                    {
                        index = i;
                        break;
                    }
                }
                if (index < 0)
                {
                    var sizeType = assembly.GetType("UnityEditor.GameViewSize");
                    var enumType = assembly.GetType("UnityEditor.GameViewSizeType");
                    var size = Activator.CreateInstance(sizeType, Enum.ToObject(enumType, 1), width, height, $"Capture {width}x{height}");
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
                }
                var viewType = assembly.GetType("UnityEditor.GameView");
                var view = EditorWindow.GetWindow(viewType);
                viewType.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(view, new object[] { index, null });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LevelOpen] Game view size left as is: {e.Message}");
            }
        }
    }
}
