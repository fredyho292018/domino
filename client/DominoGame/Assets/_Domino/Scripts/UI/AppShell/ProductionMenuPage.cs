using System;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Domino.UI.AppShell
{
    public sealed class ProductionMenuPage : ProductionRootPage
    {
        public AppTheme Theme => ThemeProvider.Current;
        readonly IMenuDataSource source;
        readonly Label displayName, membership;
        readonly Image avatar;
        public static string Title(MenuDestination destination)=>destination==MenuDestination.Support?"Help & Support":destination.ToString();
        public ProductionMenuPage(IMenuDataSource source,Action<MenuDestination> navigate, Action signOut=null) : base("", "")
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            if(navigate==null)throw new ArgumentNullException(nameof(navigate));
            this.source=source;
            Body.Clear();verticalScrollerVisibility=ScrollerVisibility.Hidden;
            var eyebrow=Text("YOUR CORNER",TextRole.Caption,Body,"MenuEyebrow");RootVisualRhythm.Eyebrow(eyebrow);
            eyebrow.style.marginBottom=Theme.Spacing.SM;
            Text("Menu",TextRole.PageTitle,Body,"MenuTitle").style.marginBottom=Theme.Spacing.MD;
            var profile=new ThemeButton("",()=>navigate(MenuDestination.Profile)){name="MenuProfile",tooltip="Open Profile"};
            profile.style.flexDirection=FlexDirection.Row;profile.style.alignItems=Align.Center;
            profile.style.minHeight=Theme.Sizing.ProfileTouchHeight;profile.style.paddingTop=profile.style.paddingBottom=Theme.Spacing.SM;
            profile.style.marginBottom=Theme.Spacing.MD;
            avatar=new Image{name="MenuAvatar"};ThemeStyles.Icon(avatar,false);
            avatar.style.width=avatar.style.height=48;avatar.style.marginRight=Theme.Spacing.IconTextGap;
            profile.Add(avatar);
            var info=new VisualElement{pickingMode=PickingMode.Ignore};info.style.flexGrow=1;info.style.minWidth=0;profile.Add(info);
            displayName=Text("",TextRole.ButtonSecondary,info,"MenuDisplayName");
            membership=Text("",TextRole.Secondary,info,"MenuMembership");
            RefreshIdentity();
            RegisterCallback<AttachToPanelEvent>(_=>{
                if(source is IObservableMenuDataSource observable)observable.Changed+=RefreshIdentity;
                LocalizationSettings.SelectedLocaleChanged+=LocaleChanged;
                RefreshIdentity();
            });
            RegisterCallback<DetachFromPanelEvent>(_=>{
                if(source is IObservableMenuDataSource observable)observable.Changed-=RefreshIdentity;
                LocalizationSettings.SelectedLocaleChanged-=LocaleChanged;
            });
            Body.Add(profile);
            var groups=new[]{"SOCIAL","ACTIVITY","PERSONALIZATION","APP"};
            var destinations=new[]{MenuDestination.Friends,MenuDestination.Messages,MenuDestination.Stats,MenuDestination.Coach,MenuDestination.Theme,MenuDestination.Membership,MenuDestination.Settings,MenuDestination.Support};
            for(int i=0;i<destinations.Length;i++)
            {
                if(i%2==0){var group=Text(groups[i/2],TextRole.Caption,Body,"MenuGroup"+i/2);group.style.color=Theme.Colors.Primary;group.style.marginTop=Theme.Spacing.SectionGap;group.style.marginBottom=Theme.Spacing.SM;}
                var destination=destinations[i];
                var row=new ThemeButton("",()=>navigate(destination)){name="MenuRow"+destination};
                var icon=Icon(destination.ToString().ToLowerInvariant(),"MenuRowIcon");
                var chevron=Icon("chevron","MenuRowChevron");
                ThemeStyles.RowContent(row,icon,Title(destination),"",chevron,false);
                icon.tintColor=Theme.Colors.IconInactive;
                Body.Add(row);
            }
            if(signOut!=null) {
                var row=new ThemeButton("",signOut){name="MenuSignOut",tooltip="Sign Out"};
                var icon=new Image{vectorImage=Resources.Load<VectorImage>("AuthIcons/icon_sign_out"),name="LogoutIcon"};
                ThemeStyles.RowContent(row,icon,"Sign Out","",Icon("chevron","LogoutChevron"),false);
                Body.Add(row);
            }
        }
        void LocaleChanged(Locale ignored)=>RefreshIdentity();
        void RefreshIdentity()
        {
            var data=source.Read();
            displayName.text=data.DisplayName;
            membership.text=string.IsNullOrEmpty(data.MembershipLabel)?data.ClubLabel:data.MembershipLabel+" · "+data.ClubLabel;
            avatar.vectorImage=data.Avatar;
        }
        Label Text(string value,TextRole role,VisualElement parent,string elementName)
        {
            var label=new Label(value){name=elementName,pickingMode=PickingMode.Ignore};ThemeStyles.Text(label,role);
            label.style.flexShrink=0;label.style.whiteSpace=WhiteSpace.Normal;label.style.unityTextAlign=TextAnchor.MiddleLeft;parent.Add(label);return label;
        }
        static Image Icon(string key,string elementName)=>new Image{vectorImage=Resources.Load<VectorImage>("AppShellMockIcons/icon_menu_"+key),name=elementName};
    }
}
