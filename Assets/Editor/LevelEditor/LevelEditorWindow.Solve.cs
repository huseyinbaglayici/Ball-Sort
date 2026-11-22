using System.Collections.Generic;
using System.Linq;
using System.Text;
using Project.Contexts.Game.Levels;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.LevelEditor
{
    // The booster-free solvability check of the open layout or of every layout of its level.
    public sealed partial class LevelEditorWindow
    {
        private readonly Dictionary<LevelData, (int Revision, LevelSolver.Report Report)> _reports = new();
        private int _timeoutSeconds = LevelSolver.DefaultTimeoutMs / 1000;
        private bool _showRoute;

        private void DrawSolvability()
        {
            EditorGUILayout.LabelField("Solvability", EditorStyles.boldLabel);
            _timeoutSeconds = Mathf.Clamp(EditorGUILayout.IntField("Time limit (s)", _timeoutSeconds), 1, 300);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Check This Layout")) Check(_level);
                using (new EditorGUI.DisabledScope(_group == null || _group.Variants.Count < 2))
                    if (GUILayout.Button("Check All Layouts"))
                        foreach (var variant in _group.Variants) Check(variant.Level);
            }

            if (_group != null)
                foreach (var variant in _group.Variants)
                    DrawReport(variant.Level, _group.Variants.Count > 1 ? variant.Label : _level.name);

            if (_reports.TryGetValue(_level, out var current) && current.Report.Outcome == LevelSolver.Outcome.Solved)
            {
                _showRoute = EditorGUILayout.Foldout(_showRoute, "Solution (tube numbers)");
                if (_showRoute) EditorGUILayout.HelpBox(Route(current.Report), MessageType.None);
            }
        }

        private void Check(LevelData level)
        {
            EditorUtility.DisplayProgressBar("Solvability", $"Solving {level.name}", 0.5f);
            try
            {
                var report = LevelSolver.Check(level, LevelSolver.DefaultNodeLimit, _timeoutSeconds * 1000);
                // Other layouts of the level are not edited here, so only the open one tracks edits.
                _reports[level] = (level == _level ? _revision : -1, report);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void DrawReport(LevelData level, string label)
        {
            if (!_reports.TryGetValue(level, out var entry))
            {
                EditorGUILayout.LabelField(label, "not checked", EditorStyles.miniLabel);
                return;
            }
            var stale = level == _level && entry.Revision != _revision;
            var type = stale ? MessageType.Info : entry.Report.Outcome switch
            {
                LevelSolver.Outcome.Solved => MessageType.Info,
                LevelSolver.Outcome.NotProven => MessageType.Warning,
                _ => MessageType.Error
            };
            var text = $"{label}: {entry.Report.Summary}";
            if (stale) text += "\nEdited since; check again.";
            EditorGUILayout.HelpBox(text, type);
        }

        private static string Route(LevelSolver.Report report)
        {
            var text = new StringBuilder();
            for (var i = 0; i < report.Route.Count; i++)
            {
                var (from, to) = report.Route[i];
                text.Append($"{from + 1}→{to + 1}");
                text.Append(i % 8 == 7 ? "\n" : "   ");
            }
            return text.ToString().TrimEnd();
        }
    }
}
