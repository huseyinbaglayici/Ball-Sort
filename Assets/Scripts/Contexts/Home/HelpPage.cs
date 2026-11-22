using Project.Contexts.Application;
using Project.Contexts.Game.Levels;
using Project.Core.Services;
using UnityEngine;

namespace Project.Contexts.Home
{
    // How-to-play page: the basics always, one note per mechanic the player has met.
    public sealed class HelpPage : MonoBehaviour
    {
        [System.Serializable]
        private struct Note
        {
            public Mechanic Mechanic;
            public GameObject Item;
        }

        [SerializeField] private LevelCatalog _catalog;
        [SerializeField] private Note[] _notes = System.Array.Empty<Note>();

        private void OnEnable()
        {
            if (!Services.TryGet<ProgressService>(out var progress)) return;
            var seen = LevelMechanics.Seen(_catalog, PlayedCount(progress.Level, progress.Attempt));
            foreach (var note in _notes)
                if (note.Item != null) note.Item.SetActive((seen & note.Mechanic) != 0);
        }

        // Won levels plus the current one once it has been tried.
        public static int PlayedCount(int level, int attempt) => level - 1 + (attempt > 0 ? 1 : 0);
    }
}
