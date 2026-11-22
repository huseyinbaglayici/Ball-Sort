using System;
using System.IO;
using UnityEngine;

namespace Project.Core.Save
{
    // Writes go via a temp file and keep the previous as .bak for loading, so a crash loses at most one save.
    public sealed class SaveService
    {
        private readonly string _folder;

#if UNITY_EDITOR
        // An editor tool can play on its own folder so the real save is never touched; cleared every Play.
        public static string EditorFolder { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => EditorFolder = null;
#endif

        public SaveService(string folder = null)
        {
#if UNITY_EDITOR
            folder ??= EditorFolder;
#endif
            _folder = folder ?? Path.Combine(Application.persistentDataPath, "save");
            Directory.CreateDirectory(_folder);
        }

        public T Load<T>(string key) where T : new()
        {
            var path = PathFor(key);
            if (TryRead(path, out T model)) return model;
            if (TryRead(path + ".bak", out model))
            {
                Debug.LogWarning($"[Save] '{key}' restored from backup.");
                return model;
            }
            return new T();
        }

        public void Save<T>(string key, T model)
        {
            var path = PathFor(key);
            var temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonUtility.ToJson(model));
                if (File.Exists(path)) File.Copy(path, path + ".bak", true);
                File.Copy(temp, path, true);
                File.Delete(temp);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Could not save '{key}': {e.Message}");
            }
        }

        private string PathFor(string key) => Path.Combine(_folder, key + ".json");

        private static bool TryRead<T>(string path, out T model)
        {
            model = default;
            if (!File.Exists(path)) return false;
            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return false;
                model = JsonUtility.FromJson<T>(json);
                return model != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Corrupt file '{Path.GetFileName(path)}': {e.Message}");
                return false;
            }
        }
    }
}
