using System;
using System.Linq;
using Domino.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.AppShellMock
{
    // No ApplicationServices, network, billing, contacts, Firebase or PlayerPrefs access.
    public sealed partial class MockShellView : VisualElement
    {
        public readonly MockShellState State;
        public readonly bool DevelopmentControls;
        VisualElement body,footer; Label error,headerTitle;
        public MockShellView(MockShellState state,bool developmentControls=false)
        {
            State=state;DevelopmentControls=developmentControls;
            style.flexGrow=1;style.backgroundColor=MockShellTheme.Background;style.color=MockShellTheme.Text;
            style.unityFont=UiKit.Font;style.fontSize=15;Rebuild();
        }
        static Color Surface=>MockShellTheme.Surface;
        static Color Primary=>MockShellTheme.Primary;
        static void Pad(VisualElement e,int n){e.style.paddingLeft=n;e.style.paddingRight=n;e.style.paddingTop=n;e.style.paddingBottom=n;}
        static void Round(VisualElement e,int n){e.style.borderTopLeftRadius=n;e.style.borderTopRightRadius=n;e.style.borderBottomLeftRadius=n;e.style.borderBottomRightRadius=n;}
        public void Rebuild()
        {
            Clear();
            name=State.Page==MockPage.Welcome?"WelcomeAuthRoot":"AppShellMockRoot";
            var banner=new Label("CUBAN DOMINO CLUB  /  VISUAL DEMO");banner.style.fontSize=10;banner.style.letterSpacing=1;banner.style.color=Primary;Pad(banner,12);if(State.Page!=MockPage.Welcome&&State.Page!=MockPage.Membership&&State.Page!=MockPage.Trial&&State.Page!=MockPage.Plans)Add(banner);
            headerTitle=null;
            if(State.Page!=MockPage.Welcome && State.Page!=MockPage.Experience && !IsRoot())
            {
                var header=new VisualElement { name="MockSubpageHeader" };
                header.style.flexDirection=FlexDirection.Row;header.style.alignItems=Align.Center;header.style.flexShrink=0;
                header.style.marginLeft=12;header.style.marginRight=12;
                var back=new Button(()=>{State.Back();Rebuild();}){name="BackAction",tooltip="Back"};
                back.style.width=48;back.style.height=48;back.style.flexShrink=0;
                back.style.marginLeft=0;back.style.marginRight=0;back.style.backgroundColor=Color.clear;
                back.style.borderTopWidth=back.style.borderBottomWidth=back.style.borderLeftWidth=back.style.borderRightWidth=0;
                back.style.alignItems=Align.Center;back.style.justifyContent=Justify.Center;
                var arrow=new Image { name="BackIcon",pickingMode=PickingMode.Ignore };
                arrow.vectorImage=Resources.Load<VectorImage>("AppShellMockIcons/icon_arrow_left");
                arrow.tintColor=MockShellTheme.Text;arrow.style.width=26;arrow.style.height=26;
                back.Add(arrow);header.Add(back);
                headerTitle=new Label { name="SubpageTitle" };
                headerTitle.style.flexGrow=1;headerTitle.style.flexShrink=1;headerTitle.style.minWidth=0;
                headerTitle.style.whiteSpace=WhiteSpace.Normal;headerTitle.style.fontSize=20;
                headerTitle.style.unityFontStyleAndWeight=FontStyle.Bold;headerTitle.style.color=MockShellTheme.Text;
                headerTitle.style.unityTextAlign=TextAnchor.MiddleCenter;header.Add(headerTitle);
                var balance=new VisualElement();balance.style.width=48;balance.style.flexShrink=0;header.Add(balance);Add(header);
            }
            var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.style.flexGrow=1;scroll.style.flexShrink=1;scroll.style.minHeight=0;scroll.horizontalScrollerVisibility=ScrollerVisibility.Hidden;Add(scroll);
            body=scroll.contentContainer;Pad(body,20);body.style.paddingTop=10;body.style.paddingBottom=28;
            if(State.Page==MockPage.Welcome)
            {
                var canvas=scroll.contentContainer;Pad(canvas,0);canvas.style.justifyContent=Justify.Center;canvas.style.alignItems=Align.Center;
                scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(e=>canvas.style.minHeight=e.newRect.height);
                body=new VisualElement {name="WelcomeContent"};body.style.width=Length.Percent(100);body.style.maxWidth=620;body.style.flexShrink=0;Pad(body,24);canvas.Add(body);
            }
            Render();
            error=new Label(State.Error);error.style.whiteSpace=WhiteSpace.Normal;error.style.color=new Color(1,.68f,.58f);error.style.marginTop=8;body.Add(error);
            if(!string.IsNullOrEmpty(State.Notice))Text(State.Notice,13);
            if(State.ShellVisible)Nav();
        }
        bool IsRoot()=>State.Page==MockPage.Home||State.Page==MockPage.Menu||State.Page==MockPage.Puzzles||State.Page==MockPage.Watch||State.Page==MockPage.Learn;
        void Act(Action a){a();Rebuild();}
        Label Text(string value,int size=15,VisualElement parent=null)
        {var l=new Label(value);l.style.whiteSpace=WhiteSpace.Normal;l.style.fontSize=size;l.style.marginBottom=10;l.style.color=MockShellTheme.SecondaryText;(parent??body).Add(l);return l;}
        void Title(string eyebrow,string title,string description="")
        {var k=Text(eyebrow.ToUpperInvariant(),11);k.style.color=Primary;k.style.letterSpacing=2;if(headerTitle!=null)headerTitle.text=title;else {var t=Text(title,29);t.style.color=MockShellTheme.Text;t.style.unityFontStyleAndWeight=FontStyle.Bold;t.style.marginBottom=16;}if(description!="")Text(description);}
        Button Button(string title,Action action,bool primary=false)
        {var b=new Button(action){text=title};b.name=title;b.style.minHeight=48;b.style.marginBottom=9;b.style.marginLeft=0;b.style.marginRight=0;b.style.paddingLeft=14;b.style.paddingRight=14;b.style.whiteSpace=WhiteSpace.Normal;b.style.fontSize=15;MockButtonTypography.Apply(b,primary);b.style.backgroundColor=primary?Primary:Surface;b.style.color=MockShellTheme.Text;Round(b,12);b.style.borderTopWidth=0;b.style.borderBottomWidth=0;b.style.borderLeftWidth=0;b.style.borderRightWidth=0;return b;}
        Button Action(string title,Action action,bool primary=false,bool enabled=true,VisualElement parent=null)
        {var b=Button(title,()=>Act(action),primary);b.SetEnabled(enabled);(parent??body).Add(b);return b;}
        VisualElement Card(string title,string detail="")
        {var c=new VisualElement();c.style.backgroundColor=Surface;Round(c,16);Pad(c,16);c.style.marginBottom=12;body.Add(c);if(title!=""){var l=Text(title,19,c);l.style.color=MockShellTheme.Text;l.style.unityFontStyleAndWeight=FontStyle.Bold;}if(detail!="")Text(detail,14,c);return c;}
        TextField Field(string label,bool password=false,string hint="")
        {Text(label,12);var f=new TextField();f.isPasswordField=password;f.style.height=48;f.style.marginBottom=14;f.style.fontSize=17;MockShellTheme.StyleInput(f);body.Add(f);if(hint!="")Text(hint,11);return f;}
        void Submit(string label,Action run){body.Add(Button(label,()=>{run();if(error!=null)error.text=State.Error;},true));}
        void Providers()
        {foreach(var p in new[]{"Google","Facebook","Email","Phone"}){string provider=p;Action("Continue with "+p,()=>State.BeginProvider(provider),p=="Google");}}
        void Progress(int step){Text("ONBOARDING   "+step+" / 4",11);var track=new VisualElement();track.style.flexDirection=FlexDirection.Row;track.style.marginBottom=20;for(int i=0;i<4;i++){var part=new VisualElement();part.style.height=3;part.style.flexGrow=1;part.style.marginRight=5;part.style.backgroundColor=i<step?Primary:Surface;track.Add(part);}body.Add(track);}
        void Select(string text,bool selected,Action action,VisualElement parent=null){var b=Action((selected?"✓  ":"○  ")+text,action,false,true,parent);MockShellTheme.Selection(b,selected);}
        Image MenuIcon(string asset,string name,int size=24)
        {
            var icon=new Image {name=name,pickingMode=PickingMode.Ignore};
            icon.vectorImage=Resources.Load<VectorImage>("AppShellMockIcons/icon_menu_"+asset);
            icon.tintColor=MockShellTheme.Inactive;icon.style.width=size;icon.style.height=size;icon.style.flexShrink=0;return icon;
        }
        void WelcomeAuthRow(string provider,string description,string asset,Action action,bool primary=false)
        {
            string title=provider=="Guest"?"Continue as Guest":"Continue with "+provider;
            var row=Button(title,()=>Act(action),primary);row.text="";row.name=title;row.tooltip=title;row.AddToClassList("welcome-auth-row");
            row.style.height=56;row.style.minHeight=56;row.style.flexShrink=0;row.style.flexDirection=FlexDirection.Row;row.style.alignItems=Align.Center;row.style.paddingLeft=12;row.style.paddingRight=12;row.style.paddingTop=6;row.style.paddingBottom=6;row.style.marginBottom=8;
            row.style.borderTopLeftRadius=row.style.borderTopRightRadius=row.style.borderBottomLeftRadius=row.style.borderBottomRightRadius=12;
            var area=new VisualElement {name="AuthIconArea",pickingMode=PickingMode.Ignore};area.style.width=34;area.style.height=34;area.style.flexShrink=0;area.style.alignItems=Align.Center;area.style.justifyContent=Justify.Center;
            area.style.borderTopLeftRadius=area.style.borderTopRightRadius=area.style.borderBottomLeftRadius=area.style.borderBottomRightRadius=17;
            area.style.backgroundColor=provider=="Google"?MockShellTheme.Text:provider=="Facebook"?new Color32(24,119,242,255):new Color(0,0,0,.15f);
            var icon=PremiumIcon("icon_auth_"+asset,22,provider=="Phone"?Primary:provider=="Facebook"||provider=="Google"?Color.white:MockShellTheme.Text);icon.name="AuthProviderIcon";area.Add(icon);row.Add(area);
            var divider=new VisualElement {name="AuthDivider",pickingMode=PickingMode.Ignore};divider.style.width=1;divider.style.height=26;divider.style.flexShrink=0;divider.style.marginLeft=10;divider.style.marginRight=10;divider.style.backgroundColor=new Color(1,1,1,primary?.25f:.12f);row.Add(divider);
            var text=new VisualElement {pickingMode=PickingMode.Ignore};text.style.flexGrow=1;text.style.flexShrink=1;text.style.minWidth=0;row.Add(text);
            var label=new Label(title){name="AuthPrimaryLabel",pickingMode=PickingMode.Ignore};label.style.fontSize=15;label.style.color=MockShellTheme.Text;label.style.whiteSpace=WhiteSpace.Normal;label.style.unityTextAlign=TextAnchor.MiddleLeft;PremiumFont(label,"Bold");text.Add(label);
            var secondary=new Label(description){name="AuthSecondaryLabel",pickingMode=PickingMode.Ignore};secondary.style.fontSize=11;secondary.style.marginTop=0;secondary.style.whiteSpace=WhiteSpace.Normal;secondary.style.unityTextAlign=TextAnchor.MiddleLeft;secondary.style.color=primary?MockShellTheme.Text:MockShellTheme.Inactive;PremiumFont(secondary,"Medium");text.Add(secondary);
            var chevron=PremiumIcon("icon_menu_chevron",14,primary?MockShellTheme.Text:MockShellTheme.Inactive);chevron.name="AuthChevron";chevron.style.marginLeft=6;row.Add(chevron);body.Add(row);
        }
        void WelcomeScreen()
        {
            var eyebrow=Text("A SEAT AT THE TABLE",11);eyebrow.style.color=Primary;eyebrow.style.letterSpacing=2;eyebrow.style.marginBottom=12;PremiumFont(eyebrow,"Semibold");
            var title=Text("CUBAN DOMINO CLUB",29);title.name="WelcomeTitle";title.style.color=MockShellTheme.Text;PremiumFont(title,"Bold");title.style.marginBottom=12;
            var subtitle=Text("Tu pr\u00F3xima partida empieza aqu\u00ED.",16);subtitle.style.marginBottom=8;subtitle.style.color=MockShellTheme.Text;PremiumFont(subtitle,"Medium");
            var intro=Text("Tradici\u00F3n. Estrategia. Comunidad.",13);intro.style.color=MockShellTheme.Inactive;intro.style.marginBottom=24;
            WelcomeAuthRow("Google","Fast, secure and easy","google",()=>State.BeginProvider("Google"),true);
            WelcomeAuthRow("Facebook","Play with your friends","facebook",()=>State.BeginProvider("Facebook"));
            WelcomeAuthRow("Email","Use your email and password","email",()=>State.BeginProvider("Email"));
            WelcomeAuthRow("Phone","Sign in with your phone number","phone",()=>State.BeginProvider("Phone"));
            var separator=new VisualElement();separator.style.flexDirection=FlexDirection.Row;separator.style.alignItems=Align.Center;separator.style.marginTop=7;separator.style.marginBottom=16;body.Add(separator);
            var left=new VisualElement();left.style.height=1;left.style.flexGrow=1;left.style.backgroundColor=Surface;separator.Add(left);
            var or=Text("or",12,separator);or.style.color=MockShellTheme.Inactive;or.style.marginBottom=0;or.style.marginLeft=12;or.style.marginRight=12;
            var right=new VisualElement();right.style.height=1;right.style.flexGrow=1;right.style.backgroundColor=Surface;separator.Add(right);
            WelcomeAuthRow("Guest","Play now, create an account later","guest",State.Guest);
            var signin=new VisualElement {name="WelcomeSignInRow"};signin.style.flexDirection=FlexDirection.Row;signin.style.justifyContent=Justify.Center;signin.style.alignItems=Align.Center;signin.style.marginTop=8;body.Add(signin);
            var prompt=Text("Already have an account?",13,signin);prompt.style.color=MockShellTheme.Inactive;prompt.style.marginBottom=0;
            var link=Button("Sign In",()=>Act(()=>{State.SignInFlow=true;State.Go(MockPage.SignIn);}));link.style.backgroundColor=Color.clear;link.style.color=Primary;link.style.paddingLeft=8;link.style.paddingRight=0;link.style.marginBottom=0;link.style.fontSize=13;PremiumFont(link,"Bold");signin.Add(link);
        }
        void MenuScreen()
        {
            var title=Text("MENU",24);title.style.color=MockShellTheme.Text;
            var profile=new Button(()=>Act(()=>State.OpenProfile(MockProfileMode.OWN_PROFILE))) {name="MenuProfile",tooltip="Open Profile"};profile.style.backgroundColor=Color.clear;profile.style.borderTopWidth=profile.style.borderBottomWidth=profile.style.borderLeftWidth=profile.style.borderRightWidth=0;profile.style.paddingLeft=0;profile.style.paddingRight=0;profile.style.flexDirection=FlexDirection.Row;profile.style.alignItems=Align.Center;profile.style.marginBottom=12;body.Add(profile);
            var avatar=new VisualElement {name="FutureAvatar",tooltip="Demo avatar placeholder"};avatar.style.width=48;avatar.style.height=48;avatar.style.marginRight=14;avatar.style.backgroundColor=Surface;Round(avatar,24);avatar.style.alignItems=Align.Center;avatar.style.justifyContent=Justify.Center;avatar.Add(MenuIcon("avatar","ProfileIcon",28));profile.Add(avatar);
            var info=new VisualElement();info.style.flexGrow=1;info.style.minWidth=0;profile.Add(info);
            var name=Text(State.AccountType=="GUEST"?"Guest player":"Alex · Demo player",17,info);name.style.color=MockShellTheme.Text;name.style.marginBottom=4;
            var plan=Text("FREE · Cuban Domino Club",13,info);plan.style.color=MockShellTheme.Inactive;plan.style.marginBottom=0;
            foreach(var child in profile.Query<VisualElement>().ToList())if(child!=profile)child.pickingMode=PickingMode.Ignore;
            var routes=new[]{MockPage.Friends,MockPage.Messages,MockPage.Stats,MockPage.Coach,MockPage.Theme,MockPage.Membership,MockPage.Settings,MockPage.Support};
            var groups=new[]{"SOCIAL","ACTIVITY","PERSONALIZATION","APP"};
            for(int i=0;i<routes.Length;i++)
            {
                if(i%2==0){var group=Text(groups[i/2],10);group.style.color=Primary;group.style.letterSpacing=1;group.style.marginTop=8;group.style.marginBottom=6;}
                var route=routes[i];string label=route==MockPage.Support?"Help & Support":route.ToString();
                var row=Button(label,()=>Act(()=>State.Go(route)));row.text="";row.name="MenuRow"+route;row.tooltip=label;
                row.style.height=56;row.style.minHeight=56;row.style.flexShrink=0;row.style.marginBottom=2;Pad(row,16);Round(row,8);
                row.style.flexDirection=FlexDirection.Row;row.style.alignItems=Align.Center;
                var icon=MenuIcon(route.ToString().ToLowerInvariant(),"MenuRowIcon");icon.style.marginRight=14;row.Add(icon);
                var text=new Label(label){name="MenuRowLabel",pickingMode=PickingMode.Ignore};text.style.color=MockShellTheme.Text;text.style.fontSize=16;text.style.flexGrow=1;text.style.flexShrink=1;text.style.minWidth=0;text.style.unityTextAlign=TextAnchor.MiddleLeft;row.Add(text);
                var chevron=MenuIcon("chevron","MenuRowChevron",18);chevron.style.marginLeft=8;row.Add(chevron);body.Add(row);
            }
            if(State.AccountType=="GUEST")Action("Secure your account",State.Secure,true);
        }
        void PremiumFont(VisualElement element,string face)
        {
            var font=Resources.Load<Font>("AppShellMockFonts/SourceSans3-"+face);
            element.style.unityFont=font;element.style.unityFontDefinition=FontDefinition.FromFont(font);
            element.style.unityFontStyleAndWeight=FontStyle.Normal;
        }
        Image PremiumIcon(string asset,int size,Color tint)
        {
            var icon=new Image {pickingMode=PickingMode.Ignore};icon.vectorImage=Resources.Load<VectorImage>("AppShellMockIcons/"+asset);
            icon.tintColor=tint;icon.style.width=size;icon.style.height=size;icon.style.flexShrink=0;return icon;
        }
        void MembershipScreen(bool allPlans)
        {
            if(headerTitle!=null)headerTitle.text="";
            var title=Text("Get 1 Week of Premium\nfor Free",27);title.name="PremiumTitle";
            title.style.color=MockShellTheme.Text;title.style.unityTextAlign=TextAnchor.MiddleCenter;title.style.marginBottom=22;PremiumFont(title,"Bold");
            var tabs=new VisualElement {name="PremiumTabs"};tabs.style.flexDirection=FlexDirection.Row;tabs.style.marginBottom=18;body.Add(tabs);
            var plans=new[]{MockMembershipPlan.DIAMOND,MockMembershipPlan.PLATINUM,MockMembershipPlan.GOLD,MockMembershipPlan.FRIENDS_AND_FAMILY};
            var assets=new[]{"icon_menu_membership","icon_premium_crown","icon_premium_star","icon_premium_family"};
            for(int i=0;i<plans.Length;i++)
            {
                var plan=plans[i];bool active=State.selectedMembershipPlan==plan;
                var tab=Button(MockMembershipPricing.Name(plan),()=>Act(()=>State.selectedMembershipPlan=plan));tab.text="";tab.name="PremiumTab_"+plan;tab.tooltip=MockMembershipPricing.Name(plan);
                tab.style.width=Length.Percent(25);tab.style.minWidth=0;tab.style.height=94;tab.style.paddingLeft=2;tab.style.paddingRight=2;tab.style.backgroundColor=Color.clear;
                tab.style.alignItems=Align.Center;tab.style.borderBottomWidth=3;tab.style.borderBottomColor=active?Primary:Color.clear;Round(tab,0);
                var planColors=new[]{UiKit.Hex("42A5F5"),UiKit.Hex("C0C0C0"),UiKit.Hex("F4C542"),Primary};
                var icon=PremiumIcon(assets[i],28,planColors[i]);icon.style.marginBottom=8;tab.Add(icon);
                var label=new Label(plan==MockMembershipPlan.FRIENDS_AND_FAMILY?"FRIENDS &\nFAMILY":MockMembershipPricing.Name(plan)){pickingMode=PickingMode.Ignore};
                label.style.fontSize=11;label.style.whiteSpace=WhiteSpace.Normal;label.style.unityTextAlign=TextAnchor.MiddleCenter;label.style.color=active?MockShellTheme.Text:MockShellTheme.Inactive;tab.Add(label);tabs.Add(tab);
            }
            var panel=Card("");panel.name="PremiumFeatures";Pad(panel,14);
            foreach(var feature in MockMembershipPricing.Features(State.selectedMembershipPlan))
            {
                string asset=feature=="Game Review"?"icon_premium_review":feature=="Move Explanations"?"icon_premium_move":feature=="Advanced Stats"?"icon_menu_stats":feature=="Puzzles"?"icon_nav_puzzles":feature=="Lessons"?"icon_nav_learn":feature=="Coach Games"?"icon_menu_coach":feature=="Bots"?"icon_premium_robot":"icon_premium_noads";
                var row=new VisualElement {name="PremiumFeature"};row.style.flexDirection=FlexDirection.Row;row.style.alignItems=Align.Center;row.style.minHeight=40;panel.Add(row);
                var tint=feature=="Game Review"||feature=="Coach Games"?Primary:feature=="Move Explanations"?UiKit.Hex("56BDB5"):feature=="Advanced Stats"?UiKit.Hex("64A5DB"):feature=="Puzzles"?UiKit.Hex("D99B5B"):feature=="Lessons"?UiKit.Hex("63B4CF"):feature=="Bots"?UiKit.Hex("A6B9CB"):UiKit.Hex("CA8080");
                var icon=PremiumIcon(asset,22,tint);icon.style.marginRight=12;row.Add(icon);
                var label=new Label(feature);label.style.color=MockShellTheme.Text;label.style.fontSize=15;label.style.flexGrow=1;label.style.flexShrink=1;label.style.whiteSpace=WhiteSpace.Normal;PremiumFont(label,"Semibold");row.Add(label);
                var check=new VisualElement();check.style.width=22;check.style.height=22;check.style.flexShrink=0;check.style.marginLeft=8;check.style.backgroundColor=Primary;Round(check,11);check.style.alignItems=Align.Center;check.style.justifyContent=Justify.Center;check.Add(PremiumIcon("icon_premium_check",16,MockShellTheme.Text));row.Add(check);
            }
            var segments=new VisualElement {name="PremiumBilling"};segments.style.flexDirection=FlexDirection.Row;segments.style.height=52;segments.style.flexShrink=0;segments.style.backgroundColor=Surface;Round(segments,12);Pad(segments,4);segments.style.marginTop=6;segments.style.marginBottom=20;body.Add(segments);
            foreach(var period in new[]{MockBillingPeriod.YEARLY,MockBillingPeriod.MONTHLY})
            {
                bool active=State.selectedBillingPeriod==period;
                var segment=Button(period==MockBillingPeriod.YEARLY?"Yearly":"Monthly",()=>Act(()=>State.selectedBillingPeriod=period));segment.name="Billing_"+period;
                segment.style.flexGrow=1;segment.style.flexBasis=0;segment.style.height=44;segment.style.minHeight=44;segment.style.marginBottom=0;Round(segment,8);
                segment.style.backgroundColor=active?MockShellTheme.Background:Color.clear;segment.style.color=active?MockShellTheme.Text:MockShellTheme.Inactive;
                segment.style.borderTopWidth=segment.style.borderBottomWidth=segment.style.borderLeftWidth=segment.style.borderRightWidth=active?1:0;
                segment.style.borderTopColor=segment.style.borderBottomColor=segment.style.borderLeftColor=segment.style.borderRightColor=active?Primary:Color.clear;
                PremiumFont(segment,active?"Bold":"Semibold");segments.Add(segment);
            }
            var column=new VisualElement {name="PremiumBillingColumn"};body.Add(column);
            var selected=State.selectedMembershipPlan;bool yearly=State.selectedBillingPeriod==MockBillingPeriod.YEARLY;
            if(yearly){var save=Text("SAVE "+MockMembershipPricing.Money(MockMembershipPricing.Savings(selected))+" \u00B7 "+MockMembershipPricing.SavingsPercent(selected)+"%",17,column);save.name="PremiumSavings";save.style.color=Primary;PremiumFont(save,"Bold");}
            var price=Text(MockMembershipPricing.Money(yearly?MockMembershipPricing.MonthlyEquivalent(selected):MockMembershipPricing.Monthly(selected),yearly)+" / month",30,column);price.name="PremiumPrice";price.style.color=MockShellTheme.Text;PremiumFont(price,"Bold");
            var billing=Text(yearly?"billed annually at "+MockMembershipPricing.Price(selected,MockBillingPeriod.YEARLY):"billed monthly",13,column);billing.name="PremiumBillingExplanation";billing.style.color=MockShellTheme.Inactive;PremiumFont(billing,"Medium");
            var summary=new VisualElement {name="MembershipSummary"};summary.style.marginTop=12;column.Add(summary);
            var planLabel=Text(MockMembershipPricing.Name(selected)+" \u00B7 "+(yearly?"Yearly":"Monthly"),16,summary);planLabel.name="PremiumPlanSummary";planLabel.style.color=MockShellTheme.Text;PremiumFont(planLabel,"Semibold");
            var trial=Text("7 days free",15,summary);trial.style.color=Primary;PremiumFont(trial,"Medium");
            var charge=Text("Then "+MockMembershipPricing.Price(selected,State.selectedBillingPeriod),13,summary);charge.style.color=MockShellTheme.Inactive;PremiumFont(charge,"Medium");
            foreach(var label in column.Query<Label>().ToList())label.style.unityTextAlign=TextAnchor.MiddleCenter;
            Action("Try 7 Days for $0",()=>State.Finish(true),true);
            if(!State.OnboardingCompleted)Action("Not Now",()=>State.Finish(false));
        }
        void ContactResultsScreen()
        {
            Progress(3);if(headerTitle!=null)headerTitle.text="Friends at your table";
            var row=new VisualElement();row.style.flexDirection=FlexDirection.Row;row.style.alignItems=Align.Center;row.style.justifyContent=Justify.SpaceBetween;body.Add(row);
            var eyebrow=Text("MOCK CONTACTS",11,row);eyebrow.style.color=Primary;eyebrow.style.letterSpacing=2;eyebrow.style.marginBottom=0;
            var toggle=Button(State.ContactResultsToggleLabel,()=>Act(State.ToggleAllContactResults));
            toggle.name="ContactResultsToggleAll";toggle.style.backgroundColor=Color.clear;toggle.style.color=Primary;toggle.style.marginBottom=0;toggle.style.paddingRight=0;row.Add(toggle);
            Text("Select one or more fictional players.");
            foreach(var friend in MockCatalog.Friends)
            {
                string name=friend;bool selected=State.MockFriendSelections.Contains(name);
                var card=Button(name,()=>Act(()=>State.ToggleContactResult(name)));card.text="";card.tooltip=name;
                card.style.flexDirection=FlexDirection.Row;card.style.alignItems=Align.Center;MockShellTheme.Selection(card,selected);
                var label=new Label(name){pickingMode=PickingMode.Ignore};label.style.flexGrow=1;label.style.unityTextAlign=TextAnchor.MiddleLeft;card.Add(label);
                var check=new Label(selected?"✓":""){pickingMode=PickingMode.Ignore};check.style.width=24;check.style.color=Primary;check.style.unityTextAlign=TextAnchor.MiddleCenter;card.Add(check);body.Add(card);
            }
            Action(State.ContactResultsAddLabel,()=>State.Contacts(true),true,State.CanAddContactResults);Action("Skip",()=>State.Contacts(false));
        }
        void ExperienceScreen()
        {
            Progress(1);
            var eyebrow=Text("TU PUNTO DE PARTIDA",11);eyebrow.style.color=Primary;
            eyebrow.style.letterSpacing=2;eyebrow.style.marginBottom=18;
            var title=Text("¿Cuál es tu nivel de\nexperiencia en dominó?",27);
            title.style.color=MockShellTheme.Text;title.style.marginBottom=24;
            var bold=Resources.Load<Font>("AppShellMockFonts/SourceSans3-Bold");
            title.style.unityFont=bold;title.style.unityFontDefinition=FontDefinition.FromFont(bold);
            string[] supporting={"Quiero aprender desde el principio","Sé cómo jugar y quiero practicar","Quiero mejorar mi juego","Juego de forma competitiva"};
            var medium=Resources.Load<Font>("AppShellMockFonts/SourceSans3-Medium");
            for(int i=0;i<4;i++)
            {
                int level=i;bool selected=State.ExperienceLevel==i;
                var card=Button(MockCatalog.Experience[i],()=>Act(()=>State.ExperienceLevel=level));
                card.text="";card.name="ExperienceOption_"+i;card.tooltip=MockCatalog.Experience[i];
                card.style.minHeight=104;card.style.marginBottom=12;Pad(card,16);Round(card,16);
                card.style.flexDirection=FlexDirection.Row;card.style.alignItems=Align.Center;
                card.style.borderTopWidth=card.style.borderBottomWidth=card.style.borderLeftWidth=card.style.borderRightWidth=2;
                var border=selected?Primary:Surface;
                card.style.borderTopColor=card.style.borderBottomColor=card.style.borderLeftColor=card.style.borderRightColor=border;
                var content=new VisualElement {pickingMode=PickingMode.Ignore};content.style.flexGrow=1;content.style.flexShrink=1;content.style.minWidth=0;
                var label=new Label(MockCatalog.Experience[i]) {pickingMode=PickingMode.Ignore};
                label.style.color=MockShellTheme.Text;label.style.fontSize=17;label.style.whiteSpace=WhiteSpace.Normal;
                label.style.unityTextAlign=TextAnchor.MiddleLeft;content.Add(label);
                var detail=new Label(supporting[i]) {pickingMode=PickingMode.Ignore};
                detail.style.color=MockShellTheme.Inactive;detail.style.fontSize=13;detail.style.marginTop=6;
                detail.style.whiteSpace=WhiteSpace.Normal;detail.style.unityTextAlign=TextAnchor.MiddleLeft;
                detail.style.unityFont=medium;detail.style.unityFontDefinition=FontDefinition.FromFont(medium);content.Add(detail);
                card.Add(content);
                var check=new Label(selected?"✓":"") {pickingMode=PickingMode.Ignore};
                check.style.color=Primary;check.style.fontSize=22;check.style.width=24;check.style.flexShrink=0;check.style.marginLeft=12;
                check.style.unityTextAlign=TextAnchor.MiddleCenter;card.Add(check);body.Add(card);
            }
            var next=Button("Continuar",()=>Act(()=>State.ContinueExperience()),true);
            next.SetEnabled(State.ExperienceLevel>=0);next.style.marginTop=16;body.Add(next);
        }
        void Avatar(MockCoach coach,VisualElement parent)
        {
            var avatar=new VisualElement { name="CoachPortrait",pickingMode=PickingMode.Ignore };
            avatar.style.width=Length.Percent(100);avatar.style.maxWidth=120;avatar.style.height=120;
            avatar.style.flexShrink=0;avatar.style.marginBottom=10;avatar.style.alignSelf=Align.Center;
            avatar.style.overflow=Overflow.Hidden;Round(avatar,999);
            avatar.RegisterCallback<GeometryChangedEvent>(e=>{if(Mathf.Abs(e.newRect.width-e.newRect.height)>.5f)avatar.style.height=e.newRect.width;});
            var image=new Image {name="PortraitImage",pickingMode=PickingMode.Ignore};
            image.image=Resources.Load<Texture2D>("AppShellMockCoaches/coach_"+coach.Id);
            image.scaleMode=ScaleMode.ScaleAndCrop;image.style.width=Length.Percent(100);image.style.height=Length.Percent(100);
            avatar.Add(image);parent.Add(avatar);
        }
        void CoachSelectionGrid()
        {
            var grid=new VisualElement {name="CoachGrid"};grid.style.flexDirection=FlexDirection.Row;
            grid.style.flexWrap=Wrap.Wrap;grid.style.justifyContent=Justify.SpaceBetween;body.Add(grid);
            var greeting=Text(State.SelectedCoach?.ShortGreeting??"",17);greeting.name="CoachGreeting";
            var next=Button("Continue",()=>Act(()=>State.ContinueCoach()),true);next.SetEnabled(State.SelectedCoach!=null);
            foreach(var coach in MockCatalog.Coaches)
            {
                var target=coach;
                var tile=Button(coach.DisplayName,()=>{});tile.text="";tile.name="CoachCard_"+coach.Id;
                tile.tooltip="Select "+coach.DisplayName;tile.style.width=Length.Percent(48);
                tile.style.minWidth=0;tile.style.flexShrink=0;tile.style.marginBottom=12;Pad(tile,10);Round(tile,16);
                Avatar(coach,tile);
                var label=new Label(coach.DisplayName){name="CoachName",pickingMode=PickingMode.Ignore};
                label.style.color=MockShellTheme.Text;label.style.fontSize=17;label.style.unityTextAlign=TextAnchor.MiddleCenter;
                label.style.whiteSpace=WhiteSpace.Normal;tile.Add(label);grid.Add(tile);
                StyleCoachCard(tile,State.SelectedCoachId==coach.Id);
                tile.clicked+=()=>
                {
                    State.SelectedCoachId=target.Id;
                    foreach(var card in grid.Children())StyleCoachCard(card,card.name=="CoachCard_"+target.Id);
                    greeting.text=target.ShortGreeting;next.SetEnabled(true);
                };
            }
            body.Add(next);
        }
        static void StyleCoachCard(VisualElement card,bool selected)
        {
            card.style.backgroundColor=MockShellTheme.Surface;
            card.style.borderTopWidth=card.style.borderBottomWidth=card.style.borderLeftWidth=card.style.borderRightWidth=3;
            var color=selected?MockShellTheme.Primary:MockShellTheme.Surface;
            card.style.borderTopColor=card.style.borderBottomColor=card.style.borderLeftColor=card.style.borderRightColor=color;
        }
        void CoachCard(MockCoach coach){var c=Card(coach.DisplayName,coach.ShortGreeting);Avatar(coach,c);}
        void Render()
        {
            switch(State.Page)
            {
                case MockPage.Welcome:WelcomeScreen();break;
                case MockPage.SignIn:Title("Welcome back","Sign In","Demo only. Use fictional details.");Providers();break;
                case MockPage.SecureAccount:Title("Your progress, with you","Secure your account","Keep your games and progress and access your account from another device.");Providers();Action("Not Now",State.Back);break;
                case MockPage.Provider:
                    Title("Provider preview",State.Provider+" account selected","Simulated selection. No provider connection or manual email required.");
                    Action(State.SignInFlow?"Continue to my demo account":"Continue with selected account",()=>State.ProviderResult(State.SignInFlow?"EXISTING_ACCOUNT":"NEW_ACCOUNT"),true);
                    if(DevelopmentControls){Text("DEVELOPMENT SCENARIOS",11);Action("Simulate existing account",()=>State.ProviderResult("EXISTING_ACCOUNT"));Action("Simulate provider already used",()=>State.ProviderResult("PROVIDER_ALREADY_USED"));}break;
                case MockPage.ProviderConflict:Title("Account already connected","Use another account","This account is already connected to another Cuban Domino Club account. Accounts will not be merged.");Action("Use another account",State.Back,true);Action("Cancel",()=>{if(State.AccountType=="GUEST")State.Tab("Home");else State.Back();});break;
                case MockPage.EmailRegister:
                    Title("Join the club","Create Account","Visual demo — do not enter a real password.");var email=Field("Email",false,"Existing-account scenario: existing@example.test");var pass=Field("Password",true);var confirm=Field("Confirm Password",true);
                    Submit("Continue",()=>{if(State.Register(email.value,pass.value,confirm.value)||State.Page!=MockPage.EmailRegister)Rebuild();});break;
                case MockPage.EmailSignIn:
                    Title("Good to see you","Email Sign In","Visual demo — no credentials are sent or saved.");var loginEmail=Field("Email");var loginPass=Field("Password",true);Submit("Sign In",()=>{if(State.EmailLogin(loginEmail.value,loginPass.value))Rebuild();});Action("Forgot Password",()=>State.Go(MockPage.SendEmail));break;
                case MockPage.ExistingEmail:Title("Your account","This email is already registered.","Accounts stay separate. Guest progress is never merged.");Action("Use another email",State.Back);Action("This is my account",()=>State.Go(MockPage.SendEmail),true);break;
                case MockPage.SendEmail:Title("Confirm ownership","This is my account","We'll send an email to confirm this account belongs to you. This preview does not send email.");Action("Send Email",()=>State.Go(MockPage.CheckEmail),true);break;
                case MockPage.CheckEmail:Title("Demo email sent","Check your email","No real email was sent. Use the development confirmation to preview the next step.");Action("Resend",()=>State.Notice="Demo email resent. Nothing was sent externally.");Action("Use another email",()=>State.Go(MockPage.EmailRegister));if(DevelopmentControls)Action("Simulate Email Confirmation",State.ConfirmEmail,true);break;
                case MockPage.LoadAccount:Title("Account confirmed","Load your account?","Continuing will leave the current Guest account and load the existing Cuban Domino Club account. Guest progress will NOT be merged.");Action("Load My Account",State.LoadExisting,true);Action("Cancel",()=>{if(State.AccountType=="GUEST")State.Tab("Home");else State.Back();});break;
                case MockPage.Phone:
                    Title("Your number","Continue with Phone","No SMS or phone data will be sent.");var country=new DropdownField("Country",new System.Collections.Generic.List<string>{"US +1","Cuba +53","Spain +34","Mexico +52"},0);country.style.minHeight=44;MockShellTheme.StyleInput(country);body.Add(country);var phone=Field("Phone number");Submit("Continue",()=>{if(State.PhoneContinue(phone.value))Rebuild();});if(DevelopmentControls)Action("Simulate phone already used",()=>State.ProviderResult("PROVIDER_ALREADY_USED"));break;
                case MockPage.Otp:Title("Verification preview","Enter verification code","Use demo code 123456. No SMS sent.");var otp=Field("6-digit OTP");otp.maxLength=6;Submit("Verify",()=>{if(State.VerifyOtp(otp.value))Rebuild();});Action("Resend Code",()=>State.Notice="Demo code remains 123456.");break;
                case MockPage.OtherDevice:Title("Session preview","Already signed in","You're already signed in on another device. Signing in here will end the other session. This is a visual simulation only.");Action("Continue on this device",State.ContinueDevice,true);Action("Cancel",()=>State.Start(MockEntry.NO_SESSION));break;
                case MockPage.Experience:
                    ExperienceScreen();break;
                case MockPage.CoachSelection:
                    Progress(2);Title("A tu lado","Elige tu entrenador","Diez personalidades. Un mismo amor por el dominó.");
                    CoachSelectionGrid();break;
                case MockPage.Contacts:Progress(3);Title("La mesa se comparte","Encuentra personas que conoces","Find friends who already play Cuban Domino Club. This preview uses a fictional contact list and never requests access to your contacts.");Action("Find My Contacts",()=>State.Go(MockPage.ContactResults),true);Action("Skip",()=>State.Contacts(false));break;
                case MockPage.ContactResults:ContactResultsScreen();break;
                case MockPage.Trial:MembershipScreen(false);break;
                case MockPage.Plans:case MockPage.Membership:MembershipScreen(true);break;
                case MockPage.Home:
                    Title("Cuban Domino Club","Hola, "+(State.AccountType=="GUEST"?"Guest":"Alex")+".","Una buena partida empieza con una buena mesa.");
                    var hero=Card("¿Jugamos?","Strategy, connection and a little Cuban spirit.");Action("PLAY  →",()=>State.Go(MockPage.Play),true,true,hero);
                    var selected=State.SelectedCoach??MockCatalog.Coaches[5];CoachCard(selected);Action("Continue Learning",()=>State.Tab("Learn"));Card("Your table of friends","5 fictional players · Mock social preview");Action("Explore Friends",()=>State.Go(MockPage.Friends));if(State.AccountType=="GUEST")Action("Secure your account",State.Secure);break;
                case MockPage.Puzzles:Title("A little challenge","Puzzles");Card("Daily Puzzle","Find the move that changes the game. Coming soon.");Action("Preview puzzle",()=>State.Placeholder("Daily Puzzle"));break;
                case MockPage.Learn:
                    Title("Your daily training","LEARN");
                    Text("Selected Coach",18);
                    var learningCoach=State.SelectedCoach??MockCatalog.Coaches[5];
                    var coachPanel=Card(learningCoach.DisplayName,"Your Coach");Avatar(learningCoach,coachPanel);
                    Text("Hola, soy "+learningCoach.DisplayName+". Let's continue your training.",15,coachPanel);
                    Text("Continue Learning",20);Card("Next Lesson · Reading the table","A short introduction to spotting your next move. Mock lesson.");
                    Action("Continue Learning",()=>State.Placeholder("Lesson · Reading the table"),true);
                    Text("Training",20);
                    Action("Lessons",()=>State.Placeholder("Lessons"));
                    Action("Puzzles",()=>State.Tab("Puzzles"));
                    Action("Coach Games",()=>State.Go(MockPage.CoachGame));
                    Action("Game Review · DIAMOND",()=>State.Placeholder("Game Review · Mock"));
                    Action("Move Explanations · DIAMOND",()=>State.Placeholder("Move Explanations · Mock"));
                    Action("Training Progress",()=>State.Placeholder("Training Progress"));
                    Text("Preview only. No lessons, AI, billing or entitlement enforcement.",12);break;
                case MockPage.Watch:Title("Around the club","Watch","Video content from the club.");Card("More from your club","Navigation preview. Final functionality is not defined yet.");break;
                case MockPage.Menu:MenuScreen();break;
                case MockPage.Profile:ProfileScreen();break;
                case MockPage.ProfileHistory:ProfileHistoryScreen();break;
                case MockPage.Play:Title("Take your seat","Play","Pick a mode. No matchmaking will start.");foreach(var mode in new[]{"1v1 Human","2v2 Human"}){var m=mode;Select(m,State.PreferredHumanGameMode==m,()=>State.PreferredHumanGameMode=m);}Action("START GAME",()=>State.Placeholder("Game preview · "+State.PreferredHumanGameMode),true);Action("Tournaments",()=>State.Placeholder("Tournaments"));Action("Play a Friend",()=>State.Go(MockPage.Friends));Action("Play Bots",()=>State.Go(MockPage.Bots));Action("Play Coach",()=>State.Go(MockPage.CoachGame));break;
                case MockPage.Bots:Title("Practice your way","Play Bots","No bot engine is connected.");foreach(var mode in new[]{"1v1","2v2"}){var m=mode;Select(m,State.BotMode==m,()=>State.BotMode=m);}foreach(var d in new[]{"Easy","Normal","Hard","Expert"}){var value=d;Select(d,State.Difficulty==d,()=>State.Difficulty=value);}Action("Start Game · Demo",()=>State.Placeholder("Bot game preview"),true);break;
                case MockPage.CoachGame:Title("Learn at the table","Play Coach");CoachCard(State.SelectedCoach??MockCatalog.Coaches[5]);foreach(var mode in new[]{"1v1","2v2"}){var m=mode;Select(m,State.CoachMode==m,()=>State.CoachMode=m);}Action("Start Coach Game",()=>State.Placeholder("Coach game preview"),true);break;
                case MockPage.Friends:Title("Better together","Friends","Fictional players. No Social requests are made.");Field("Search by name or username");foreach(var title in new[]{"Contacts","Facebook Friends","Invite Friends","Share QR Code","Send Challenge Link"}){var name=title;Action(name+"  ›",()=>State.Placeholder(name));}Text("MY FRIENDS · MOCK",12);foreach(var name in MockCatalog.Friends)Card(name,"Demo friend · No backend relationship");break;
                case MockPage.Messages:Title("Across the table","Messages","Fictional conversations.");for(int i=0;i<3;i++){var name=MockCatalog.Friends[i];var c=Card(name,i==0?"¿Una partida? · 10:24 · 2 unread":"¡Nos vemos en la mesa! · Yesterday");Action("Open conversation",()=>{State.ConversationName=name;State.Go(MockPage.Conversation);},false,true,c);}break;
                case MockPage.Conversation:Title("Mock conversation",State.ConversationName);Card(State.ConversationName,"¿Jugamos una partida?   10:24");Card("You","¡Claro! Nos vemos en la mesa.   10:25");Field("Message preview",false,"Typing is local only; no send operation.");Action("Emoji ☺",()=>State.Notice="☺  ♥  ★  — visual preview only");Action("Challenge",()=>State.Placeholder("Challenge preview"));break;
                case MockPage.Stats:Title("Every game teaches","Stats","Illustrative data only.");Choices("Mode",new[]{"All","1v1","2v2"},State.StatsMode,v=>State.StatsMode=v);Choices("Period",new[]{"7D","30D","90D","1Y","All"},State.StatsPeriod,v=>State.StatsPeriod=v);Card("24 Games    ·    15 Wins","9 Losses   ·   62.5% Win Rate\nMock overview · "+State.StatsMode+" / "+State.StatsPeriod);Card("Game Mode","1v1: 10 games · 2v2: 14 games");Card("Partner","Ana · 8 games together");Card("Opponent","Carlos · 6 games");break;
                case MockPage.Coach:Title("Your guide","Coach");CoachCard(State.SelectedCoach??MockCatalog.Coaches[5]);foreach(var section in new[]{"Lessons","Puzzles","Coach Games","Game Review"}){var title=section;Action(title,()=>State.Placeholder(title));}break;
                case MockPage.Theme:Title("Made for the club","Theme","ModernSocialPremium is active everywhere.");Card("Future possibilities","Custom · Green · Blue · Pink · White · Dark\nPreview labels only. Theme switching is not implemented.");break;
                case MockPage.Settings:Title("Your preferences","Settings");Card("Settings preview","Account, language and notifications will be reviewed in a later phase. Nothing is changed here.");break;
                case MockPage.Support:Title("We're here","Help & Support");Card("How can we help?","FAQ and support routes are visual placeholders. No external message is sent.");break;
                default:Title("Coming later",State.PlaceholderTitle,"This is a navigation preview. No external service is called.");break;
            }
        }
        void Choices(string title,string[] options,string selected,Action<string> choose)
        {Text(title,12);var row=new VisualElement();row.style.flexDirection=FlexDirection.Row;row.style.flexWrap=Wrap.Wrap;body.Add(row);foreach(var value in options){var v=value;var b=Action((selected==v?"✓ ":"")+v,()=>choose(v),selected==v,true,row);b.style.flexGrow=1;b.style.marginRight=4;b.style.minWidth=50;}}
        void Nav()
        {
            footer=new VisualElement { name="MockBottomToolbar" };
            footer.style.flexDirection=FlexDirection.Row;footer.style.flexShrink=0;
            footer.style.backgroundColor=MockShellTheme.Background;Pad(footer,6);Add(footer);
            foreach(var tab in new[]{"Home","Puzzles","Learn","Watch","Menu"})
            {
                var target=tab;
                var tint=State.ActiveTab==tab?MockShellTheme.Text:MockShellTheme.Inactive;
                var button=Button(tab,()=>Act(()=>State.Tab(target)));
                button.name="Nav"+tab;button.tooltip=tab;button.text="";
                button.style.width=Length.Percent(20);button.style.flexGrow=0;button.style.flexShrink=0;button.style.minWidth=0;
                button.style.minHeight=60;button.style.marginBottom=0;
                button.style.paddingLeft=0;button.style.paddingRight=0;
                button.style.alignItems=Align.Center;button.style.justifyContent=Justify.Center;
                button.style.backgroundColor=MockShellTheme.Background;button.style.color=tint;
                var icon=new Image { name="Icon"+tab, pickingMode=PickingMode.Ignore };
                icon.vectorImage=Resources.Load<VectorImage>("AppShellMockIcons/icon_nav_"+tab.ToLowerInvariant());
                icon.tintColor=tint;icon.style.width=26;icon.style.height=26;icon.style.flexShrink=0;
                var label=new Label(tab){name="Label"+tab,pickingMode=PickingMode.Ignore};
                label.style.color=tint;label.style.fontSize=12;label.style.marginTop=3;
                label.style.unityTextAlign=TextAnchor.MiddleCenter;
                button.Add(icon);button.Add(label);footer.Add(button);
            }
        }
    }
}
