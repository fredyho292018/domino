using System;
using Domino.Player;
using Domino.Infrastructure.Api;
using UnityEngine;

namespace Domino.UI.AppShell
{
    // Stateless UI adapter over the SAME host-owned presentation used by Menu.
    public sealed class PlayerHomeDataSource : IObservableHomeDataSource
    {
        readonly PlayerPresentationSource source;
        public PlayerHomeDataSource(PlayerPresentationSource source) { this.source=source; }
        public event Action Changed {
            add { if(source!=null)source.Changed+=value; }
            remove { if(source!=null)source.Changed-=value; }
        }
        public void SetLocale(string locale)=>source?.SetLocale(locale);
        public HomeSummary Read()
        {
            var state=source?.Current;
            var locale=DominoLocalization.Ready?DominoLocalization.Language:state?.PreferredLocale;
            string Localize(string key)=>DominoLocalization.Ready?DominoLocalization.Get(key):null;
            var content=state?.Availability==PlayerPresentationAvailability.Ready?HomeContentState.Content:
                state?.Availability==PlayerPresentationAvailability.Loading?HomeContentState.Loading:
                state?.Availability==PlayerPresentationAvailability.Unavailable?HomeContentState.Error:HomeContentState.Empty;
            HomeCoachSummary coach=null;
            if(state?.Coach?.Availability==PlayerCoachAvailability.Ready) {
                var c=state.Coach;var path=CoachAvatarResources.VersionedPath(c.AvatarKey,c.AvatarVersion,c.AvatarPath);
                coach=new HomeCoachSummary(c.Key,c.Name,c.Description,path==null?null:Resources.Load<Texture2D>(path));
            }
            return new HomeSummary(content,state?.DisplayName,coach,new HomeFriendsSummary(5,"fictional players"),
                HomePlayerText.Greeting(state,locale,Localize),coach==null?HomePlayerText.CoachStatus(state?.Coach,locale):null);
        }
    }
}
