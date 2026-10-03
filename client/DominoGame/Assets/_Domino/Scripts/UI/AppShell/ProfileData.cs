using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    public enum ProfileFriendState { NotFriend, RequestSent, Friend }
    public enum ProfileSection { Profile, EditProfile, GameDetails, GameHistory }
    // Presentation models only; not backend DTOs or authenticated identities.
    public sealed class PlayerProfileViewModel
    {
        public string PlayerId { get; }
        public string DisplayName { get; }
        public Texture2D Avatar { get; }
        public string CountryCode { get; }
        public string CountryName { get; }
        public VectorImage Flag { get; }
        public DateTime? JoinedAt { get; }
        public string JoinedLabel { get; }
        public VectorImage AvatarIcon { get; }
        public bool IsOwnProfile { get; }
        public ProfileFriendState FriendState { get; }
        public PlayerProfileViewModel(string id,string name,Texture2D avatar,string countryCode,string countryName,VectorImage flag,DateTime? joined,bool own,ProfileFriendState state,
            string joinedLabel=null,VectorImage avatarIcon=null)
        {PlayerId=id;DisplayName=name;Avatar=avatar;CountryCode=countryCode;CountryName=countryName;Flag=flag;JoinedAt=joined;IsOwnProfile=own;FriendState=state;JoinedLabel=joinedLabel;AvatarIcon=avatarIcon;}
    }
    public sealed class ProfileGameSummary
    {
        public string GameId { get; }
        public string Mode { get; }
        public string Matchup { get; }
        public Texture2D OpponentAvatar { get; }
        public int LeftScore { get; }
        public int RightScore { get; }
        public bool Won { get; }
        public string Result=>Won?"You Won":"You Lost";
        public string Score=>LeftScore+" \u2013 "+RightScore;
        public ProfileGameSummary(string id,string mode,string matchup,Texture2D avatar,int left,int right,bool won)
        {GameId=id;Mode=mode;Matchup=matchup;OpponentAvatar=avatar;LeftScore=left;RightScore=right;Won=won;}
    }
    public interface IProfileDataSource
    {
        PlayerProfileViewModel ReadProfile();
        IReadOnlyList<ProfileGameSummary> ReadGames();
    }
    public sealed class DemoProfileDataSource : IProfileDataSource
    {
        readonly bool own;readonly ProfileFriendState state;readonly string displayName;
        public DemoProfileDataSource(bool own=true,ProfileFriendState state=ProfileFriendState.NotFriend,string displayName=null)
        {this.own=own;this.state=state;this.displayName=displayName;}
        static Texture2D Avatar(string name)=>Resources.Load<Texture2D>("AppShellMockCoaches/coach_"+name);
        public PlayerProfileViewModel ReadProfile()=>new PlayerProfileViewModel(own?"demo-alex":"demo-pedro",displayName??(own?"Alex · Demo player":"Pedro · Demo player"),Avatar(own?"mateo":"david"),"CU","Cuba",Resources.Load<VectorImage>("AppShellMockIcons/icon_flag_cu"),new DateTime(2022,1,21),own,state);
        public IReadOnlyList<ProfileGameSummary> ReadGames()=>Array.AsReadOnly(new[]{
            new ProfileGameSummary("demo-game-1","2v2","Carlos Alejandro - Juan Manuel vs Alex - Ana",Avatar("leo"),150,220,true),
            new ProfileGameSummary("demo-game-2","1v1","Alex vs Isabel",Avatar("elena"),90,200,false),
            new ProfileGameSummary("demo-game-3","2v2","Alex - Sofía vs Luis - David",Avatar("david"),205,180,true),
            new ProfileGameSummary("demo-game-4","1v1","Ana vs Alex",Avatar("amara"),200,145,false),
            new ProfileGameSummary("demo-game-5","1v1","Juan vs Alex",Avatar("omar"),130,200,true),
            new ProfileGameSummary("demo-game-6","2v2","Alex - Luis vs Carlos - Isabel",Avatar("leo"),170,210,false),
            new ProfileGameSummary("demo-game-7","1v1","Alex vs David",Avatar("david"),200,180,true),
            new ProfileGameSummary("demo-game-8","2v2","Ana - Sofía vs Alex - Juan",Avatar("amara"),215,160,false)});
    }
}
