using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.AppShellMock
{
    public sealed partial class MockShellView
    {
        VisualElement ProfileAvatar(string asset,int size)
        {
            var frame=new VisualElement {name="PlayerAvatar",tooltip="Player avatar",pickingMode=PickingMode.Ignore};
            frame.style.width=size;frame.style.height=size;frame.style.flexShrink=0;frame.style.overflow=Overflow.Hidden;Round(frame,size/2);
            var image=new Image {pickingMode=PickingMode.Ignore,image=Resources.Load<Texture2D>("AppShellMockCoaches/"+asset),scaleMode=ScaleMode.ScaleAndCrop};
            image.style.width=Length.Percent(100);image.style.height=Length.Percent(100);frame.Add(image);return frame;
        }
        void ProfileScreen()
        {
            headerTitle.text="Profile";
            var player=State.Profile;
            var card=Card("");card.name="ProfilePlayerCard";card.style.flexDirection=FlexDirection.Row;card.style.alignItems=Align.Center;Pad(card,12);
            var avatar=ProfileAvatar(player.avatar,76);avatar.style.marginRight=12;card.Add(avatar);
            var details=new VisualElement();details.style.flexGrow=1;details.style.flexShrink=1;details.style.minWidth=0;card.Add(details);
            var name=Text(player.displayName,18,details);name.name="ProfilePlayerName";name.style.color=MockShellTheme.Text;PremiumFont(name,"Semibold");
            var country=new VisualElement();country.style.flexDirection=FlexDirection.Row;country.style.alignItems=Align.Center;details.Add(country);
            var flag=PremiumIcon("icon_flag_cu",24,Color.white);flag.name="ProfileFlag";flag.tooltip="Cuba flag";flag.style.height=16;flag.style.marginRight=7;country.Add(flag);
            var countryName=Text(player.countryName,14,country);countryName.name="ProfileCountry";countryName.style.marginBottom=0;
            var date=Text("Joined "+player.joinedAt.ToString("MMMM d, yyyy",CultureInfo.GetCultureInfo("en-US")),12,details);date.name="ProfileJoined";date.style.marginTop=8;date.style.color=MockShellTheme.Inactive;
            var actions=new VisualElement();actions.style.flexDirection=FlexDirection.Row;actions.style.marginBottom=12;body.Add(actions);
            var action=Button(State.ProfileAction,()=>Act(State.ProfilePrimaryAction),true);action.name="ProfilePrimaryAction";action.tooltip=State.ProfileAction;action.text="";action.style.flexGrow=1;action.style.flexBasis=0;action.style.flexDirection=FlexDirection.Row;action.style.alignItems=Align.Center;action.style.justifyContent=Justify.Center;
            if(!State.OwnProfile){var icon=PremiumIcon(State.Profile.friendState==MockFriendState.NOT_FRIEND?"icon_user_add":"icon_premium_check",22,MockShellTheme.Text);icon.style.marginRight=8;action.Add(icon);}
            var actionLabel=new Label(State.ProfileAction){pickingMode=PickingMode.Ignore};action.Add(actionLabel);actions.Add(action);
            var share=Button("Share",()=>Act(State.ShareProfile));share.name="ShareProfile";share.tooltip="Share Profile";share.text="";share.style.width=52;share.style.marginLeft=8;share.style.alignItems=Align.Center;share.style.justifyContent=Justify.Center;share.Add(PremiumIcon("icon_share",24,MockShellTheme.Text));actions.Add(share);
            var heading=new VisualElement();heading.style.flexDirection=FlexDirection.Row;heading.style.justifyContent=Justify.SpaceBetween;heading.style.alignItems=Align.Center;body.Add(heading);
            var title=Text("Game History",18,heading);title.style.color=MockShellTheme.Text;PremiumFont(title,"Bold");var last=Text("Last 5 games",12,heading);last.style.color=MockShellTheme.Inactive;
            Text(player.gameHistoryTotal+" games",12).style.color=MockShellTheme.Inactive;
            foreach(var game in State.ProfilePreviewGames)ProfileGameRow(game);
            Action("View All Games",()=>State.Go(MockPage.ProfileHistory));
        }
        void ProfileGameRow(MockProfileGame game)
        {
            var row=Button(game.Matchup,()=>Act(()=>State.Placeholder("Game Details Coming Soon")));row.text="";row.name="ProfileGameRow";row.tooltip=game.Matchup+", "+game.Score+", "+game.Result;
            row.style.flexDirection=FlexDirection.Row;row.style.alignItems=Align.Center;row.style.minHeight=84;Pad(row,10);body.Add(row);
            var avatar=ProfileAvatar(game.avatar,32);avatar.style.marginRight=8;row.Add(avatar);
            var main=new VisualElement {pickingMode=PickingMode.Ignore};main.style.flexGrow=1;main.style.flexShrink=1;main.style.minWidth=0;row.Add(main);
            var description=Text(game.Mode+" \u00B7 "+game.Matchup,13,main);description.name="GameMatchup";description.pickingMode=PickingMode.Ignore;description.style.color=MockShellTheme.Text;description.style.marginBottom=4;description.style.unityTextAlign=TextAnchor.MiddleLeft;
            var result=Text(game.Result,13,main);result.name="GameResult";result.pickingMode=PickingMode.Ignore;result.style.marginBottom=0;result.style.color=Domino.UI.UiKit.Hex(game.won?"7BCB63":"EF6A67");result.style.unityTextAlign=TextAnchor.MiddleLeft;
            var score=new Label(game.Score){name="GameScore",pickingMode=PickingMode.Ignore};score.style.width=76;score.style.flexShrink=0;score.style.marginLeft=8;score.style.fontSize=14;score.style.color=MockShellTheme.Text;score.style.unityTextAlign=TextAnchor.MiddleRight;PremiumFont(score,"Bold");row.Add(score);
        }
        void ProfileHistoryScreen()
        {
            headerTitle.text="Game History";Text(State.Profile.gameHistoryTotal+" games",13);
            foreach(var game in State.ProfileGames)ProfileGameRow(game);
        }
    }
}
