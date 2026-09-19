using System;
using System.IO;
using Domino.UI;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Domino.Editor
{
    public static class LocalizationAssets
    {
        [Serializable] public class Entry { public string key, en, es; }
        [Serializable] public class Source { public Entry[] entries; }
        const string Root = "Assets/_Domino/Localization";
        // Update existing tables without touching user-local locale startup settings.
        public static void ImportTablesOnly()
        {
            var collection=LocalizationEditorSettings.GetStringTableCollection(DominoLocalization.TableName);
            if(collection==null)throw new Exception("Existing localization tables required");
            var source=JsonUtility.FromJson<Source>(File.ReadAllText("Assets/_Domino/Editor/Localization/Translations.json"));
            foreach(string code in new[]{"en","es"}) {
                var table=(StringTable)collection.GetTable(new LocaleIdentifier(code));
                foreach(var row in source.entries){var entry=table.AddEntry(row.key,code=="en"?row.en:row.es);entry.IsSmart=true;}
                EditorUtility.SetDirty(table);AssetDatabase.SaveAssetIfDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData);AssetDatabase.SaveAssetIfDirty(collection.SharedData);
        }
        [MenuItem("Domino/Localization/Import translations")]
        public static void Import()
        {
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(Root + "/Localization Settings.asset");
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, Root + "/Localization Settings.asset");
            }
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            var en = MakeLocale("en"); var es = MakeLocale("es");
            if (es.Metadata.GetMetadata<FallbackLocale>() == null) es.Metadata.AddMetadata(new FallbackLocale(en));
            LocalizationSettings.ProjectLocale = en;
            LocalizationSettings.StartupLocaleSelectors.Clear();
            LocalizationSettings.StartupLocaleSelectors.Add(new PlayerPrefLocaleSelector { PlayerPreferenceKey = DominoLocalization.PreferenceKey });
            LocalizationSettings.StartupLocaleSelectors.Add(new SystemLocaleSelector());
            LocalizationSettings.StartupLocaleSelectors.Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier("en") });
            var collection = LocalizationEditorSettings.GetStringTableCollection(DominoLocalization.TableName)
                ?? LocalizationEditorSettings.CreateStringTableCollection(DominoLocalization.TableName, Root + "/Tables");
            var source = JsonUtility.FromJson<Source>(File.ReadAllText("Assets/_Domino/Editor/Localization/Translations.json"));
            foreach (var locale in new[] { en, es })
            {
                var table = (StringTable)collection.GetTable(locale.Identifier);
                foreach (var row in source.entries)
                {
                    if (string.IsNullOrWhiteSpace(row.en) || string.IsNullOrWhiteSpace(row.es)) throw new Exception("Missing translation " + row.key);
                    var entry = table.AddEntry(row.key, locale == en ? row.en : row.es);
                    entry.IsSmart = true;
                }
                LocalizationEditorSettings.SetPreloadTableFlag(table, true);
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData); EditorUtility.SetDirty(collection);
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(es);
            AssetDatabase.SaveAssets();
            Debug.Log("LOCALIZATION_ASSETS=SUCCESS KEYS=" + source.entries.Length);
        }
        static Locale MakeLocale(string code)
        {
            string path = Root + "/" + code + ".asset";
            var locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (!locale) { locale = Locale.CreateLocale(code); AssetDatabase.CreateAsset(locale, path); }
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }
    }
}
