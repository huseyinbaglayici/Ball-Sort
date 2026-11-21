using System;
using System.Collections.Generic;
using System.IO;
using Project.Core.Audio;
using UnityEditor;
using UnityEngine;

namespace Project.Editor
{
    // Importing AudioEvents.json or a clip under Assets/Art/Audio rebuilds the AudioLibrary's events.
    public sealed class AudioEventImporter : AssetPostprocessor
    {
        private const string AudioFolder = "Assets/Art/Audio/";
        private const string CatalogPath = AudioFolder + "AudioEvents.json";
        private const string LibraryPath = "Assets/Configs/AudioLibrary.asset";

        [Serializable]
        private sealed class Catalog
        {
            public List<CatalogEvent> events = new();
        }

        [Serializable]
        private sealed class CatalogEvent
        {
            public string id;
            public string bus;
            public string label;
            public bool enabled = true;
            public List<string> files = new();
        }

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioFolder, StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.ADPCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (!path.StartsWith(AudioFolder, StringComparison.Ordinal)) continue;
                // Clips can only be loaded once the import has finished.
                EditorApplication.delayCall -= Rebuild;
                EditorApplication.delayCall += Rebuild;
                return;
            }
        }

        [MenuItem("Tools/Project/Rebuild Audio Events")]
        public static void Rebuild()
        {
            if (!File.Exists(CatalogPath))
            {
                Debug.LogWarning($"[Audio] {CatalogPath} not found.");
                return;
            }
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogError($"[Audio] {LibraryPath} not found.");
                return;
            }

            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(CatalogPath));
            var events = new List<AudioLibrary.EventEntry>(catalog.events.Count);
            var missing = 0;
            foreach (var e in catalog.events)
            {
                // A disabled event stays out of the library, so playing it is silent.
                if (!e.enabled) continue;
                var clips = new List<AudioClip>(e.files.Count);
                foreach (var file in e.files)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(file);
                    if (clip != null) clips.Add(clip);
                    else missing++;
                }
                events.Add(new AudioLibrary.EventEntry { Id = e.id, Bus = e.bus, Clips = clips.ToArray() });
            }

            library.SetEvents(events);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
            if (missing > 0) Debug.LogWarning($"[Audio] {missing} clip(s) listed in {CatalogPath} were not found; import may still be running.");
            else Debug.Log($"[Audio] {events.Count} sound events rebuilt from {CatalogPath}.");
        }
    }
}
