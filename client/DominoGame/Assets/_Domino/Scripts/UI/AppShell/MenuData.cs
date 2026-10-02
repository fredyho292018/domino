using UnityEngine.UIElements;
using UnityEngine;

namespace Domino.UI.AppShell
{
    public enum MenuDestination { Profile, Friends, Messages, Stats, Coach, Theme, Membership, Settings, Support }
    public sealed class MenuProfileSummary
    {
        public string PlayerId { get; }
        public string DisplayName { get; }
        public VectorImage Avatar { get; }
        public string MembershipLabel { get; }
        public string ClubLabel { get; }
        public MenuProfileSummary(string playerId,string displayName,VectorImage avatar,string membershipLabel,string clubLabel)
        { PlayerId=playerId;DisplayName=displayName;Avatar=avatar;MembershipLabel=membershipLabel;ClubLabel=clubLabel; }
    }
    public interface IMenuDataSource { MenuProfileSummary Read(); }
    public interface IObservableMenuDataSource : IMenuDataSource { event System.Action Changed; }
    public sealed class DemoMenuDataSource : IMenuDataSource
    {
        public MenuProfileSummary Read()=>new MenuProfileSummary("demo-alex","Alex · Demo player",
            Resources.Load<VectorImage>("AppShellMockIcons/icon_menu_avatar"),"FREE","Cuban Domino Club");
    }
}
