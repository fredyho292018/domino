using UnityEngine;

namespace Domino.UI.AppShell
{
    public enum HomeContentState { Content, Loading, Empty, Error }
    public sealed class HomeCoachSummary
    {
        public string CoachId { get; }
        public string DisplayName { get; }
        public string Greeting { get; }
        public Texture2D Avatar { get; }
        public HomeCoachSummary(string id, string name, string greeting, Texture2D avatar)
        { CoachId=id; DisplayName=name; Greeting=greeting; Avatar=avatar; }
    }
    public sealed class HomeFriendsSummary
    {
        public int Count { get; }
        public string Description { get; }
        public HomeFriendsSummary(int count, string description) { Count=count; Description=description; }
    }
    public sealed class HomeSummary
    {
        public HomeContentState State { get; }
        public string DisplayName { get; }
        public HomeCoachSummary Coach { get; }
        public HomeFriendsSummary Friends { get; }
        public string Greeting { get; }
        public string CoachStatus { get; }
        public HomeSummary(HomeContentState state, string name="", HomeCoachSummary coach=null, HomeFriendsSummary friends=null, string greeting=null, string coachStatus=null)
        { State=state; DisplayName=name; Coach=coach; Friends=friends; Greeting=greeting; CoachStatus=coachStatus; }
    }
    public interface IHomeDataSource { HomeSummary Read(); }
    public interface IObservableHomeDataSource : IHomeDataSource { event System.Action Changed; void SetLocale(string locale); }

    // Temporary composition input, not a service, session or real player identity.
    public sealed class DemoHomeDataSource : IHomeDataSource
    {
        public HomeSummary Read() => new HomeSummary(HomeContentState.Content,"Alex",
            new HomeCoachSummary("amara","Amara","Hola, soy Amara.\nTe enseñaré a jugar dominó.",Resources.Load<Texture2D>("AppShellMockCoaches/coach_amara")),
            new HomeFriendsSummary(5,"fictional players"));
    }
}
