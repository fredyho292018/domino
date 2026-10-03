using System;
using System.Collections.Generic;
using Domino.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    public interface IObservableProfileDataSource : IProfileDataSource
    {
        event Action Changed;
        string HistoryUnavailableText { get; }
    }
    // Stateless consumer of the same host source as Menu/Home. No Player fetch, cache or history API.
    public sealed class PlayerProfileDataSource : IObservableProfileDataSource
    {
        readonly PlayerPresentationSource source;
        public PlayerProfileDataSource(PlayerPresentationSource source){this.source=source;}
        public event Action Changed {add{if(source!=null)source.Changed+=value;}remove{if(source!=null)source.Changed-=value;}}
        string Locale=>DominoLocalization.Ready?DominoLocalization.Language:source?.Current.PreferredLocale;
        public string HistoryUnavailableText=>ProfilePlayerText.History(Locale);
        public PlayerProfileViewModel ReadProfile()
        {
            var state=source?.Current;var code=ProfilePlayerText.CountryCode(state);
            string Localize(string key)=>DominoLocalization.Ready?DominoLocalization.Get(key):null;
            // Only an exact saved CU code has a bundled flag today; other countries never borrow it.
            var flag=code=="CU"?Resources.Load<VectorImage>("AppShellMockIcons/icon_flag_cu"):null;
            return new PlayerProfileViewModel(null,MenuPlayerText.Name(state,Locale,Localize),null,code,
                ProfilePlayerText.Country(state,Locale),flag,state?.CreatedAt?.UtcDateTime,true,ProfileFriendState.NotFriend,
                ProfilePlayerText.Joined(state,Locale),Resources.Load<VectorImage>("AppShellMockIcons/icon_menu_avatar"));
        }
        public IReadOnlyList<ProfileGameSummary> ReadGames()=>Array.Empty<ProfileGameSummary>();
    }
}
