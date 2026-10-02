using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    // Stateless adapter: no Player access, API requests, independent cache or ownership.
    public sealed class PlayerMenuDataSource : IObservableMenuDataSource
    {
        readonly PlayerPresentationSource source;
        public PlayerMenuDataSource(PlayerPresentationSource source) { this.source = source; }
        public event Action Changed {
            add { if (source != null) source.Changed += value; }
            remove { if (source != null) source.Changed -= value; }
        }
        public MenuProfileSummary Read()
        {
            var state = source?.Current;
            bool localized = DominoLocalization.Ready;
            var locale = localized ? DominoLocalization.Language : state?.PreferredLocale;
            string Localize(string key) => localized ? DominoLocalization.Get(key) : null;
            return new MenuProfileSummary(string.Empty, MenuPlayerText.Name(state, locale, Localize),
                Resources.Load<VectorImage>("AppShellMockIcons/icon_menu_avatar"),
                MenuPlayerText.Membership(state, locale, Localize), MenuPlayerText.ProductBrand);
        }
    }
}
