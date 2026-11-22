using System.Collections.Generic;
using Project.Core.Events;
using UnityEngine;

namespace Project.Core.Localization
{
    public readonly struct LanguageChanged : IEvent
    {
        public readonly string LanguageCode;
        public LanguageChanged(string languageCode) => LanguageCode = languageCode;
    }

    // Falls back to the first table, then to the key, so a missing translation is never blank.
    public sealed class LocalizationService
    {
        private readonly Dictionary<string, Dictionary<string, string>> _tables = new();
        private readonly List<string> _languages = new();
        private readonly Dictionary<string, string> _names = new();
        private readonly string _fallbackCode;
        private Dictionary<string, string> _current;
        private Dictionary<string, string> _fallback;

        public string LanguageCode { get; private set; }

        // Starts in the device language, or the first table's when there is none for it.
        public LocalizationService(IReadOnlyList<LocalizationTable> tables)
        {
            foreach (var table in tables)
            {
                if (table == null || _tables.ContainsKey(table.LanguageCode)) continue;
                _tables[table.LanguageCode] = table.BuildLookup();
                _languages.Add(table.LanguageCode);
                _names[table.LanguageCode] = table.NativeName;
            }

            _fallbackCode = tables.Count > 0 && tables[0] != null ? tables[0].LanguageCode : "en";
            _tables.TryGetValue(_fallbackCode, out _fallback);
            SetLanguage(SystemLanguageCode(), raise: false);
        }

        // Codes of the tables in the build, in table order.
        public IReadOnlyList<string> Languages => _languages;

        public string NativeName(string code) => _names.TryGetValue(code ?? string.Empty, out var name) ? name : code;

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (_current != null && _current.TryGetValue(key, out var text)) return text;
            if (_fallback != null && _fallback.TryGetValue(key, out text)) return text;
            return key;
        }

        public string Format(string key, params object[] args) => string.Format(Get(key), args);

        public void SetLanguage(string code) => SetLanguage(code, raise: true);

        private void SetLanguage(string code, bool raise)
        {
            if (!_tables.TryGetValue(code ?? string.Empty, out _current))
            {
                code = _fallbackCode;
                _current = _fallback;
            }
            LanguageCode = code;
            if (raise) EventBus<LanguageChanged>.Raise(new LanguageChanged(code));
        }

        private static string SystemLanguageCode() => Application.systemLanguage switch
        {
            SystemLanguage.Turkish => "tr",
            SystemLanguage.German => "de",
            SystemLanguage.French => "fr",
            SystemLanguage.Spanish => "es",
            SystemLanguage.Portuguese => "pt",
            SystemLanguage.Russian => "ru",
            SystemLanguage.Japanese => "ja",
            SystemLanguage.Korean => "ko",
            _ => "en"
        };
    }
}
