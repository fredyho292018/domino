using System;
using System.Linq;

namespace Domino.AppShellMock
{
    // Fictional session-only presentation data. Not a backend contract.
    public enum MockProfileMode { OWN_PROFILE, OTHER_NOT_FRIEND, OTHER_REQUEST_SENT, OTHER_FRIEND }
    public enum MockFriendState { NOT_FRIEND, REQUEST_SENT, FRIEND }
    public sealed class MockPlayerProfile
    {
        public string playerId,displayName,avatar,countryCode,countryName;
        public DateTime joinedAt;
        public MockFriendState friendState;
        public int gameHistoryTotal=1111;
    }
    public sealed class MockProfileGame
    {
        public readonly string[] leftPlayers,rightPlayers;
        public readonly int leftScore,rightScore,currentPlayerSide;
        public readonly bool won;
        public readonly string avatar;
        public string Mode=>leftPlayers.Length==2?"2v2":"1v1";
        public string Matchup=>string.Join(" - ",leftPlayers)+" vs "+string.Join(" - ",rightPlayers);
        public string Score=>leftScore+" \u2013 "+rightScore;
        public string Result=>won?"You Won":"You Lost";
        public MockProfileGame(string[] left,string[] right,int ls,int rs,int side,bool win,string image)
        {leftPlayers=left;rightPlayers=right;leftScore=ls;rightScore=rs;currentPlayerSide=side;won=win;avatar=image;}
    }
    public sealed partial class MockShellState
    {
        public MockProfileMode ProfileMode=MockProfileMode.OWN_PROFILE;
        public MockPlayerProfile Profile=CreateProfile(MockProfileMode.OWN_PROFILE);
        public bool OwnProfile=>ProfileMode==MockProfileMode.OWN_PROFILE;
        public string ProfileAction=>OwnProfile?"Edit Profile":Profile.friendState==MockFriendState.NOT_FRIEND?"Add Friend":Profile.friendState==MockFriendState.REQUEST_SENT?"Request Sent":"Friends";
        static MockPlayerProfile CreateProfile(MockProfileMode mode)=>new MockPlayerProfile {
            playerId=mode==MockProfileMode.OWN_PROFILE?"mock-alex":"mock-pedro",
            displayName=mode==MockProfileMode.OWN_PROFILE?"Alex \u00B7 Demo player":"Pedro \u00B7 Demo player",
            avatar=mode==MockProfileMode.OWN_PROFILE?"coach_mateo":"coach_david",
            countryCode="CU",countryName="Cuba",joinedAt=new DateTime(2022,1,21),
            friendState=mode==MockProfileMode.OTHER_FRIEND?MockFriendState.FRIEND:mode==MockProfileMode.OTHER_REQUEST_SENT?MockFriendState.REQUEST_SENT:MockFriendState.NOT_FRIEND };
        public void SetProfileMode(MockProfileMode mode){ProfileMode=mode;Profile=CreateProfile(mode);}
        public void OpenProfile(MockProfileMode mode){SetProfileMode(mode);Go(MockPage.Profile);}
        public void ProfilePrimaryAction()
        {
            if(OwnProfile){Notice="Demo: profile editing coming soon.";return;}
            if(Profile.friendState==MockFriendState.NOT_FRIEND)Profile.friendState=MockFriendState.REQUEST_SENT;
        }
        public void ShareProfile(){Notice="Demo: share preview only. Nothing was shared.";}
        public MockProfileGame[] ProfileGames
        {
            get
            {
                string me=OwnProfile?"Alex":"Pedro";
                return new[]{
                    new MockProfileGame(new[]{"Carlos Alejandro","Juan Manuel"},new[]{me,"Ana"},150,220,1,true,"coach_leo"),
                    new MockProfileGame(new[]{me},new[]{"Isabel"},90,200,0,false,"coach_elena"),
                    new MockProfileGame(new[]{me,"Sof\u00EDa"},new[]{"Luis","David"},205,180,0,true,"coach_david"),
                    new MockProfileGame(new[]{"Ana"},new[]{me},200,145,1,false,"coach_amara"),
                    new MockProfileGame(new[]{"Juan"},new[]{me},130,200,1,true,"coach_omar"),
                    new MockProfileGame(new[]{me,"Luis"},new[]{"Carlos","Isabel"},170,210,0,false,"coach_leo"),
                    new MockProfileGame(new[]{me},new[]{"David"},200,180,0,true,"coach_david"),
                    new MockProfileGame(new[]{"Ana","Sof\u00EDa"},new[]{me,"Juan"},215,160,1,false,"coach_amara")};
            }
        }
        public MockProfileGame[] ProfilePreviewGames=>ProfileGames.Take(5).ToArray();
    }
}
