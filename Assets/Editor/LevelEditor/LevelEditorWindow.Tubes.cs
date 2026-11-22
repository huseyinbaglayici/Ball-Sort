using System.Collections.Generic;
using System.Linq;
using Project.Contexts.Game.Levels;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.LevelEditor
{
    // Editing what the game plays: the slot brush, a tube's size and obstacles, and the lockboxes.
    public sealed partial class LevelEditorWindow
    {
        private int _brushColor;
        private bool _brushHidden;
        private int _brushKey;
        private int _brushFuse;
        private bool _brushLit;
        private bool _erase;

        private void DrawBrush()
        {
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _erase = GUILayout.Toggle(_erase, "Erase", EditorStyles.miniButton, GUILayout.Width(48));
                GUILayout.FlexibleSpace();
            }
            var perLine = 8;
            for (var start = 0; start < _palette.Count; start += perLine)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var i = start; i < Mathf.Min(start + perLine, _palette.Count); i++)
                    {
                        var old = GUI.backgroundColor;
                        GUI.backgroundColor = _palette.Get(i);
                        if (GUILayout.Toggle(!_erase && _brushColor == i, i.ToString(), EditorStyles.miniButton, GUILayout.Width(30)))
                        {
                            _brushColor = i;
                            _erase = false;
                        }
                        GUI.backgroundColor = old;
                    }
                }
            }
            using (new EditorGUI.DisabledScope(_erase))
            {
                _brushHidden = EditorGUILayout.ToggleLeft("Hidden ball (color shows at the mouth)", _brushHidden);
                var ids = BoxIds();
                var options = new[] { "None" }.Concat(ids.Select(id => "Key " + id)).ToArray();
                _brushKey = Mathf.Clamp(EditorGUILayout.Popup("Key for box", _brushKey, options), 0, options.Length - 1);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _brushFuse = Mathf.Max(0, EditorGUILayout.IntField("Bomb fuse (0 = none)", _brushFuse));
                    using (new EditorGUI.DisabledScope(_brushFuse == 0))
                        _brushLit = EditorGUILayout.ToggleLeft("Lit", _brushLit, GUILayout.Width(40));
                }
            }
            EditorGUILayout.Space();
        }

        private SlotData Brush()
        {
            if (_erase) return SlotData.Empty;
            var ids = BoxIds();
            return new SlotData
            {
                Color = _brushColor,
                Kind = _brushHidden ? SlotKind.Special : SlotKind.Normal,
                Key = _brushKey > 0 && _brushKey <= ids.Count ? ids[_brushKey - 1] : null,
                Fuse = _brushFuse,
                FuseLit = _brushFuse > 0 && _brushLit
            };
        }

        private void Paint(LevelPreview.SlotHit hit)
        {
            var slot = Brush();
            Edit("Paint Slot", () => _level.Bottles[hit.Bottle].Slots[hit.Slot] = slot);
        }

        private void DrawSelectedBottle()
        {
            EditorGUILayout.LabelField(HasSelection ? $"Tube {_selectedBottle + 1}" : "Tube (right click one)", EditorStyles.boldLabel);
            if (!HasSelection)
            {
                EditorGUILayout.Space();
                return;
            }
            var bottle = _level.Bottles[_selectedBottle];

            var sizes = LevelRules.TubeCapacities;
            var labels = sizes.Select(c => c >= LevelMechanics.TowerCapacity ? $"{c} (tower)" : c.ToString()).ToArray();
            var current = System.Array.IndexOf(sizes, bottle.Capacity);
            var chosen = EditorGUILayout.Popup("Size", current, labels);
            if (chosen != current && chosen >= 0) Edit("Tube Size", () => LevelData.Resize(bottle, sizes[chosen]));

            var curtain = _level.Curtains.Find(c => c.Bottle == _selectedBottle);
            var frozen = EditorGUILayout.ToggleLeft("Frozen (melts when a tube of one color fills)", curtain != null);
            if (frozen && curtain == null)
                Edit("Freeze Tube", () => _level.Curtains.Add(new CurtainData { Bottle = _selectedBottle, Color = _brushColor, Need = LevelRules.TubeCapacities[0] }));
            else if (!frozen && curtain != null)
                Edit("Thaw Tube", () => _level.Curtains.Remove(curtain));
            if (curtain != null)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    var color = EditorGUILayout.IntSlider("Color", curtain.Color, 0, Mathf.Max(0, _palette.Count - 1));
                    if (color != curtain.Color) Edit("Ice Color", () => curtain.Color = color);
                    var need = EditorGUILayout.IntField("Balls needed", curtain.Need);
                    if (need != curtain.Need) Edit("Ice Need", () => curtain.Need = Mathf.Max(1, need));
                }
            }

            var ids = BoxIds();
            var owner = _level.Lockboxes.FindIndex(box => box.Bottles.Contains(_selectedBottle));
            var boxOptions = new[] { "None" }.Concat(ids.Select(id => "Box " + id)).ToArray();
            var box = EditorGUILayout.Popup("Locked by", owner + 1, boxOptions) - 1;
            if (box != owner)
            {
                Edit("Lock Tube", () =>
                {
                    foreach (var lockbox in _level.Lockboxes) lockbox.Bottles.Remove(_selectedBottle);
                    if (box >= 0) _level.Lockboxes[box].Bottles.Add(_selectedBottle);
                });
            }
            EditorGUILayout.Space();
        }

        private void DrawLockboxes()
        {
            EditorGUILayout.LabelField("Lockboxes", EditorStyles.boldLabel);
            for (var i = 0; i < _level.Lockboxes.Count; i++)
            {
                var box = _level.Lockboxes[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    var tubes = box.Bottles.Count == 0 ? "no tube" : string.Join(", ", box.Bottles.Select(b => (b + 1).ToString()));
                    GUILayout.Label($"Box {box.Id}: {tubes}");
                    var index = i;
                    if (GUILayout.Button("Remove", GUILayout.Width(60))) Edit("Remove Lockbox", () => RemoveBox(index));
                }
            }
            if (GUILayout.Button("Add Lockbox")) Edit("Add Lockbox", () => _level.Lockboxes.Add(new LockboxData { Id = NextBoxId() }));
            EditorGUILayout.Space();
        }

        // Its keys go back to plain balls.
        private void RemoveBox(int index)
        {
            var id = _level.Lockboxes[index].Id;
            _level.Lockboxes.RemoveAt(index);
            foreach (var bottle in _level.Bottles)
                for (var s = 0; s < bottle.Slots.Count; s++)
                {
                    if (bottle.Slots[s].Key != id) continue;
                    var slot = bottle.Slots[s];
                    slot.Key = null;
                    bottle.Slots[s] = slot;
                }
        }

        private List<string> BoxIds() => _level.Lockboxes.Select(box => box.Id).ToList();

        private string NextBoxId()
        {
            for (var c = 'A'; c <= 'Z'; c++)
                if (_level.Lockboxes.All(box => box.Id != c.ToString())) return c.ToString();
            return "Box" + _level.Lockboxes.Count;
        }
    }
}
