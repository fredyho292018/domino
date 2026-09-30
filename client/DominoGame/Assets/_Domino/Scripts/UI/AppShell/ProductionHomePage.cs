using System;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    public sealed class ProductionHomePage : ProductionRootPage
    {
        readonly IHomeDataSource source;
        readonly Action continueLearning;
        public AppTheme Theme => ThemeProvider.Current;
        public HomeContentState ContentState { get; private set; }
        public ProductionHomePage(IHomeDataSource source, Action continueLearning) : base("", "")
        {
            this.source=source ?? throw new ArgumentNullException(nameof(source));
            this.continueLearning=continueLearning ?? throw new ArgumentNullException(nameof(continueLearning));
            verticalScrollerVisibility=ScrollerVisibility.Hidden;
            Refresh();
        }
        Label Text(string value, TextRole role, VisualElement parent, string elementName="")
        {
            var label=new Label(value){name=elementName};ThemeStyles.Text(label,role);
            label.style.whiteSpace=WhiteSpace.Normal;label.style.flexShrink=0;
            label.style.marginBottom=Theme.Spacing.SM;parent.Add(label);return label;
        }
        VisualElement Card(string title, string detail, string elementName)
        {
            var card=new VisualElement{name=elementName};ThemeStyles.Card(card);
            card.style.flexShrink=0;card.style.marginBottom=Theme.Spacing.MD;
            Text(title,TextRole.SectionTitle,card);Text(detail,TextRole.Secondary,card);Body.Add(card);return card;
        }
        public void Refresh()
        {
            Body.Clear();var data=source.Read();ContentState=data?.State ?? HomeContentState.Empty;
            var identity=Text("CUBAN DOMINO CLUB",TextRole.Caption,Body,"HomeIdentity");identity.style.color=Theme.Colors.Primary;
            if(ContentState!=HomeContentState.Content)
            {
                Text(ContentState==HomeContentState.Loading?"Loading…":ContentState==HomeContentState.Error?"Unable to load Home.":"Nothing here yet.",TextRole.Body,Body,"HomeStatus");return;
            }
            Text("Hola, "+data.DisplayName+".",TextRole.PageTitle,Body,"HomeGreeting");
            Text("Una buena partida empieza con una buena mesa.",TextRole.Secondary,Body,"HomeSubtitle");
            var play=Card("¿Jugamos?","Strategy, connection and a little Cuban spirit.","HomePlayCard");
            var notice=new Label("Coming Soon"){name="HomePlayNotice"};ThemeStyles.Text(notice,TextRole.Secondary);notice.style.display=DisplayStyle.None;
            var button=new ThemeButton("PLAY →",()=>notice.style.display=DisplayStyle.Flex,true){name="HomePlay"};play.Add(button);play.Add(notice);
            if(data.Coach!=null)
            {
                var coach=Card(data.Coach.DisplayName,data.Coach.Greeting,"HomeCoachCard");
                var portrait=new VisualElement{name="HomeCoachPortrait"};portrait.style.width=portrait.style.height=120;portrait.style.flexShrink=0;
                portrait.style.alignSelf=Align.Center;portrait.style.overflow=Overflow.Hidden;ThemeStyles.Round(portrait,60);
                var image=new Image{image=data.Coach.Avatar,scaleMode=ScaleMode.ScaleAndCrop};image.style.width=image.style.height=Length.Percent(100);portrait.Add(image);coach.Add(portrait);
            }
            Body.Add(new ThemeButton("Continue Learning",continueLearning){name="HomeContinueLearning"});
            if(data.Friends!=null)Card("Your table of friends",data.Friends.Count+" "+data.Friends.Description,"HomeFriendsCard");
        }
    }
}
