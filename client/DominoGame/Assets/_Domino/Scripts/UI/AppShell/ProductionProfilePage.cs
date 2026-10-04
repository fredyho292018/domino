using System;
using System.Globalization;
using System.Linq;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Domino.UI.AppShell
{
    public sealed class ProductionProfilePage : ProductionRootPage
    {
        public const int MaxVisibleGames=5;
        public AppTheme Theme=>ThemeProvider.Current;
        public ProfileSection Section { get; }
        public PlayerProfileViewModel Profile { get; private set; }
        readonly IProfileDataSource source;
        Label playerName,countryName,joined,historyUnavailable;
        Image playerImage,countryFlag;
        public ProductionProfilePage(IProfileDataSource source,ProfileSection section,Action back,Action<ProfileSection> navigate):base("", "")
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            this.source=source;Section=section;Profile=source.ReadProfile();Body.Clear();verticalScrollerVisibility=ScrollerVisibility.Hidden;
            var header=new VisualElement();header.style.flexDirection=FlexDirection.Row;header.style.alignItems=Align.Center;header.style.marginBottom=Theme.Spacing.MD;
            var backButton=new Button(back){name="ShellBack"};var arrow=new Image();ThemeStyles.Back(backButton,arrow);backButton.Add(arrow);header.Add(backButton);
            var title=section==ProfileSection.Profile?"Profile":section==ProfileSection.EditProfile?"Edit Profile":section==ProfileSection.GameHistory?"Game History":"Game Details";
            var heading=Text(title,TextRole.SectionTitle,header,"SubpageTitle");heading.style.flexGrow=1;heading.style.unityTextAlign=TextAnchor.MiddleCenter;heading.style.marginRight=Theme.Sizing.MinTouchTarget;
            Body.Add(header);
            if(section==ProfileSection.EditProfile||section==ProfileSection.GameDetails){Text("Coming Soon",TextRole.Secondary,Body,"SubpageMessage");return;}
            var games=source.ReadGames();
            if(section==ProfileSection.Profile)
            {
                var card=new VisualElement{name="ProfilePlayerCard"};ThemeStyles.Card(card);ThemeStyles.Pad(card,Theme.Spacing.MD);card.style.flexDirection=FlexDirection.Row;card.style.alignItems=Align.Center;card.style.flexShrink=0;card.style.marginBottom=Theme.Spacing.MD;Body.Add(card);
                var avatar=Avatar(Profile.Avatar,76);playerImage=avatar.Q<Image>();playerImage.name="ProfilePlayerAvatarImage";avatar.style.marginRight=Theme.Spacing.MD;card.Add(avatar);
                var info=new VisualElement{name="ProfileInfo"};info.style.flexGrow=1;info.style.flexShrink=1;info.style.minWidth=0;card.Add(info);
                playerName=Text(Profile.DisplayName,TextRole.ButtonSecondary,info,"ProfilePlayerName");playerName.enableRichText=false;
                var country=new VisualElement();country.style.flexDirection=FlexDirection.Row;country.style.alignItems=Align.Center;info.Add(country);
                countryFlag=new Image{vectorImage=Profile.Flag,name="ProfileFlag",pickingMode=PickingMode.Ignore};countryFlag.style.width=24;countryFlag.style.height=16;countryFlag.style.flexShrink=0;countryFlag.style.marginRight=Theme.Spacing.SM;country.Add(countryFlag);
                countryName=Text(Profile.CountryName,TextRole.Body,country,"ProfileCountry");
                joined=Text("",TextRole.Caption,info,"ProfileJoined");joined.style.marginTop=Theme.Spacing.SM;
                var actions=new VisualElement{name="ProfileActions"};actions.style.flexDirection=FlexDirection.Row;actions.style.marginBottom=Theme.Spacing.MD;Body.Add(actions);
                var notice=Text("",TextRole.Secondary,Body,"ProfileNotice");notice.style.display=DisplayStyle.None;
                string action=Profile.IsOwnProfile?"Edit Profile":Profile.FriendState==ProfileFriendState.NotFriend?"Add Friend":Profile.FriendState==ProfileFriendState.RequestSent?"Request Sent":"Friends";
                var primary=new ThemeButton(action,()=>{if(Profile.IsOwnProfile)navigate(ProfileSection.EditProfile);else{notice.text="Coming Soon";notice.style.display=DisplayStyle.Flex;}},true){name="ProfilePrimaryAction",tooltip=action};primary.style.flexGrow=1;primary.style.flexBasis=0;actions.Add(primary);
                var share=new ThemeButton("",()=>{notice.text="Sharing coming soon.";notice.style.display=DisplayStyle.Flex;}){name="ShareProfile",tooltip="Share Profile"};share.style.width=52;share.style.marginLeft=Theme.Spacing.SM;share.style.alignItems=Align.Center;share.style.justifyContent=Justify.Center;
                var shareIcon=new Image{vectorImage=Resources.Load<VectorImage>("AppShellMockIcons/icon_share")};ThemeStyles.Icon(shareIcon);share.Add(shareIcon);actions.Add(share);
                var historyHeading=new VisualElement();historyHeading.style.flexDirection=FlexDirection.Row;historyHeading.style.justifyContent=Justify.SpaceBetween;historyHeading.style.alignItems=Align.Center;Body.Add(historyHeading);
                Text("Game History",TextRole.SectionTitle,historyHeading,"HistoryHeading");
                if(!(source is IObservableProfileDataSource))Text("Last 5 games",TextRole.Caption,historyHeading,"HistoryLimit");
            }
            if(source is IObservableProfileDataSource real)historyUnavailable=Text(real.HistoryUnavailableText,TextRole.Secondary,Body,"ProfileHistoryUnavailable");
            else if(section==ProfileSection.GameHistory)Text(games.Count+" games",TextRole.Caption,Body,"HistoryCount").style.marginBottom=Theme.Spacing.SM;
            foreach(var game in games.Take(section==ProfileSection.Profile?MaxVisibleGames:games.Count))AddGame(game,()=>navigate(ProfileSection.GameDetails));
            if(section==ProfileSection.Profile && !(source is IObservableProfileDataSource)){var all=new ThemeButton("View All Games ›",()=>navigate(ProfileSection.GameHistory)){name="ViewAllGames",tooltip="View All Games"};all.style.marginTop=Theme.Spacing.MD;Body.Add(all);}
            RefreshIdentity();
            RegisterCallback<AttachToPanelEvent>(_=>{if(source is IObservableProfileDataSource observable)observable.Changed+=RefreshIdentity;LocalizationSettings.SelectedLocaleChanged+=LocaleChanged;RefreshIdentity();});
            RegisterCallback<DetachFromPanelEvent>(_=>{if(source is IObservableProfileDataSource observable)observable.Changed-=RefreshIdentity;LocalizationSettings.SelectedLocaleChanged-=LocaleChanged;});
        }
        void LocaleChanged(Locale ignored)=>RefreshIdentity();
        void RefreshIdentity()
        {
            Profile=source.ReadProfile();
            bool es=DominoLocalization.Language=="es";
            var title=Body.Q<Label>("SubpageTitle");if(title!=null)title.text=Section==ProfileSection.Profile?(es?"Perfil":"Profile"):Section==ProfileSection.GameHistory?(es?"Historial de partidas":"Game History"):(es?"Detalles de partida":"Game Details");
            var action=Body.Q<Button>("ProfilePrimaryAction");if(action!=null&&Profile.IsOwnProfile){action.text=es?"Editar perfil":"Edit Profile";action.tooltip=action.text;}
            var history=Body.Q<Label>("HistoryHeading");if(history!=null)history.text=es?"Historial de partidas":"Game History";
            if(playerName!=null) {
                playerName.text=Profile.DisplayName;countryName.text=Profile.CountryName;
                countryFlag.vectorImage=Profile.Flag;countryFlag.tooltip=Profile.Flag==null?null:Profile.CountryName;
                countryFlag.style.visibility=Profile.Flag==null?Visibility.Hidden:Visibility.Visible;
                joined.text=Profile.JoinedLabel??(Profile.JoinedAt.HasValue?"Joined "+Profile.JoinedAt.Value.ToString("MMMM d, yyyy",CultureInfo.GetCultureInfo("en-US")):"Joined date unavailable");
                playerImage.image=Profile.Avatar;playerImage.vectorImage=Profile.AvatarIcon;
                playerImage.scaleMode=Profile.AvatarIcon==null?ScaleMode.ScaleAndCrop:ScaleMode.ScaleToFit;
                playerImage.tintColor=Profile.AvatarIcon==null?Color.white:Theme.Colors.IconInactive;
            }
            if(historyUnavailable!=null && source is IObservableProfileDataSource real)historyUnavailable.text=real.HistoryUnavailableText;
        }
        Label Text(string value,TextRole role,VisualElement parent,string elementName)
        {
            var label=new Label(value){name=elementName,pickingMode=PickingMode.Ignore};ThemeStyles.Text(label,role);label.style.whiteSpace=WhiteSpace.Normal;label.style.unityTextAlign=TextAnchor.MiddleLeft;label.style.flexShrink=1;parent.Add(label);return label;
        }
        VisualElement Avatar(Texture2D texture,int size)
        {
            var frame=new VisualElement{name="PlayerAvatar",tooltip="Player avatar",pickingMode=PickingMode.Ignore};frame.style.width=frame.style.height=size;frame.style.flexShrink=0;frame.style.overflow=Overflow.Hidden;ThemeStyles.Round(frame,size/2);
            var image=new Image{image=texture,scaleMode=ScaleMode.ScaleAndCrop,pickingMode=PickingMode.Ignore};image.style.width=image.style.height=Length.Percent(100);frame.Add(image);return frame;
        }
        void AddGame(ProfileGameSummary game,Action open)
        {
            var row=new ThemeButton("",open){name="ProfileGameRow",tooltip=game.Mode+" "+game.Matchup+", "+game.Score+", "+game.Result};row.style.flexDirection=FlexDirection.Row;row.style.alignItems=Align.Center;row.style.minHeight=88;row.style.height=StyleKeyword.Auto;ThemeStyles.Pad(row,Theme.Spacing.SM);row.style.paddingTop=row.style.paddingBottom=Theme.Spacing.MD;Body.Add(row);
            var avatar=Avatar(game.OpponentAvatar,32);avatar.style.marginRight=Theme.Spacing.SM;row.Add(avatar);
            var main=new VisualElement{name="GameText",pickingMode=PickingMode.Ignore};main.style.flexGrow=1;main.style.flexShrink=1;main.style.minWidth=0;row.Add(main);
            var description=Text(game.Mode+" · "+game.Matchup,TextRole.Secondary,main,"GameMatchup");description.style.color=Theme.Colors.TextPrimary;description.style.flexShrink=0;description.style.marginBottom=Theme.Spacing.XS;
            var result=Text(game.Result,TextRole.Secondary,main,"GameResult");result.style.color=game.Won?Theme.Colors.Win:Theme.Colors.Loss;result.style.flexShrink=0;
            var score=Text(game.Score,TextRole.ButtonPrimary,row,"GameScore");score.style.width=score.style.minWidth=76;score.style.flexShrink=0;score.style.marginLeft=Theme.Spacing.SM;score.style.whiteSpace=WhiteSpace.NoWrap;score.style.unityTextAlign=TextAnchor.MiddleRight;
            var chevron=new Image{name="GameChevron",vectorImage=Resources.Load<VectorImage>("AppShellMockIcons/icon_menu_chevron")};ThemeStyles.Icon(chevron,false);chevron.style.width=chevron.style.height=16;chevron.style.marginLeft=Theme.Spacing.XS;row.Add(chevron);
        }
    }
}
