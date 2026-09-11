using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace Domino.UI
{
    // Thin presentation adapter over official Unity StringTables and Smart Strings.
    public static class DominoLocalization
    {
        public const string TableName = "Domino UI";
        public const string PreferenceKey = "domino.language";
        static StringTable english, spanish;
        public static bool Ready => english && spanish;
        public static string Language => LocalizationSettings.SelectedLocale.Identifier.Code;
        public static IEnumerator Initialize()
        {
            yield return LocalizationSettings.InitializationOperation;
            var en = LocalizationSettings.StringDatabase.GetTableAsync(TableName, LocalizationSettings.AvailableLocales.GetLocale("en"));
            yield return en;
            var es = LocalizationSettings.StringDatabase.GetTableAsync(TableName, LocalizationSettings.AvailableLocales.GetLocale("es"));
            yield return es;
            english = en.Result; spanish = es.Result;
            if (!Ready) throw new InvalidOperationException("Localization tables failed to load.");
        }
        public static void Select(string code)
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (!locale || (code != "en" && code != "es")) return;
            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.SetString(PreferenceKey, code); PlayerPrefs.Save();
        }
        public static string Get(string key, params object[] arguments)
        {
            if (!Ready) return string.Empty;
            var entry = (Language == "es" ? spanish : english).GetEntry(key);
            if (entry == null || string.IsNullOrEmpty(entry.Value)) entry = english.GetEntry(key);
            if (entry == null) { Debug.LogWarning("Missing localization entry: " + key); entry = english.GetEntry("system.unavailable"); }
            return entry.GetLocalizedString(arguments);
        }
        public static void Set(Text text, string key, params object[] arguments) => Bind(text, () => Get(key, arguments));
        public static void Bind(Text text, Func<string> value)
        {
            var binding = text.GetComponent<LocalizedUiText>();
            if (!binding) binding = text.gameObject.AddComponent<LocalizedUiText>();
            binding.Set(value);
        }
    }
}
