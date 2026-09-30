using System;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    public sealed class ProductionMenuPage : ProductionRootPage
    {
        public AppTheme Theme => ThemeProvider.Current;
        public static string Title(MenuDestination destination)=>destination==MenuDestination.Support?"Help & Support":destination.ToString();
        public ProductionMenuPage(IMenuDataSource source,Action<MenuDestination> navigate) : base("", "")
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            if(navigate==null)throw new ArgumentNullException(nameof(navigate));
            Body.Clear();verticalScrollerVisibility=ScrollerVisibility.Hidden;
            var eyebrow=Text("YOUR CORNER",TextRole.Caption,Body,"MenuEyebrow");RootVisualRhythm.Eyebrow(eyebrow);
            eyebrow.style.marginBottom=Theme.Spacing.SM;
            Text("Menu",TextRole.PageTitle,Body,"MenuTitle").style.marginBottom=Theme.Spacing.MD;
            var data=source.Read();
            var profile=new ThemeButton("",()=>navigate(MenuDestination.Profile)){name="MenuProfile",tooltip="Open Profile"};
            profile.style.flexDirection=FlexDirection.Row;profile.style.alignItems=Align.Center;
            profile.style.minHeight=Theme.Sizing.ProfileTouchHeight;profile.style.paddingTop=profile.style.paddingBottom=Theme.Spacing.SM;
            profile.style.marginBottom=Theme.Spacing.MD;
            var avatar=new Image{vectorImage=data.Avatar,name="MenuAvatar"};ThemeStyles.Icon(avatar,false);
            avatar.style.width=avatar.style.height=48;avatar.style.marginRight=Theme.Spacing.IconTextGap;
            profile.Add(avatar);
            var info=new VisualElement{pickingMode=PickingMode.Ignore};info.style.flexGrow=1;info.style.minWidth=0;profile.Add(info);
            Text(data.DisplayName,TextRole.ButtonSecondary,info,"MenuDisplayName");
            Text(data.MembershipLabel+" · "+data.ClubLabel,TextRole.Secondary,info,"MenuMembership");
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
        }
        Label Text(string value,TextRole role,VisualElement parent,string elementName)
        {
            var label=new Label(value){name=elementName,pickingMode=PickingMode.Ignore};ThemeStyles.Text(label,role);
            label.style.flexShrink=0;label.style.whiteSpace=WhiteSpace.Normal;label.style.unityTextAlign=TextAnchor.MiddleLeft;parent.Add(label);return label;
        }
        static Image Icon(string key,string elementName)=>new Image{vectorImage=Resources.Load<VectorImage>("AppShellMockIcons/icon_menu_"+key),name=elementName};
    }
}
