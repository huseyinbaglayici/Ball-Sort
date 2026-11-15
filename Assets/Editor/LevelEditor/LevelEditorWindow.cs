using System;
using System.Collections.Generic;
using System.IO;
using Project.Contexts.Game.Levels;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.LevelEditor
{
    /// <summary>
    /// Lists level assets, previews the selected one and paints slots with a brush.
    /// Drawing, parsing, layout and disk access live in their own classes; this window only wires them to the UI.
    /// </summary>
    public sealed class LevelEditorWindow : EditorWindow
    {
        private const float ListWidth = 180f;

        private readonly List<string> _errors = new();
        private List<LevelGroup> _groups = new();
        private LevelGroup _group;
        private LevelData _level;
        private LevelPalette _palette;
        private Vector2 _listScroll;
        private int _selectedBottle = -1;
        private int _perRow = 5;

        private SlotData _brush = new() { Color = 0 };

        [MenuItem("Tools/Project/Level Editor")]
        private static void Open() => GetWindow<LevelEditorWindow>("Level Editor");

        private void OnEnable()
        {
            _palette = LevelAssetStore.FindOrCreatePalette();
            Refresh();
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable() => Undo.undoRedoPerformed -= Repaint;

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
        }

        private void OnGUI()
        {
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLevelList();
                using (new EditorGUILayout.VerticalScope())
                {
                    if (_level == null) EditorGUILayout.HelpBox("Create a level or import a JSON file.", MessageType.Info);
                    else DrawLevel();
                }
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50))) CreateLevel();
                if (GUILayout.Button("Import JSON...", EditorStyles.toolbarButton, GUILayout.Width(100))) ImportJson();
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

        private void DrawLevel()
        {
            DrawVariantPopup();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(_level, typeof(LevelData), false);
                GUILayout.Label($"{_level.Bottles.Count} bottles, {_level.ColorCount} colors", GUILayout.Width(140));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var capacity = EditorGUILayout.IntField("Capacity", _level.Capacity);
                if (capacity != _level.Capacity) Edit("Change Capacity", () => _level.Capacity = capacity);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Bottle")) Edit("Add Bottle", AddBottle);
                using (new EditorGUI.DisabledScope(_selectedBottle < 0 || _selectedBottle >= _level.Bottles.Count))
                    if (GUILayout.Button("Remove Selected")) Edit("Remove Bottle", RemoveSelectedBottle);
                _perRow = Mathf.Max(1, EditorGUILayout.IntField(_perRow, GUILayout.Width(30)));
                if (GUILayout.Button("Auto Layout")) Edit("Auto Layout", () => LevelLayout.Arrange(_level, _perRow));
            }

            DrawBrush();

            var area = GUILayoutUtility.GetRect(10f, 10000f, 10f, 10000f);
            var hit = LevelPreview.Draw(area, _level, _palette, _selectedBottle);
            if (hit.HasValue) Paint(hit.Value);

            LevelValidator.Validate(_level, _errors);
            if (_errors.Count == 0) EditorGUILayout.HelpBox("Level is valid.", MessageType.None);
            else EditorGUILayout.HelpBox(string.Join("\n", _errors), MessageType.Warning);
        }

        private void DrawBrush()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Brush", GUILayout.Width(40));
                if (GUILayout.Toggle(_brush.IsEmpty, "Erase", EditorStyles.miniButton, GUILayout.Width(44))) _brush.Color = SlotData.EmptyColor;
                for (var i = 0; i < _palette.Count; i++)
                {
                    var old = GUI.backgroundColor;
                    GUI.backgroundColor = _palette.Get(i);
                    if (GUILayout.Toggle(_brush.Color == i, i.ToString(), EditorStyles.miniButton, GUILayout.Width(26))) _brush.Color = i;
                    GUI.backgroundColor = old;
                }
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _brush.Kind = (SlotKind)EditorGUILayout.EnumPopup("Kind", _brush.Kind);
                _brush.Hidden = EditorGUILayout.ToggleLeft("Hidden (?)", _brush.Hidden, GUILayout.Width(90));
            }
        }

        private void Paint(LevelPreview.SlotHit hit)
        {
            _selectedBottle = hit.Bottle;
            var slot = _brush.IsEmpty ? SlotData.Empty : _brush;
            Edit("Paint Slot", () => _level.Bottles[hit.Bottle].Slots[hit.Slot] = slot);
        }

        private void AddBottle()
        {
            _level.Bottles.Add(BottleData.CreateEmpty(_level.Capacity, Vector2.zero));
            LevelLayout.Arrange(_level, _perRow);
        }

        private void RemoveSelectedBottle()
        {
            _level.Bottles.RemoveAt(_selectedBottle);
            _selectedBottle = -1;
        }

        private void Edit(string undoName, Action change)
        {
            Undo.RecordObject(_level, undoName);
            change();
            EditorUtility.SetDirty(_level);
            Repaint();
        }

        private void CreateLevel()
        {
            var level = CreateInstance<LevelData>();
            for (var i = 0; i < 3; i++) level.Bottles.Add(BottleData.CreateEmpty(level.Capacity, Vector2.zero));
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
            _level = null;
            Refresh();
            Debug.Log($"Imported {imported.Count} levels into {LevelAssetStore.LevelsFolder}.");
        }
    }
}
