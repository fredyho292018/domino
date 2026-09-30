using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Domino.AppShellMock
{
    public enum MockMembershipPlan { GOLD, PLATINUM, DIAMOND, FRIENDS_AND_FAMILY }
    public enum MockBillingPeriod { MONTHLY, YEARLY }
    public static class MockMembershipPricing
    {
        public static decimal Monthly(MockMembershipPlan plan) => new[]{3.99m,6m,10m,16m}[(int)plan];
        public static decimal Annual(MockMembershipPlan plan) => new[]{36m,48m,80m,120m}[(int)plan];
        public static decimal Annualized(MockMembershipPlan plan) => Monthly(plan)*12m;
        public static decimal Savings(MockMembershipPlan plan) => Annualized(plan)-Annual(plan);
        public static decimal SavingsPercent(MockMembershipPlan plan) => Math.Round(Savings(plan)/Annualized(plan)*100m,0,MidpointRounding.AwayFromZero);
        public static decimal MonthlyEquivalent(MockMembershipPlan plan) => Math.Round(Annual(plan)/12m,2,MidpointRounding.AwayFromZero);
        public static string Money(decimal value,bool cents=false) => "$"+value.ToString(cents?"0.00":"0.##",CultureInfo.InvariantCulture);
        public static string Name(MockMembershipPlan plan) => MockCatalog.Plans[(int)plan];
        public static string Price(MockMembershipPlan plan,MockBillingPeriod period) => period==MockBillingPeriod.YEARLY?Money(Annual(plan))+"/year":Money(Monthly(plan))+"/month";
        public static string[] Features(MockMembershipPlan plan) => MockCatalog.Features((int)plan).Split(new[]{" · "},StringSplitOptions.RemoveEmptyEntries);
    }
    // Session-only UX state. Not a persistence DTO, entitlement or authentication contract.
    public enum MockEntry { NO_SESSION, NEW_GUEST, EXISTING_GUEST, NEW_REGISTERED_GOOGLE, NEW_REGISTERED_FACEBOOK, NEW_REGISTERED_EMAIL, NEW_REGISTERED_PHONE, EXISTING_REGISTERED, REGISTERED_ON_OTHER_DEVICE }
    public enum MockPage { Welcome, SignIn, EmailRegister, EmailSignIn, ExistingEmail, SendEmail, CheckEmail, LoadAccount, Phone, Otp, Provider, ProviderConflict, OtherDevice, SecureAccount, Experience, CoachSelection, Contacts, ContactResults, Trial, Plans, Home, Puzzles, Learn, Watch, Menu, Friends, Messages, Conversation, Stats, Coach, Theme, Membership, Settings, Support, Play, Bots, CoachGame, Placeholder, Profile, ProfileHistory }
    public sealed class MockCoach
    {
        public readonly string Id, DisplayName, Avatar, Gender;
        public string ShortGreeting => "Hola, soy " + DisplayName + ".\nTe enseñaré a jugar dominó.";
        public MockCoach(string id, string name, string avatar, string gender) { Id=id; DisplayName=name; Avatar=avatar; Gender=gender; }
    }
    public static class MockCatalog
    {
        public static readonly MockCoach[] Coaches = {
            new MockCoach("lucia","Lucía","coral","woman"), new MockCoach("elena","Elena","gold","woman"),
            new MockCoach("amara","Amara","teal","woman"), new MockCoach("mei","Mei","plum","woman"), new MockCoach("sofia","Sofía","slate","woman"),
            new MockCoach("david","David","gold","man"), new MockCoach("mateo","Mateo","teal","man"),
            new MockCoach("gabriel","Gabriel","coral","man"), new MockCoach("leo","Leo","slate","man"), new MockCoach("omar","Omar","plum","man") };
        public static readonly string[] Friends = { "Ana Rivera", "Carlos Vega", "Isabel Cruz", "Luis Moreno", "Nora Díaz" };
        public static readonly string[] Experience = { "No sé jugar", "Conozco las reglas y los conceptos", "Tengo conocimientos de estrategia y tácticas", "Soy un jugador de torneo" };
        public static readonly string[] Plans = { "GOLD", "PLATINUM", "DIAMOND", "FRIENDS & FAMILY" };
        public static readonly string[] Monthly = { "$3.99", "$6", "$10", "$16" }, Annual = { "$36", "$48", "$80", "$120" };
        public static string Features(int plan) => (plan>=1?"Game Review · ":"")+(plan>=2?"Move Explanations · Advanced Stats · ":"")+"Puzzles · Lessons · Coach Games · Bots · No Ads";
    }
    public sealed partial class MockShellState
    {
        public MockPage Page { get; private set; } = MockPage.Welcome;
        public string AccountType="NONE", Provider="", SelectedCoachId="", ContactsChoice="", MembershipChoice="FREE", PreferredHumanGameMode="1v1 Human", MockActiveSession="NONE", PreviousSession="NONE";
        public bool OnboardingCompleted;
        public int OnboardingCurrentStep, ExperienceLevel=-1;
        public readonly HashSet<string> MockFriendSelections=new HashSet<string>();
        public int ContactResultSelectionCount => MockCatalog.Friends.Count(MockFriendSelections.Contains);
        public bool AllContactResultsSelected => MockCatalog.Friends.Length>0 && ContactResultSelectionCount==MockCatalog.Friends.Length;
        public string ContactResultsToggleLabel => AllContactResultsSelected?"Deselect All":"Select All";
        public string ContactResultsAddLabel => "Add Selected ("+ContactResultSelectionCount+")";
        public bool CanAddContactResults => ContactResultSelectionCount>0;
        public void ToggleContactResult(string name)
        {
            if(!MockCatalog.Friends.Contains(name))return;
            if(!MockFriendSelections.Add(name))MockFriendSelections.Remove(name);
        }
        public void ToggleAllContactResults()
        {
            if(AllContactResultsSelected)MockFriendSelections.ExceptWith(MockCatalog.Friends);
            else MockFriendSelections.UnionWith(MockCatalog.Friends);
        }
        public string Error="", Notice="", ActiveTab="Home", ConversationName="Ana Rivera", PlaceholderTitle="", StatsMode="All", StatsPeriod="30D", Difficulty="Normal", CoachMode="1v1", BotMode="1v1";
        public MockMembershipPlan selectedMembershipPlan=MockMembershipPlan.DIAMOND;
        public MockBillingPeriod selectedBillingPeriod=MockBillingPeriod.YEARLY;
        public string MembershipSummary => MockMembershipPricing.Name(selectedMembershipPlan)+" · "+(selectedBillingPeriod==MockBillingPeriod.YEARLY?"Yearly":"Monthly")+"\n7 days free\nThen "+MockMembershipPricing.Price(selectedMembershipPlan,selectedBillingPeriod);
        public bool SecureFlow, SignInFlow;
        readonly Stack<MockPage> back=new Stack<MockPage>();
        public MockCoach SelectedCoach => MockCatalog.Coaches.FirstOrDefault(c=>c.Id==SelectedCoachId);
        public bool ShellVisible => OnboardingCompleted && (int)Page >= (int)MockPage.Home;
        public void Start(MockEntry entry)
        {
            selectedMembershipPlan=MockMembershipPlan.DIAMOND;selectedBillingPeriod=MockBillingPeriod.YEARLY;
            back.Clear(); AccountType="NONE";Provider="";SelectedCoachId="";ContactsChoice="";MembershipChoice="FREE";PreferredHumanGameMode="1v1 Human";MockActiveSession="NONE";PreviousSession="NONE";
            OnboardingCompleted=false;OnboardingCurrentStep=0;ExperienceLevel=-1;MockFriendSelections.Clear();Error="";Notice="";SecureFlow=false;SignInFlow=false;ActiveTab="Home";
            if(entry==MockEntry.NO_SESSION){Page=MockPage.Welcome;return;}
            if(entry==MockEntry.REGISTERED_ON_OTHER_DEVICE){Page=MockPage.OtherDevice;return;}
            bool guest=entry==MockEntry.NEW_GUEST||entry==MockEntry.EXISTING_GUEST;
            bool existing=entry==MockEntry.EXISTING_GUEST||entry==MockEntry.EXISTING_REGISTERED;
            Provider=guest?"Guest":entry.ToString().Replace("NEW_REGISTERED_","");
            AccountType=guest?"GUEST":"REGISTERED";OnboardingCompleted=existing;MockActiveSession="ACTIVE";
            if(existing){ExperienceLevel=1;SelectedCoachId="david";Page=MockPage.Home;}else Page=MockPage.Experience;
        }
        public void Go(MockPage page){back.Push(Page);Page=page;Error="";Notice="";}
        public void Back()
        {
            Error="";Notice="";
            if(Page==MockPage.CoachSelection){Page=MockPage.Experience;OnboardingCurrentStep=0;return;}
            if(Page==MockPage.Contacts){Page=MockPage.CoachSelection;OnboardingCurrentStep=1;return;}
            if(Page==MockPage.Trial){Page=MockPage.Contacts;OnboardingCurrentStep=2;return;}
            if(Page==MockPage.Experience)return;
            if(back.Count>0){Page=back.Pop();return;}
            Page=OnboardingCompleted?MockPage.Home:MockPage.Welcome;
        }
        public void Tab(string tab)
        {
            if(!OnboardingCompleted)return;
            back.Clear();ActiveTab=tab;Page=tab=="Home"?MockPage.Home:tab=="Puzzles"?MockPage.Puzzles:tab=="Learn"?MockPage.Learn:tab=="Watch"?MockPage.Watch:MockPage.Menu;Error="";Notice="";
        }
        public void Guest(){AccountType="GUEST";Provider="Guest";Bootstrap(false);}
        void Bootstrap(bool existing)
        {
            MockActiveSession="ACTIVE";back.Clear();Error="";
            if(existing){OnboardingCompleted=true;SelectedCoachId="david";ExperienceLevel=1;}
            Page=OnboardingCompleted?MockPage.Home:MockPage.Experience;SecureFlow=false;SignInFlow=false;
        }
        public void BeginProvider(string provider){Provider=provider;Go(provider=="Email"?(SignInFlow?MockPage.EmailSignIn:MockPage.EmailRegister):provider=="Phone"?MockPage.Phone:MockPage.Provider);}
        public void ProviderResult(string result)
        {
            if(result=="PROVIDER_ALREADY_USED"){Go(MockPage.ProviderConflict);return;}
            if(result=="EXISTING_ACCOUNT"&&AccountType=="GUEST"){Go(MockPage.LoadAccount);return;}
            AccountType="REGISTERED";Bootstrap(result=="EXISTING_ACCOUNT"||SignInFlow);
        }
        public bool Register(string email,string password,string confirm)
        {
            Error="";
            if(!Regex.IsMatch(email??"",@"^[^\s@]+@[^\s@]+\.[^\s@]+$")){Error="Enter a valid email address.";return false;}
            if(string.IsNullOrEmpty(password)||password.Length<6){Error="Use at least 6 characters for this demo.";return false;}
            if(password!=confirm){Error="Passwords do not match.";return false;}
            if(email.Equals("existing@example.test",StringComparison.OrdinalIgnoreCase)){Go(MockPage.ExistingEmail);return false;}
            AccountType="REGISTERED";Provider="Email";Bootstrap(false);return true;
        }
        public bool EmailLogin(string email,string password)
        {
            if(!Regex.IsMatch(email??"",@"^[^\s@]+@[^\s@]+\.[^\s@]+$")||string.IsNullOrEmpty(password)){Error="Enter a valid demo email and password.";return false;}
            Provider="Email";ProviderResult("EXISTING_ACCOUNT");return true;
        }
        public bool PhoneContinue(string phone){if(!Regex.IsMatch(phone??"",@"^\d{6,15}$")){Error="Enter 6–15 digits for this demo.";return false;}Go(MockPage.Otp);return true;}
        public bool VerifyOtp(string code){if(code!="123456"){Error="Use the demo verification code 123456.";return false;}ProviderResult(SignInFlow?"EXISTING_ACCOUNT":"NEW_ACCOUNT");return true;}
        public void Secure(){SecureFlow=true;SignInFlow=false;Go(MockPage.SecureAccount);}
        public void ConfirmEmail(){if(Page==MockPage.CheckEmail)Go(MockPage.LoadAccount);}
        public void LoadExisting(){AccountType="REGISTERED";MockFriendSelections.Clear();MembershipChoice="FREE";OnboardingCompleted=true;Bootstrap(true);}
        public void ContinueDevice(){PreviousSession="REVOKED";AccountType="REGISTERED";Provider="Email";Bootstrap(true);Notice="Demo: previous session revoked; this session is active.";}
        public bool ContinueExperience(){if(ExperienceLevel<0)return false;OnboardingCurrentStep=1;Go(MockPage.CoachSelection);return true;}
        public bool ContinueCoach(){if(SelectedCoach==null)return false;OnboardingCurrentStep=2;Go(MockPage.Contacts);return true;}
        public void Contacts(bool add){ContactsChoice=add?"MOCK_SELECTED":"SKIPPED";if(!add)MockFriendSelections.Clear();OnboardingCurrentStep=3;Go(MockPage.Trial);}
        public void Finish(bool trial){MembershipChoice=trial?"MOCK_"+selectedMembershipPlan+"_TRIAL":"FREE";OnboardingCompleted=true;OnboardingCurrentStep=4;back.Clear();Page=MockPage.Home;ActiveTab="Home";}
        public void Placeholder(string title){PlaceholderTitle=title;Go(MockPage.Placeholder);}
    }
}
