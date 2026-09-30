using System;
using System.Collections.Generic;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    public enum ShellTab { Home, Puzzles, Learn, Watch, Menu }

    public abstract class ProductionRootPage : ScrollView
    {
        public VisualElement Body { get; }
        protected ProductionRootPage(string title, string subtitle) : base(ScrollViewMode.Vertical)
        {
            name = GetType().Name;
            style.flexGrow = 1; style.minHeight = 0;
            horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            Body = new VisualElement(); ThemeStyles.Page(Body); Add(Body);
            var heading = new Label(title); ThemeStyles.Text(heading, TextRole.PageTitle);
            heading.style.whiteSpace = WhiteSpace.Normal; Body.Add(heading);
            var detail = new Label(subtitle); ThemeStyles.Text(detail, TextRole.Secondary); Body.Add(detail);
        }
    }
    public sealed class ProductionPuzzlesPage : ProductionRootPage { public ProductionPuzzlesPage() : base("Puzzles", "Coming Soon") {} }
    public sealed class ProductionLearnPage : ProductionRootPage { public ProductionLearnPage() : base("Learn", "Coming Soon") {} }
    public sealed class ProductionWatchPage : ProductionRootPage { public ProductionWatchPage() : base("Watch", "Coming Soon") {} }
    // One retained page host. No authentication, backend or gameplay routes.
    public sealed class ProductionAppShell : VisualElement
    {
        public AppTheme Theme => ThemeProvider.Current;
        public ShellTab ActiveTab { get; private set; }
        public VisualElement PageHost { get; } = new VisualElement { name = "PageHost" };
        public VisualElement BottomNavigation { get; } = new VisualElement { name = "BottomNavigation" };
        public IReadOnlyList<ProductionRootPage> Pages => pages;
        public bool HasSubpage => detail != null;
        public MenuDestination? ActiveMenuDestination { get; private set; }
        public ProfileSection ActiveProfileSection { get; private set; }
        public IProfileDataSource ProfileDataSource { get; }
        readonly ProductionRootPage[] pages;
        readonly ThemeButton[] tabs = new ThemeButton[5];
        readonly Image[] icons = new Image[5];
        readonly Label[] labels = new Label[5];
        VisualElement detail;

        public ProductionAppShell(IProfileDataSource profileDataSource=null)
        {
            ProfileDataSource=profileDataSource??new DemoProfileDataSource();
            name = "ProductionAppShell"; style.flexGrow = 1; style.minHeight = 0;
            style.backgroundColor = Theme.Colors.Background;
            PageHost.style.flexGrow = 1; PageHost.style.minHeight = 0;
            PageHost.style.overflow = Overflow.Hidden; Add(PageHost);
            BottomNavigation.style.flexDirection = FlexDirection.Row;
            BottomNavigation.style.height = BottomNavigation.style.minHeight = Theme.Sizing.BottomTabTouchHeight;
            BottomNavigation.style.flexShrink = 0; BottomNavigation.style.backgroundColor = Theme.Colors.Surface;
            Add(BottomNavigation);
            pages = new ProductionRootPage[] { new ProductionHomePage(new DemoHomeDataSource(), () => Select(ShellTab.Learn)), new ProductionPuzzlesPage(), new ProductionLearnPage(), new ProductionWatchPage(), new ProductionMenuPage(new DemoMenuDataSource(), OpenMenuDestination) };
            for (int i = 0; i < 5; i++)
            {
                int target = i; PageHost.Add(pages[i]);
                var tab = tabs[i] = new ThemeButton("", () => Select((ShellTab)target)) { name = "Tab" + (ShellTab)i };
                var icon = icons[i] = new Image { vectorImage = Resources.Load<VectorImage>("AppShellMockIcons/icon_nav_" + ((ShellTab)i).ToString().ToLowerInvariant()) };
                ThemeStyles.BottomTab(tab, icon, false);
                tab.style.height = Theme.Sizing.BottomTabTouchHeight; tab.style.marginBottom = 0;
                tab.style.paddingLeft = tab.style.paddingRight = 0; tab.style.alignItems = Align.Center;
                tab.style.justifyContent = Justify.Center; tab.Add(icon);
                var label = labels[i] = new Label(((ShellTab)i).ToString()) { pickingMode = PickingMode.Ignore };
                ThemeStyles.Text(label, TextRole.NavigationLabel); tab.Add(label); BottomNavigation.Add(tab);
            }
            Select(ShellTab.Home);
        }

        public void Select(ShellTab tab)
        {
            if ((int)tab < 0 || (int)tab >= pages.Length) throw new ArgumentOutOfRangeException(nameof(tab));
            CloseDetail(); ActiveTab = tab;
            for (int i = 0; i < pages.Length; i++)
            {
                bool active = i == (int)tab;
                pages[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                tabs[i].Selected = active;
                icons[i].tintColor = active ? Theme.Colors.IconActive : Theme.Colors.IconInactive;
                labels[i].style.color = active ? Theme.Colors.TextPrimary : Theme.Colors.TextSecondary;
            }
        }

        public void OpenDetail() => OpenSubpage("Shell Test Detail",null);
        public void OpenMenuDestination(MenuDestination destination)
        {
            if(destination==MenuDestination.Profile){if(!HasSubpage && ActiveTab==ShellTab.Menu)OpenProfileSection(ProfileSection.Profile);return;}
            OpenSubpage(ProductionMenuPage.Title(destination),destination);
        }
        public void OpenProfileSection(ProfileSection section)
        {
            if(ActiveTab!=ShellTab.Menu || (HasSubpage && ActiveMenuDestination!=MenuDestination.Profile))return;
            CloseDetail();ActiveMenuDestination=MenuDestination.Profile;ActiveProfileSection=section;
            pages[(int)ActiveTab].style.display=DisplayStyle.None;
            detail=new ProductionProfilePage(ProfileDataSource,section,Back,OpenProfileSection);PageHost.Add(detail);
        }
        void OpenSubpage(string pageTitle,MenuDestination? destination)
        {
            if (HasSubpage || ActiveTab != ShellTab.Menu) return;
            pages[(int)ActiveTab].style.display = DisplayStyle.None;
            ActiveMenuDestination=destination;
            detail = new VisualElement { name = "ShellTestDetail" }; detail.style.flexGrow = 1;
            ThemeStyles.Page(detail);
            var header = new VisualElement(); header.style.flexDirection = FlexDirection.Row;
            var back = new Button(Back) { name = "ShellBack" }; var arrow = new Image();
            ThemeStyles.Back(back, arrow); back.Add(arrow); header.Add(back);
            var title = new Label(pageTitle){name="SubpageTitle"}; ThemeStyles.Text(title, TextRole.SectionTitle); header.Add(title);
            detail.Add(header);
            var message=new Label("Coming Soon"){name="SubpageMessage"};ThemeStyles.Text(message,TextRole.Secondary);detail.Add(message);
            PageHost.Add(detail);
        }
        void CloseDetail() { detail?.RemoveFromHierarchy(); detail = null; ActiveMenuDestination=null; }
        public void Back() { if (!HasSubpage) return; if(ActiveMenuDestination==MenuDestination.Profile && ActiveProfileSection!=ProfileSection.Profile){OpenProfileSection(ProfileSection.Profile);return;} CloseDetail(); pages[(int)ActiveTab].style.display = DisplayStyle.Flex; }

        // Insets are logical panel units; caller converts Screen.safeArea from pixels.
        public void SetSafeArea(float left, float top, float right, float bottom)
        {
            style.paddingLeft = Mathf.Max(0, left); style.paddingTop = Mathf.Max(0, top);
            style.paddingRight = Mathf.Max(0, right); style.paddingBottom = Mathf.Max(0, bottom);
        }
    }
}
