using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Common.UI
{
    // More marks means harder; an empty label hides the tag.
    public sealed class DifficultyTag : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private GameObject[] _marks = System.Array.Empty<GameObject>();
        // Badge art per mark count (index 0 = one mark); optional.
        [SerializeField] private Image _background;
        [SerializeField] private Sprite[] _backgrounds = System.Array.Empty<Sprite>();

        public void Set(string label, int marks)
        {
            var shown = !string.IsNullOrEmpty(label);
            (_root != null ? _root : gameObject).SetActive(shown);
            if (_label != null) _label.text = label ?? string.Empty;
            for (var i = 0; i < _marks.Length; i++)
                if (_marks[i] != null) _marks[i].SetActive(shown && i < marks);
            if (_background != null && marks > 0 && _backgrounds.Length > 0)
                _background.sprite = _backgrounds[Mathf.Min(marks, _backgrounds.Length) - 1];
        }
    }
}
