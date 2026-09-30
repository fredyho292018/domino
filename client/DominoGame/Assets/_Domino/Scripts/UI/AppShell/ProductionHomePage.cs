using System;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    // Opt-in rhythm for the four polished roots; no shared theme or Auth changes.
    internal static class RootVisualRhythm
    {
        static AppTheme Theme=>ThemeProvider.Current;
        public static void Eyebrow(Label label){label.style.flexShrink=0;label.style.marginBottom=Theme.Spacing.SM;label.style.color=Theme.Colors.Primary;}
        public static void Cta(ThemeButton button){button.style.unityTextAlign=TextAnchor.MiddleCenter;button.style.marginBottom=0;}
        public static void Card(VisualElement card){card.style.flexShrink=0;card.style.marginBottom=Theme.Spacing.MD;}
        public static void Compact(VisualElement card){card.style.paddingTop=card.style.paddingBottom=Theme.Spacing.MD;card.style.minHeight=Theme.Sizing.MinTouchTarget;}
    }
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
            RootVisualRhythm.Card(card);
            Text(title,TextRole.SectionTitle,card);var detailLabel=Text(detail,TextRole.Secondary,card);detailLabel.style.marginBottom=0;Body.Add(card);return card;
        }
        public void Refresh()
        {
            Body.Clear();var data=source.Read();ContentState=data?.State ?? HomeContentState.Empty;
            var identity=Text("CUBAN DOMINO CLUB",TextRole.Caption,Body,"HomeIdentity");RootVisualRhythm.Eyebrow(identity);
            if(ContentState!=HomeContentState.Content)
            {
                Text(ContentState==HomeContentState.Loading?"Loading…":ContentState==HomeContentState.Error?"Unable to load Home.":"Nothing here yet.",TextRole.Body,Body,"HomeStatus");return;
            }
            Text("Hola, "+data.DisplayName+".",TextRole.PageTitle,Body,"HomeGreeting");
            Text("Una buena partida empieza con una buena mesa.",TextRole.Secondary,Body,"HomeSubtitle").style.marginBottom=Theme.Spacing.SectionGap;
            var play=Card("¿Jugamos?","Strategy, connection and a little Cuban spirit.","HomePlayCard");
            var notice=new Label("Coming Soon"){name="HomePlayNotice"};ThemeStyles.Text(notice,TextRole.Secondary);notice.style.display=DisplayStyle.None;
            var button=new ThemeButton("PLAY →",()=>notice.style.display=DisplayStyle.Flex,true){name="HomePlay"};RootVisualRhythm.Cta(button);button.style.marginTop=Theme.Spacing.MD;play.Add(button);play.Add(notice);
            if(data.Coach!=null)
            {
                var coach=Card(data.Coach.DisplayName,data.Coach.Greeting,"HomeCoachCard");
                var portrait=new VisualElement{name="HomeCoachPortrait"};portrait.style.width=portrait.style.height=120;portrait.style.flexShrink=0;
                portrait.style.marginTop=Theme.Spacing.MD;portrait.style.alignSelf=Align.Center;portrait.style.overflow=Overflow.Hidden;ThemeStyles.Round(portrait,60);
                var image=new Image{image=data.Coach.Avatar,scaleMode=ScaleMode.ScaleAndCrop};image.style.width=image.style.height=Length.Percent(100);portrait.Add(image);coach.Add(portrait);
            }
            var learning=new ThemeButton("Continue Learning",continueLearning){name="HomeContinueLearning"};RootVisualRhythm.Cta(learning);learning.style.marginBottom=Theme.Spacing.SectionGap;Body.Add(learning);
            if(data.Friends!=null)Card("Your table of friends",data.Friends.Count+" "+data.Friends.Description,"HomeFriendsCard");
        }
    }
}
