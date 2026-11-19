using TMPro;
using UnityEngine;

namespace Project.Common.UI
{
    // More marks means harder; an empty label hides the tag.
    public sealed class DifficultyTag : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private GameObject[] _marks = System.Array.Empty<GameObject>();

        public void Set(string label, int marks)
        {
            var shown = !string.IsNullOrEmpty(label);
            (_root != null ? _root : gameObject).SetActive(shown);
            if (_label != null) _label.text = label ?? string.Empty;
            for (var i = 0; i < _marks.Length; i++)
                if (_marks[i] != null) _marks[i].SetActive(shown && i < marks);
        }
    }
}
