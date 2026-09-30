using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.UI.Theming
{
    // Isolated opt-in sample. No application services or real routes.
    public sealed class ThemeProbeView:ScrollView
    {
        public AppTheme Theme {get;}=ThemeProvider.Resolve();
        public int Actions {get;private set;}
        public ThemeProbeView():base(ScrollViewMode.Vertical)
        {
            name="ProductionThemeProbe";style.flexGrow=1;style.minHeight=0;style.backgroundColor=Theme.Colors.Background;
            horizontalScrollerVisibility=ScrollerVisibility.Hidden;
            var content=new VisualElement{name="ThemeContent"};ThemeStyles.Page(content);Add(content);
            var heading=new Label("ModernSocialPremium");ThemeStyles.Text(heading,TextRole.PageTitle);heading.style.whiteSpace=WhiteSpace.Normal;content.Add(heading);
            var caption=new Label("Isolated theme probe"){name="Secondary"};ThemeStyles.Text(caption,TextRole.Secondary);content.Add(caption);
            content.Add(new ThemeButton("Primary",()=>Actions++,true){name="Primary"});content.Add(new ThemeButton("Secondary",()=>Actions++){name="SecondaryButton"});
            var card=new VisualElement{name="Card"};ThemeStyles.Card(card,true);var label=new Label("Selected card");ThemeStyles.Text(label,TextRole.Body);card.Add(label);content.Add(card);
            var back=new ThemeButton("",()=>Actions++){name="Back"};var arrow=new Image();ThemeStyles.Back(back,arrow);back.Add(arrow);content.Add(back);
            var inactive=new Image{name="InactiveIcon"};inactive.vectorImage=arrow.vectorImage;ThemeStyles.Icon(inactive,false);content.Add(inactive);
            content.Add(ThemeStyles.Input("Example field","Placeholder"));
            foreach(bool auth in new[]{false,true}){var row=new ThemeButton("",()=>Actions++){name=auth?"AuthRow":"MenuRow"};var icon=new Image{vectorImage=arrow.vectorImage};var chevron=new Image{vectorImage=arrow.vectorImage};ThemeStyles.RowContent(row,icon,auth?"Provider sample":"Menu sample","Secondary text",chevron,auth);content.Add(row);}
            var segments=new VisualElement{name="Segments"};ThemeStyles.Segmented(segments);
            foreach(var text in new[]{"Yearly","Monthly"}){var segment=new ThemeButton(text,()=>Actions++){Selected=text=="Yearly"};segment.style.flexGrow=1;segments.Add(segment);}content.Add(segments);
            var navigation=new VisualElement{name="BottomNavigation"};navigation.style.flexDirection=FlexDirection.Row;
            foreach(var tabName in Theme.BottomTabs){var tab=new ThemeButton("",()=>Actions++);var icon=new Image{vectorImage=arrow.vectorImage};ThemeStyles.BottomTab(tab,icon,tabName=="HOME");tab.style.paddingLeft=tab.style.paddingRight=0;tab.style.alignItems=Align.Center;tab.Add(icon);var text=new Label(tabName);ThemeStyles.Text(text,TextRole.NavigationLabel);tab.Add(text);navigation.Add(tab);}content.Add(navigation);
        }
    }
}
