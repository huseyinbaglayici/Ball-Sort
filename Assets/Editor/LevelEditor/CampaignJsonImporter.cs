using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Project.Contexts.Game.Levels;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.LevelEditor
{
    // Each board becomes Level_NNN_vK, replacing old assets; solutions are saved by asset name for replays.
    public static class CampaignJsonImporter
    {
        public const string SolutionsPath = "Assets/Tests/EditMode/CampaignSolutions.json";

        [MenuItem("Tools/Project/Import Campaign JSON...")]
        private static void ImportFromMenu()
        {
            var levelsPath = EditorUtility.OpenFilePanel("Campaign levels JSON", "", "json");
            if (string.IsNullOrEmpty(levelsPath)) return;
            var solutionsPath = EditorUtility.OpenFilePanel("Campaign solutions JSON", Path.GetDirectoryName(levelsPath), "json");
            if (string.IsNullOrEmpty(solutionsPath)) return;
            if (!EditorUtility.DisplayDialog("Import campaign",
                    $"Every level asset in {LevelAssetStore.LevelsFolder} is deleted and replaced by the campaign.", "Import", "Cancel"))
                return;

            try
            {
                Import(File.ReadAllText(levelsPath), File.ReadAllText(solutionsPath));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Import failed", e.Message, "OK");
            }
        }

        public static void Import(string levelsJson, string solutionsJson)
        {
            var levels = (JArray)JObject.Parse(levelsJson)["levels"] ?? throw new FormatException("JSON has no \"levels\" array.");
            var proofs = JObject.Parse(solutionsJson);

            var boards = new List<(string Name, LevelData Level)>();
            var solutions = new SolutionFile();
            foreach (var level in levels)
            {
                var number = (int)level["level"];
                var variants = (JArray)level["sourceVariants"];
                for (var v = 0; v < variants.Count; v++)
                {
                    var board = variants[v]["modes"]?["classic"] ?? throw new FormatException($"Level {number} V{v + 1} has no classic board.");
                    var name = $"Level_{number:000}_v{v + 1}";
                    boards.Add((name, ToLevel(board, name)));

                    var id = (string)board["id"];
                    var moves = proofs[id]?["moves"] as JArray ?? throw new FormatException($"No solution for {name}.");
                    var solution = new Solution { Level = name };
                    foreach (var move in moves)
                    {
                        if ((string)move["type"] != "move") throw new FormatException($"{name}: only plain moves are expected in a solution.");
                        solution.Moves.Add((int)move["from"]);
                        solution.Moves.Add((int)move["to"]);
                        solution.Moves.Add((int)move["count"]);
                    }
                    solutions.Boards.Add(solution);
                }
            }

            foreach (var old in LevelAssetStore.FindLevels()) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(old));
            foreach (var (name, level) in boards) LevelAssetStore.Save(level, name);

            Directory.CreateDirectory(Path.GetDirectoryName(SolutionsPath)!);
            File.WriteAllText(SolutionsPath, JsonUtility.ToJson(solutions));
            AssetDatabase.ImportAsset(SolutionsPath);
            AssetDatabase.SaveAssets();
            LevelCatalogBuilder.Rebuild();
            Debug.Log($"[Campaign] Imported {levels.Count} levels, {boards.Count} boards.");
        }

        private static LevelData ToLevel(JToken board, string name)
        {
            var state = board["state"];
            if ((string)state["mode"] != "classic" || state["requireVisibleCompletion"]?.Value<bool>() != true)
                throw new FormatException($"{name}: only classic boards with visible completion are supported.");
            if (((JArray)state["anchors"])?.Count > 0 || ((JArray)state["gravityPairs"])?.Count > 0)
                throw new FormatException($"{name}: anchors and gravity pairs are not part of the game.");

            var tubes = (JArray)state["tubes"];
            var capacities = (JArray)state["capacities"];
            var hidden = (JArray)state["hidden"];
            var positions = (JArray)board["positions"];
            var level = ScriptableObject.CreateInstance<LevelData>();
            for (var t = 0; t < tubes.Count; t++)
            {
                var position = positions[t];
                var bottle = BottleData.CreateEmpty((int)capacities[t], new Vector2((float)position["x"], (float)position["y"]));
                var balls = (JArray)tubes[t];
                for (var s = 0; s < balls.Count; s++)
                    bottle.Slots[s] = new SlotData
                    {
                        Color = (int)balls[s],
                        Kind = hidden?[t]?[s]?.Value<bool>() == true ? SlotKind.Special : SlotKind.Normal
                    };
                level.Bottles.Add(bottle);
            }

            foreach (var cover in state["covers"] ?? new JArray())
                level.Curtains.Add(new CurtainData { Bottle = (int)cover["tube"], Color = (int)cover["color"], Need = (int?)cover["target"] ?? 4 });

            foreach (var box in state["lockboxes"] ?? new JArray())
                level.Lockboxes.Add(new LockboxData { Id = (string)box["id"], Bottles = box["tubes"].ToObject<List<int>>() });

            foreach (var key in state["keys"] ?? new JArray())
                Edit(level, name, (int)key["tube"], (int)key["slot"], (ref SlotData slot) => slot.Key = (string)key["boxId"]);

            foreach (var bomb in state["bombs"] ?? new JArray())
            {
                var budget = (int)bomb["budget"];
                if (((int?)bomb["remaining"] ?? budget) != budget) throw new FormatException($"{name}: a bomb must start with its full fuse.");
                var lit = bomb["active"]?.Value<bool>() == true;
                Edit(level, name, (int)bomb["tube"], (int)bomb["slot"], (ref SlotData slot) =>
                {
                    slot.Fuse = budget;
                    slot.FuseLit = lit;
                });
            }
            return level;
        }

        private delegate void SlotEdit(ref SlotData slot);

        private static void Edit(LevelData level, string name, int bottle, int slot, SlotEdit edit)
        {
            var slots = level.Bottles[bottle].Slots;
            if (slot < 0 || slot >= slots.Count || slots[slot].IsEmpty) throw new FormatException($"{name}: marker on an empty slot.");
            var data = slots[slot];
            edit(ref data);
            slots[slot] = data;
        }

        // Moves as flat (from, to, count) triples.
        [Serializable]
        public sealed class Solution
        {
            public string Level;
            public List<int> Moves = new();
        }

        [Serializable]
        public sealed class SolutionFile
        {
            public List<Solution> Boards = new();
        }
    }
}
