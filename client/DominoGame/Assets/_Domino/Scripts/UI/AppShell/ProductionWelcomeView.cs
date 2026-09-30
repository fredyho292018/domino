using System;
using Domino.Identity;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    public sealed class ProductionWelcomeView : ScrollView
    {
        public VisualElement Body { get; } = new VisualElement { name="WelcomeBody" };
        readonly ThemeButton guest;
        readonly Label message;
        AppTheme Theme=>ThemeProvider.Current;
        public ProductionWelcomeView(Action continueGuest, Action continueEmail=null, Action signIn=null) : base(ScrollViewMode.Vertical)
        {
            name="ProductionWelcome";style.flexGrow=1;style.minHeight=0;style.backgroundColor=Theme.Colors.Background;
            verticalScrollerVisibility=horizontalScrollerVisibility=ScrollerVisibility.Hidden;
            contentContainer.style.flexGrow=1;contentContainer.style.justifyContent=Justify.FlexStart;
            ThemeStyles.Page(Body);Body.style.flexShrink=0;Add(Body);
            var eyebrow=Text("A SEAT AT THE TABLE",TextRole.Caption);eyebrow.style.color=Theme.Colors.Primary;eyebrow.style.letterSpacing=2;eyebrow.style.marginBottom=12;
            Text("CUBAN DOMINO CLUB",TextRole.PageTitle).style.marginBottom=12;
            var sub=Text("Tu próxima partida empieza aquí.",TextRole.Body);sub.style.fontSize=16;sub.style.marginBottom=8;
            Text("Tradición. Estrategia. Comunidad.",TextRole.Secondary).style.marginBottom=24;
            Row("Google","Fast, secure and easy","google",()=>Unavailable(),true);
            Row("Facebook","Play with your friends","facebook",()=>Unavailable());
            Row("Email","Use your email and password","email",()=>{if(continueEmail!=null)continueEmail();else Unavailable();});
            Row("Phone","Sign in with your phone number","phone",()=>Unavailable());
            var separator=new VisualElement{name="WelcomeSeparator"};separator.style.flexDirection=FlexDirection.Row;separator.style.alignItems=Align.Center;separator.style.marginTop=7;separator.style.marginBottom=16;
            foreach(var side in new[]{0,1}){var line=new VisualElement();line.style.height=1;line.style.flexGrow=1;line.style.backgroundColor=Theme.Colors.Surface;separator.Add(line);if(side==0){var or=new Label("or");ThemeStyles.Text(or,TextRole.Caption);or.style.marginLeft=or.style.marginRight=12;separator.Add(or);}}
            Body.Add(separator);
            guest=Row("Guest","Play now, create an account later","guest",continueGuest);
            var signin=new VisualElement{name="WelcomeSignInRow"};signin.style.flexDirection=FlexDirection.Row;signin.style.justifyContent=Justify.Center;signin.style.alignItems=Align.Center;signin.style.marginTop=8;
            var prompt=new Label("Already have an account?");ThemeStyles.Text(prompt,TextRole.Secondary);signin.Add(prompt);
            // Inline action: ThemeButton intentionally paints a surface on hover/focus.
            var link=new Button(()=>signIn?.Invoke()){name="SignIn",text="Sign In"};
            ThemeStyles.Text(link,TextRole.ButtonPrimary);link.style.fontSize=13;link.style.color=Theme.Colors.Primary;
            link.style.backgroundColor=Color.clear;link.style.backgroundImage=StyleKeyword.None;ThemeStyles.Border(link,Color.clear,0);
            link.style.marginLeft=6;link.style.marginRight=link.style.marginTop=link.style.marginBottom=0;
            link.style.paddingLeft=link.style.paddingRight=link.style.paddingTop=link.style.paddingBottom=0;
            link.style.minWidth=44;link.style.minHeight=44;link.style.flexShrink=0;
            link.style.alignItems=Align.Center;link.style.justifyContent=Justify.Center;link.style.unityTextAlign=TextAnchor.MiddleCenter;
            signin.Add(link);Body.Add(signin);
            message=Text("",TextRole.Secondary);message.name="AuthStatus";message.style.display=DisplayStyle.None;
        }
        Label Text(string text,TextRole role){var label=new Label(text);ThemeStyles.Text(label,role);label.style.whiteSpace=WhiteSpace.Normal;label.style.flexShrink=0;label.style.unityTextAlign=TextAnchor.MiddleLeft;label.style.marginLeft=label.style.marginRight=0;label.style.marginBottom=8;Body.Add(label);return label;}
        Image Icon(string asset,float size,Color tint){var i=new Image{vectorImage=Resources.Load<VectorImage>("AppShellMockIcons/"+asset),tintColor=tint,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};i.style.width=i.style.height=size;i.style.flexShrink=0;return i;}
        ThemeButton Row(string provider,string description,string asset,Action action,bool primary=false)
        {
            string title=provider=="Guest"?"Continue as Guest":"Continue with "+provider;
            var row=new ThemeButton("",action,primary){name="Auth"+provider,tooltip=title};ThemeStyles.Row(row,true);row.style.paddingLeft=row.style.paddingRight=12;row.style.paddingTop=row.style.paddingBottom=6;row.style.marginBottom=8;row.style.marginLeft=row.style.marginRight=0;
            var area=new VisualElement{name="AuthIconArea",pickingMode=PickingMode.Ignore};area.style.width=area.style.height=34;area.style.flexShrink=0;area.style.alignItems=Align.Center;area.style.justifyContent=Justify.Center;ThemeStyles.Round(area,17);
            // Provider brand colors are not application palette replacements.
            area.style.backgroundColor=provider=="Google"?Theme.Colors.TextPrimary:provider=="Facebook"?new Color32(24,119,242,255):new Color(0,0,0,.15f);
            var icon=Icon("icon_auth_"+asset,22,provider=="Phone"?Theme.Colors.Primary:provider=="Google"||provider=="Facebook"?Color.white:Theme.Colors.TextPrimary);icon.name="AuthProviderIcon";area.Add(icon);row.Add(area);
            var divider=new VisualElement{pickingMode=PickingMode.Ignore};divider.style.width=1;divider.style.height=26;divider.style.flexShrink=0;divider.style.marginLeft=divider.style.marginRight=10;divider.style.backgroundColor=new Color(1,1,1,primary?.25f:.12f);row.Add(divider);
            var text=new VisualElement{pickingMode=PickingMode.Ignore};text.style.flexGrow=1;text.style.flexShrink=1;text.style.minWidth=0;
            var label=new Label(title){name="AuthPrimaryLabel",pickingMode=PickingMode.Ignore};ThemeStyles.Text(label,TextRole.ButtonPrimary);label.style.whiteSpace=WhiteSpace.Normal;label.style.unityTextAlign=TextAnchor.MiddleLeft;text.Add(label);
            var detail=new Label(description){name="AuthSecondaryLabel",pickingMode=PickingMode.Ignore};ThemeStyles.Text(detail,TextRole.Caption);detail.style.whiteSpace=WhiteSpace.Normal;detail.style.unityTextAlign=TextAnchor.MiddleLeft;detail.style.color=primary?Theme.Colors.TextPrimary:Theme.Colors.TextSecondary;text.Add(detail);row.Add(text);
            var chevron=Icon("icon_menu_chevron",14,primary?Theme.Colors.TextPrimary:Theme.Colors.TextSecondary);chevron.style.marginLeft=6;row.Add(chevron);Body.Add(row);return row;
        }
        void Unavailable(){message.text="Coming Soon";message.style.display=DisplayStyle.Flex;}
        public void SetState(bool busy,string status){guest.SetEnabled(!busy);message.text=status??"";message.style.display=string.IsNullOrEmpty(status)?DisplayStyle.None:DisplayStyle.Flex;}
    }
}
