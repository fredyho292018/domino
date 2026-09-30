using System;
using System.Linq;
using Domino.AppShellMock;
static class AppShell01NavigationTests
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    static void Finish(MockShellState s,bool trial=false){s.ExperienceLevel=2;Check(s.ContinueExperience(),"experience");s.SelectedCoachId="amara";Check(s.ContinueCoach(),"coach");s.Contacts(false);s.Finish(trial);Check(s.Page==MockPage.Home&&s.OnboardingCompleted,"finish");}
    public static void Main()
    {
        var s=new MockShellState();s.Start(MockEntry.NO_SESSION);Check(s.Page==MockPage.Welcome,"welcome");
        foreach(var e in new[]{MockEntry.NEW_GUEST,MockEntry.NEW_REGISTERED_GOOGLE,MockEntry.NEW_REGISTERED_FACEBOOK,MockEntry.NEW_REGISTERED_EMAIL,MockEntry.NEW_REGISTERED_PHONE})
        {s.Start(e);Check(s.Page==MockPage.Experience,"new account onboarding");Check(!s.ContinueExperience(),"experience required");s.ExperienceLevel=1;s.ContinueExperience();Check(!s.ContinueCoach(),"coach required");Finish(s);}
        foreach(var e in new[]{MockEntry.EXISTING_REGISTERED,MockEntry.EXISTING_GUEST}){s.Start(e);Check(s.Page==MockPage.Home&&s.OnboardingCompleted,"existing skips onboarding");}
        s.Start(MockEntry.NO_SESSION);s.Guest();Check(s.AccountType=="GUEST"&&s.Page==MockPage.Experience,"guest entry");Finish(s,true);Check(s.MembershipChoice=="MOCK_DIAMOND_TRIAL","mock trial");s.Back();Check(s.Page==MockPage.Home,"home never returns welcome");
        s.Secure();Check(s.Page==MockPage.SecureAccount,"secure");s.BeginProvider("Google");s.ProviderResult("PROVIDER_ALREADY_USED");Check(s.AccountType=="GUEST"&&s.Page==MockPage.ProviderConflict,"provider conflict preserves guest");s.Back();s.ProviderResult("NEW_ACCOUNT");Check(s.AccountType=="REGISTERED"&&s.SelectedCoachId=="amara"&&s.Page==MockPage.Home,"secure conversion retains demo progress");
        s.Start(MockEntry.EXISTING_GUEST);s.Secure();s.BeginProvider("Email");Check(!s.Register("bad","abcdef","abcdef")&&s.Page==MockPage.EmailRegister,"email invalid");Check(!s.Register("new@example.test","abcdef","different"),"password mismatch");Check(!s.Register("existing@example.test","abcdef","abcdef")&&s.Page==MockPage.ExistingEmail,"existing email");s.Go(MockPage.SendEmail);s.Go(MockPage.CheckEmail);s.ConfirmEmail();Check(s.Page==MockPage.LoadAccount&&s.AccountType=="GUEST","confirmation not merge");s.Back();Check(s.AccountType=="GUEST","cancel preserves guest");s.ConfirmEmail();s.LoadExisting();Check(s.AccountType=="REGISTERED"&&s.Page==MockPage.Home,"load existing");
        s.Start(MockEntry.NO_SESSION);s.BeginProvider("Email");Check(s.Register("new@example.test","abcdef","abcdef")&&s.Page==MockPage.Experience,"email new");
        s.Start(MockEntry.NO_SESSION);s.BeginProvider("Phone");Check(!s.PhoneContinue("abc"),"phone validation");Check(s.PhoneContinue("5550100123")&&s.Page==MockPage.Otp,"phone otp");Check(!s.VerifyOtp("000000"),"invalid otp");Check(s.VerifyOtp("123456")&&s.Page==MockPage.Experience,"valid demo otp");
        foreach(var p in new[]{"Google","Facebook"}){s.Start(MockEntry.NO_SESSION);s.BeginProvider(p);s.ProviderResult("NEW_ACCOUNT");Check(s.Page==MockPage.Experience,"provider new");Finish(s);}
        s.Start(MockEntry.REGISTERED_ON_OTHER_DEVICE);Check(s.Page==MockPage.OtherDevice,"other device");s.ContinueDevice();Check(s.PreviousSession=="REVOKED"&&s.MockActiveSession=="ACTIVE"&&s.Page==MockPage.Home,"session simulation");
        foreach(var tab in new[]{"Home","Puzzles","Learn","Watch","Menu"}){s.Tab(tab);Check(s.ActiveTab==tab&&s.ShellVisible,"tabs");}
        foreach(var page in new[]{MockPage.Friends,MockPage.Messages,MockPage.Stats,MockPage.Coach,MockPage.Theme,MockPage.Membership,MockPage.Settings,MockPage.Support,MockPage.Play,MockPage.Bots,MockPage.CoachGame}){s.Tab("Menu");s.Go(page);s.Back();Check(s.Page==MockPage.Menu,"subpage back");}
        s.Start(MockEntry.NEW_GUEST);s.ExperienceLevel=3;s.ContinueExperience();s.SelectedCoachId="david";s.ContinueCoach();s.Go(MockPage.ContactResults);s.MockFriendSelections.Add("Ana Rivera");s.MockFriendSelections.Add("Carlos Vega");s.Contacts(true);Check(s.MockFriendSelections.Count==2&&s.Page==MockPage.Trial,"contacts add");s.Back();Check(s.Page==MockPage.Contacts&&s.OnboardingCurrentStep==2,"onboarding back");s.Contacts(false);Check(s.MockFriendSelections.Count==0,"skip clears selections");s.Finish(false);Check(s.MembershipChoice=="FREE","not now");s.PreferredHumanGameMode="2v2 Human";s.Tab("Menu");s.Go(MockPage.Play);Check(s.PreferredHumanGameMode=="2v2 Human","mode retained");
        Check(MockCatalog.Coaches.Length==10&&MockCatalog.Coaches.Count(c=>c.Gender=="woman")==5&&MockCatalog.Coaches.Count(c=>c.Gender=="man")==5,"coach catalog");
        Console.WriteLine("APP_SHELL_NAVIGATION_CHECKS_PASS="+checks);
        int baseline=checks;
        var tabs=new[]{"Home","Puzzles","Learn","Watch","Menu"};
        var routes=new[]{MockPage.Home,MockPage.Puzzles,MockPage.Learn,MockPage.Watch,MockPage.Menu};
        s.Start(MockEntry.EXISTING_REGISTERED);
        for(int i=0;i<tabs.Length;i++){s.Tab(tabs[i]);Check(s.Page==routes[i]&&s.ActiveTab==tabs[i],"toolbar route");}
        foreach(var page in new[]{MockPage.Home,MockPage.Play,MockPage.Friends,MockPage.Messages,MockPage.Conversation,MockPage.Stats,MockPage.Coach,MockPage.Theme,MockPage.Membership}){s.Go(page);Check(s.ShellVisible,"toolbar persists");}
        s.SelectedCoachId="amara";s.Tab("Learn");Check(s.SelectedCoach.Id=="amara","Learn preserves selected coach");
        s.Placeholder("Lesson");s.Back();Check(s.Page==MockPage.Learn&&s.ActiveTab=="Learn","Lesson back to Learn");
        s.Go(MockPage.CoachGame);s.Back();Check(s.Page==MockPage.Learn,"Coach game back to Learn");
        Console.WriteLine("TOOLBAR_NAVIGATION_CHECKS_PASS="+(checks-baseline));
        baseline=checks;s.Start(MockEntry.NEW_GUEST);s.Go(MockPage.ContactResults);
        Check(!s.CanAddContactResults&&s.ContactResultsAddLabel=="Add Selected (0)","zero disabled");
        s.ToggleAllContactResults();Check(s.ContactResultSelectionCount==5,"select five");
        Check(s.ContactResultsToggleLabel=="Deselect All","all label");
        Check(s.ContactResultsAddLabel=="Add Selected (5)"&&s.CanAddContactResults,"five CTA");
        s.ToggleAllContactResults();Check(s.ContactResultSelectionCount==0&&!s.CanAddContactResults,"clear five");
        s.ToggleContactResult("Ana Rivera");Check(s.ContactResultsAddLabel=="Add Selected (1)"&&s.CanAddContactResults,"individual select");
        s.ToggleAllContactResults();s.ToggleContactResult("Carlos Vega");
        Check(s.ContactResultsToggleLabel=="Select All"&&s.ContactResultSelectionCount==4,"manual deselect");
        s.ToggleContactResult("Carlos Vega");Check(s.AllContactResultsSelected,"individual reselect");
        s.MockFriendSelections.Add("Outside results");s.ToggleAllContactResults();
        Check(s.MockFriendSelections.SetEquals(new[]{"Outside results"})&&!s.CanAddContactResults,"scoped clear and count");
        s.ToggleContactResult("Unknown");Check(!s.MockFriendSelections.Contains("Unknown"),"unknown excluded");
        Check(s.Page==MockPage.ContactResults&&s.ContactsChoice=="","no submit");
        Console.WriteLine("CONTACT_RESULTS_CHECKS_PASS="+(checks-baseline));
        baseline=checks;s.Start(MockEntry.NEW_GUEST);
        Check(s.selectedMembershipPlan==MockMembershipPlan.DIAMOND,"default Diamond");
        Check(s.selectedBillingPeriod==MockBillingPeriod.YEARLY,"default annual");
        decimal[] savings={11.88m,24m,40m,72m},percent={25m,33m,33m,38m},equivalent={3m,4m,6.67m,10m};
        string[] annual={"$36/year","$48/year","$80/year","$120/year"},monthly={"$3.99/month","$6/month","$10/month","$16/month"};
        foreach(MockMembershipPlan plan in Enum.GetValues(typeof(MockMembershipPlan)))
        {
            int i=(int)plan;s.selectedMembershipPlan=plan;
            Check(s.selectedMembershipPlan==plan,"single plan selection");
            Check(MockMembershipPricing.Savings(plan)==savings[i],"savings");
            Check(MockMembershipPricing.SavingsPercent(plan)==percent[i],"rounded percentage");
            Check(MockMembershipPricing.MonthlyEquivalent(plan)==equivalent[i],"monthly equivalent");
            s.selectedBillingPeriod=MockBillingPeriod.YEARLY;
            Check(s.MembershipSummary.Contains(annual[i])&&s.MembershipSummary.Contains(MockMembershipPricing.Name(plan)),"annual summary");
            s.selectedBillingPeriod=MockBillingPeriod.MONTHLY;
            Check(s.MembershipSummary.Contains(monthly[i])&&s.MembershipSummary.Contains("Monthly"),"monthly summary");
        }
        s.Go(MockPage.Trial);s.Go(MockPage.Plans);s.selectedMembershipPlan=MockMembershipPlan.PLATINUM;s.selectedBillingPeriod=MockBillingPeriod.YEARLY;s.Back();
        Check(s.Page==MockPage.Trial&&s.selectedMembershipPlan==MockMembershipPlan.PLATINUM&&s.selectedBillingPeriod==MockBillingPeriod.YEARLY,"back preserves selection");
        Check(s.MembershipSummary.Contains("$48/year")&&s.MembershipSummary.Contains("7 days free"),"onboarding summary follows selection");
        Check(MockMembershipPricing.Features(MockMembershipPlan.GOLD).Length==5&&MockMembershipPricing.Features(MockMembershipPlan.PLATINUM).Length==6&&MockMembershipPricing.Features(MockMembershipPlan.DIAMOND).Length==8,"feature differentiation");
        s.Finish(true);Check(s.MembershipChoice=="MOCK_PLATINUM_TRIAL","mock trial uses selected plan");
        s.Start(MockEntry.NEW_GUEST);s.Finish(false);Check(s.MembershipChoice=="FREE"&&s.Page==MockPage.Home,"not now");
        Console.WriteLine("MEMBERSHIP_CHECKS_PASS="+(checks-baseline));
        baseline=checks;s.Start(MockEntry.EXISTING_REGISTERED);s.Tab("Menu");s.OpenProfile(MockProfileMode.OWN_PROFILE);
        Check(s.Page==MockPage.Profile&&s.ProfileAction=="Edit Profile","own profile");s.Back();Check(s.Page==MockPage.Menu,"profile back");
        s.OpenProfile(MockProfileMode.OTHER_NOT_FRIEND);Check(s.ProfileAction=="Add Friend","other add");s.ProfilePrimaryAction();Check(s.ProfileAction=="Request Sent","request sent");
        s.SetProfileMode(MockProfileMode.OTHER_FRIEND);Check(s.ProfileAction=="Friends","friends");
        Check(s.ProfilePreviewGames.Length==5&&s.ProfileGames.Length==8,"history limits");
        Check(s.ProfileGames.Any(g=>g.won)&&s.ProfileGames.Any(g=>!g.won)&&s.ProfileGames.Any(g=>g.Mode=="1v1")&&s.ProfileGames.Any(g=>g.Mode=="2v2"),"mixed games");
        foreach(var game in s.ProfileGames){Check(game.won==(game.currentPlayerSide==0?game.leftScore>game.rightScore:game.rightScore>game.leftScore),"explicit side/result consistency");}
        s.Go(MockPage.ProfileHistory);s.Back();Check(s.Page==MockPage.Profile,"history back");s.ShareProfile();Check(s.Notice.Contains("Nothing was shared"),"mock share");
        Check(s.Profile.countryCode=="CU"&&s.Profile.joinedAt==new DateTime(2022,1,21),"profile metadata");
        Console.WriteLine("PROFILE_STATE_CHECKS_PASS="+(checks-baseline));
        baseline=checks;
        foreach(MockPage page in Enum.GetValues(typeof(MockPage)))
        {
            if(new[]{MockPage.Welcome,MockPage.Experience,MockPage.Home,MockPage.Puzzles,MockPage.Learn,MockPage.Watch,MockPage.Menu}.Contains(page))continue;
            var trialState=new MockShellState();trialState.Start(MockEntry.NEW_GUEST);
            trialState.ExperienceLevel=2;trialState.SelectedCoachId="amara";trialState.MockFriendSelections.Add("Ana Rivera");
            trialState.selectedMembershipPlan=MockMembershipPlan.PLATINUM;trialState.selectedBillingPeriod=MockBillingPeriod.MONTHLY;
            var parent=trialState.Page;trialState.Go(page);trialState.Back();
            var expected=page==MockPage.CoachSelection?MockPage.Experience:page==MockPage.Contacts?MockPage.CoachSelection:page==MockPage.Trial?MockPage.Contacts:parent;
            Check(trialState.Page==expected,"Back parent "+page);
            Check(trialState.ExperienceLevel==2&&trialState.SelectedCoachId=="amara"&&trialState.MockFriendSelections.Contains("Ana Rivera")&&trialState.selectedMembershipPlan==MockMembershipPlan.PLATINUM&&trialState.selectedBillingPeriod==MockBillingPeriod.MONTHLY,"Back preserves state "+page);
        }
        Console.WriteLine("BACK_ROUTE_STATE_CHECKS_PASS="+(checks-baseline));
    }
}
