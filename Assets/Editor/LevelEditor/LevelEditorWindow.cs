using System;
using System.Collections.Generic;
using System.IO;
using Project.Contexts.Game.Levels;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.LevelEditor
{
    // Level list, the board preview and saving; tube editing and the solvability check live in their own parts.
    public sealed partial class LevelEditorWindow : EditorWindow
    {
        private const float ListWidth = 180f;
        private const float SideWidth = 300f;

        private readonly List<string> _errors = new();
        private List<LevelGroup> _groups = new();
        private LevelGroup _group;
        private LevelData _level;
        private LevelPalette _palette;
        private Vector2 _listScroll;
        private Vector2 _sideScroll;
        private int _selectedBottle = -1;
        private int _perRow = LevelRules.MaxPerRow;
        // Bumped on every edit, so a solvability result knows when it is out of date.
        private int _revision;

        [MenuItem("Tools/Project/Level Editor")]
        private static void Open() => GetWindow<LevelEditorWindow>("Level Editor");

        private void OnEnable()
        {
            _palette = LevelAssetStore.FindOrCreatePalette();
            Refresh();
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        private void OnUndoRedo()
        {
            _revision++;
            Repaint();
        }

        private void OnProjectChange() => Refresh();

        private void Refresh()
        {
            _groups = LevelGrouping.Build(LevelAssetStore.FindLevels());
            _group = _level != null ? LevelGrouping.Find(_groups, _level) : null;
            if (_group == null)
            {
                _level = null;
                if (_groups.Count > 0) Select(_groups[0].Variants[0].Level);
            }
            Repaint();
        }

        private void Select(LevelData level)
        {
            _level = level;
            _group = LevelGrouping.Find(_groups, level);
            _selectedBottle = -1;
            _revision++;
        }

        private void OnGUI()
        {
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLevelList();
                if (_level == null)
                {
                    EditorGUILayout.HelpBox("Create a level or import a JSON file.", MessageType.Info);
                    return;
                }
                using (new EditorGUILayout.VerticalScope()) DrawBoard();
                using (var side = new EditorGUILayout.ScrollViewScope(_sideScroll, GUILayout.Width(SideWidth)))
                {
                    _sideScroll = side.scrollPosition;
                    DrawBrush();
                    DrawSelectedBottle();
                    DrawLockboxes();
                    DrawSolvability();
                }
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50))) CreateLevel();
                if (GUILayout.Button("Import JSON...", EditorStyles.toolbarButton, GUILayout.Width(100))) ImportJson();
                if (GUILayout.Button("Rebuild Catalog", EditorStyles.toolbarButton, GUILayout.Width(110))) LevelCatalogBuilder.Rebuild();
                if (GUILayout.Button("Check All Levels", EditorStyles.toolbarButton, GUILayout.Width(115))) LevelSolver.CheckAllLayouts();
                GUILayout.FlexibleSpace();
                _palette = (LevelPalette)EditorGUILayout.ObjectField(_palette, typeof(LevelPalette), false, GUILayout.Width(160));
            }
        }

        private void DrawLevelList()
        {
            using var scroll = new EditorGUILayout.ScrollViewScope(_listScroll, GUILayout.Width(ListWidth));
            _listScroll = scroll.scrollPosition;
            foreach (var group in _groups)
            {
                var style = group == _group ? EditorStyles.boldLabel : EditorStyles.label;
                var title = group.Variants.Count > 1 ? $"{group.Title}  ({group.Variants.Count})" : group.Title;
                if (GUILayout.Button(title, style)) Select(group.Variants[0].Level);
            }
        }

        private void DrawVariantPopup()
        {
            if (_group == null || _group.Variants.Count < 2) return;

            var current = _group.Variants.FindIndex(variant => variant.Level == _level);
            var labels = new string[_group.Variants.Count];
            for (var i = 0; i < labels.Length; i++) labels[i] = _group.Variants[i].Label;

            var chosen = EditorGUILayout.Popup(_group.Title, Mathf.Max(0, current), labels);
            if (chosen != current) Select(_group.Variants[chosen].Level);
        }

        private void DrawBoard()
        {
            DrawVariantPopup();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(_level, typeof(LevelData), false);
                GUILayout.Label($"{_level.Bottles.Count} tubes, {_level.ColorCount} colors · {LevelMechanics.Of(_level)}", GUILayout.Width(320));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Tube")) Edit("Add Tube", AddBottle);
                using (new EditorGUI.DisabledScope(!HasSelection))
                    if (GUILayout.Button("Remove Selected")) Edit("Remove Tube", RemoveSelectedBottle);
                GUILayout.Label("Per row", GUILayout.Width(50));
                _perRow = Mathf.Clamp(EditorGUILayout.IntField(_perRow, GUILayout.Width(30)), 1, LevelRules.MaxPerRow);
                if (GUILayout.Button("Auto Layout")) Edit("Auto Layout", () => LevelLayout.Arrange(_level, _perRow));
            }
            EditorGUILayout.LabelField("Left click paints a slot · right click selects a tube", EditorStyles.miniLabel);

            var area = GUILayoutUtility.GetRect(10f, 10000f, 10f, 10000f);
            var hit = LevelPreview.Draw(area, _level, _palette, _selectedBottle);
            if (hit.HasValue)
            {
                _selectedBottle = hit.Value.Bottle;
                if (hit.Value.Paints) Paint(hit.Value);
                Repaint();
            }

            LevelValidator.Validate(_level, _errors);
            if (_errors.Count == 0) EditorGUILayout.HelpBox("Layout is valid.", MessageType.None);
            else EditorGUILayout.HelpBox(string.Join("\n", _errors), MessageType.Warning);
        }

        private bool HasSelection => _selectedBottle >= 0 && _selectedBottle < _level.Bottles.Count;

        private void AddBottle()
        {
            _level.Bottles.Add(BottleData.CreateEmpty(LevelRules.TubeCapacities[0], Vector2.zero));
            LevelLayout.Arrange(_level, _perRow);
        }

        // Obstacles keep pointing at the same tubes: indices above the removed one shift down.
        private void RemoveSelectedBottle()
        {
            var removed = _selectedBottle;
            _level.Bottles.RemoveAt(removed);
            _level.Curtains.RemoveAll(c => c.Bottle == removed);
            foreach (var curtain in _level.Curtains)
                if (curtain.Bottle > removed) curtain.Bottle--;
            foreach (var box in _level.Lockboxes)
            {
                box.Bottles.Remove(removed);
                for (var i = 0; i < box.Bottles.Count; i++)
                    if (box.Bottles[i] > removed) box.Bottles[i]--;
            }
            _selectedBottle = -1;
        }

        private void Edit(string undoName, Action change)
        {
            Undo.RecordObject(_level, undoName);
            change();
            EditorUtility.SetDirty(_level);
            _revision++;
            Repaint();
        }

        private void CreateLevel()
        {
            var level = CreateInstance<LevelData>();
            for (var i = 0; i < 3; i++) level.Bottles.Add(BottleData.CreateEmpty(LevelRules.TubeCapacities[0], Vector2.zero));
            LevelLayout.Arrange(level, _perRow);
            var saved = LevelAssetStore.Save(level, LevelGrouping.NextName(_groups));
            AssetDatabase.SaveAssets();
            Refresh();
            Select(saved);
        }

        private void ImportJson()
        {
            var path = EditorUtility.OpenFilePanel("Import levels JSON", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            var choice = EditorUtility.DisplayDialogComplex("Import levels",
                $"Import into {LevelAssetStore.LevelsFolder}. Existing levels with the same name are overwritten.",
                "First variant only", "Cancel", "All variants");
            if (choice == 1) return;

            List<LevelJsonImporter.ImportedLevel> imported;
            try
            {
                imported = LevelJsonImporter.Parse(File.ReadAllText(path), allVariants: choice == 2);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Import failed", e.Message, "OK");
                return;
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var item in imported) LevelAssetStore.Save(item.Level, item.Name);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();
            // Imported levels are playable at once; one made with New joins at the next rebuild.
            LevelCatalogBuilder.Rebuild();
            _level = null;
            Refresh();
            Debug.Log($"Imported {imported.Count} levels into {LevelAssetStore.LevelsFolder}.");
        }
    }
}
