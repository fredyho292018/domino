using System;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    public abstract class ProductionContentRoot : ProductionRootPage
    {
        public AppTheme Theme=>ThemeProvider.Current;
        protected ProductionContentRoot(string title,string subtitle):base("", "")
        {
            Body.Clear();verticalScrollerVisibility=ScrollerVisibility.Hidden;
            Text("CUBAN DOMINO CLUB",TextRole.Caption,Body,"RootEyebrow").style.color=Theme.Colors.Primary;
            Text(title,TextRole.PageTitle,Body,"RootTitle");
            if(!string.IsNullOrEmpty(subtitle))Text(subtitle,TextRole.Secondary,Body,"RootSubtitle");
        }
        protected Label Text(string value,TextRole role,VisualElement parent,string elementName="")
        {
            var label=new Label(value){name=elementName,pickingMode=PickingMode.Ignore};ThemeStyles.Text(label,role);label.style.whiteSpace=WhiteSpace.Normal;label.style.flexShrink=0;label.style.marginBottom=Theme.Spacing.SM;label.style.unityTextAlign=TextAnchor.MiddleLeft;parent.Add(label);return label;
        }
        protected void Section(string title){var label=Text(title,TextRole.SectionTitle,Body,"Section"+title.Replace(" ",""));label.style.marginTop=Theme.Spacing.MD;}
        protected VisualElement Card(string elementName)
        {
            var card=new VisualElement{name=elementName};ThemeStyles.Card(card);card.style.flexShrink=0;card.style.marginBottom=Theme.Spacing.MD;Body.Add(card);return card;
        }
    }
    public sealed class ProductionPuzzlesPage : ProductionContentRoot
    {
        public ProductionPuzzlesPage(IPuzzlesDataSource source,Action start):base("Puzzles","Train your domino vision.")
        {
            var daily=source.Daily();Section("Daily Puzzle");var card=Card("DailyPuzzle");
            Text(daily.Title,TextRole.SectionTitle,card);Text(daily.Description,TextRole.Body,card);
            Text("Difficulty",TextRole.Caption,card);Text(daily.Difficulty,TextRole.Body,card,"PuzzleDifficulty");
            Text("Progress",TextRole.Caption,card);Text(daily.Progress,TextRole.Secondary,card,"PuzzleProgress");
            card.Add(new ThemeButton("Start Puzzle",start,true){name="StartPuzzle"});
            Section("Puzzle Categories");foreach(var category in source.Categories()){var item=Card("PuzzleCategory");Text(category.Name,TextRole.ButtonSecondary,item,"CategoryName");Text(category.Description,TextRole.Secondary,item);}
        }
    }
    public sealed class ProductionLearnPage : ProductionContentRoot
    {
        public HomeCoachSummary Coach { get; }
        public ProductionLearnPage(ILearnDataSource source,Action lesson,Action puzzles,Action coachGame):base("Learn", "")
        {
            Coach=source.Coach;Section("Your Coach");var card=Card("LearnCoachCard");
            var portrait=new VisualElement{name="LearnCoachAvatar"};portrait.style.width=portrait.style.height=120;portrait.style.alignSelf=Align.Center;portrait.style.flexShrink=0;portrait.style.overflow=Overflow.Hidden;portrait.style.marginBottom=Theme.Spacing.MD;ThemeStyles.Round(portrait,60);
            var image=new Image{image=Coach.Avatar,scaleMode=ScaleMode.ScaleAndCrop,pickingMode=PickingMode.Ignore};image.style.width=image.style.height=Length.Percent(100);portrait.Add(image);card.Add(portrait);
            Text(Coach.DisplayName,TextRole.SectionTitle,card,"LearnCoachName");Text(Coach.Greeting,TextRole.Body,card,"LearnCoachGreeting");
            card.Add(new ThemeButton("Continue Learning",lesson,true){name="LearnContinue"});
            Section("Training");Body.Add(new ThemeButton("Lessons",lesson){name="LearnLessons"});Body.Add(new ThemeButton("Puzzles",puzzles){name="LearnPuzzles"});Body.Add(new ThemeButton("Coach Games",coachGame){name="LearnCoachGames"});
        }
    }
    public sealed class ProductionWatchPage : ProductionContentRoot
    {
        public ProductionWatchPage(IWatchDataSource source,Action watch):base("Watch","Watch games. Learn from every move.")
        {
            var games=source.Games();for(int i=0;i<games.Count;i++){
                if(i==0)Section("Featured");else if(i==1)Section("Recent Games");
                var game=games[i];var card=new ThemeButton("",watch){name="WatchCard",tooltip=game.Players};ThemeStyles.Pad(card,Theme.Spacing.CardPadding);card.style.marginBottom=Theme.Spacing.MD;card.style.alignItems=Align.Stretch;card.style.height=StyleKeyword.Auto;
                Text(game.Mode,TextRole.Caption,card,"WatchMode");Text(game.Players,TextRole.ButtonSecondary,card,"WatchPlayers");Text(game.Duration,TextRole.Secondary,card,"WatchDuration");Body.Add(card);
            }
        }
    }
}
