using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Domino.UI
{
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedUiText : MonoBehaviour
    {
        Func<string> value;
        public void Set(Func<string> formatter)
        {
            value = formatter;
            var text = GetComponent<Text>();
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(text.fontSize, 12);
            text.resizeTextMaxSize = text.fontSize;
            Refresh(null);
        }
        void OnEnable() { LocalizationSettings.SelectedLocaleChanged += Refresh; Refresh(null); }
        void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= Refresh;
        void Refresh(Locale _) { if (value != null) GetComponent<Text>().text = value(); }
    }
}
