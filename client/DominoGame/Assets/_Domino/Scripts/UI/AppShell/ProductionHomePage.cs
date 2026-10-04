using System;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

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
            RegisterCallback<AttachToPanelEvent>(_=>{
                if(source is IObservableHomeDataSource observable)observable.Changed+=Refresh;
                LocalizationSettings.SelectedLocaleChanged+=LocaleChanged;
                LocaleChanged(LocalizationSettings.SelectedLocale);
            });
            RegisterCallback<DetachFromPanelEvent>(_=>{
                if(source is IObservableHomeDataSource observable)observable.Changed-=Refresh;
                LocalizationSettings.SelectedLocaleChanged-=LocaleChanged;
            });
        }
        void LocaleChanged(Locale locale) { if(locale!=null && source is IObservableHomeDataSource observable)observable.SetLocale(locale.Identifier.Code); Refresh(); }
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
            bool es=DominoLocalization.Language=="es";
            var previousScroll=scrollOffset;
            Body.Clear();var data=source.Read();ContentState=data?.State ?? HomeContentState.Empty;
            var identity=Text("CUBAN DOMINO CLUB",TextRole.Caption,Body,"HomeIdentity");RootVisualRhythm.Eyebrow(identity);
            if(ContentState!=HomeContentState.Content)
            {
                Text(ContentState==HomeContentState.Loading?"Loading…":ContentState==HomeContentState.Error?"Unable to load Home.":"Nothing here yet.",TextRole.Body,Body,"HomeStatus");return;
            }
            Text(data.Greeting ?? "Hola, "+data.DisplayName+".",TextRole.PageTitle,Body,"HomeGreeting").enableRichText=false;
            Text(es?"Una buena partida empieza con una buena mesa.":"A good game starts with a good table.",TextRole.Secondary,Body,"HomeSubtitle").style.marginBottom=Theme.Spacing.SectionGap;
            var play=Card(es?"¿Jugamos?":"Shall we play?",es?"Estrategia, conexión y un poco de espíritu cubano.":"Strategy, connection and a little Cuban spirit.","HomePlayCard");
            var notice=new Label("Coming Soon"){name="HomePlayNotice"};ThemeStyles.Text(notice,TextRole.Secondary);notice.style.display=DisplayStyle.None;
            var button=new ThemeButton(es?"JUGAR →":"PLAY →",()=>notice.style.display=DisplayStyle.Flex,true){name="HomePlay"};RootVisualRhythm.Cta(button);button.style.marginTop=Theme.Spacing.MD;play.Add(button);play.Add(notice);
            if(data.Coach!=null)
            {
                var coach=Card(data.Coach.DisplayName,data.Coach.Greeting,"HomeCoachCard");
                var labels=coach.Query<Label>().ToList();labels[0].name="HomeCoachName";labels[1].name="HomeCoachDescription";
                labels[0].enableRichText=labels[1].enableRichText=false;
                var portrait=new VisualElement{name="HomeCoachPortrait"};portrait.style.width=portrait.style.height=120;portrait.style.flexShrink=0;
                portrait.style.marginTop=Theme.Spacing.MD;portrait.style.alignSelf=Align.Center;portrait.style.overflow=Overflow.Hidden;ThemeStyles.Round(portrait,60);
                var image=new Image{image=data.Coach.Avatar,scaleMode=ScaleMode.ScaleAndCrop,name="HomeCoachAvatar"};
                if(data.Coach.Avatar==null){image.vectorImage=Resources.Load<VectorImage>("AppShellMockIcons/icon_menu_avatar");image.scaleMode=ScaleMode.ScaleToFit;}
                image.style.width=image.style.height=Length.Percent(100);portrait.Add(image);coach.Add(portrait);
            }
            else if(!string.IsNullOrEmpty(data.CoachStatus))Text(data.CoachStatus,TextRole.Secondary,Body,"HomeCoachStatus");
            var learning=new ThemeButton(es?"Seguir aprendiendo":"Continue Learning",continueLearning){name="HomeContinueLearning"};RootVisualRhythm.Cta(learning);learning.style.marginBottom=Theme.Spacing.SectionGap;Body.Add(learning);
            if(data.Friends!=null)Card("Your table of friends",data.Friends.Count+" "+data.Friends.Description,"HomeFriendsCard");
            schedule.Execute(()=>scrollOffset=previousScroll);
        }
    }
}
