using System;
using System.IO;
using System.Linq;
using Domino.UI.AppShell;
using Domino.UI.Theming;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.Editor
{
    public sealed partial class ProductionShellPreview
    {
        const string ProfileResult="Library/UI02B3.result.txt";
        int profilePreset,profileChecks,profileStep;
        void ProfileCheck(bool ok,string label){if(!ok)throw new Exception(label);profileChecks++;}
        void ProfileFail(Exception e){File.AppendAllText(ProfileResult,"FAIL="+e.Message+"\n");Debug.LogException(e);}
        [MenuItem("Domino/Production App Shell/Profile preview/Own profile")]
        static void OwnProfile(){var w=GetWindow<ProductionShellPreview>();w.index=1;w.ProfileMount(new DemoProfileDataSource());}
        [MenuItem("Domino/Production App Shell/Profile preview/Other - Not friend")]
        static void OtherProfile(){var w=GetWindow<ProductionShellPreview>();w.index=1;w.ProfileMount(new DemoProfileDataSource(false));}
        void ProfileMount(IProfileDataSource source)
        {
            Mount(false);shell.RemoveFromHierarchy();shell=new ProductionAppShell(source);frame.Add(shell);shell.SetSafeArea(0,24,0,24);shell.Select(ShellTab.Menu);Click(shell.Q("MenuProfile"));
        }
        void StartProfileTests(){profilePreset=profileChecks=0;File.WriteAllText(ProfileResult,"RUNNING\n");ProfilePreset();}
        void ProfilePreset(){index=profilePreset;ProfileMount(new DemoProfileDataSource());rootVisualElement.schedule.Execute(ProfileGeometry).ExecuteLater(300);}
        void ProfileGeometry()
        {
            try {
                var page=shell.Q<ProductionProfilePage>();var body=page.Body;var theme=ThemeProvider.Current;
                ProfileCheck(page.Profile.IsOwnProfile && shell.ActiveMenuDestination==MenuDestination.Profile,"Own profile route");
                ProfileCheck(page.Q<Label>("ProfilePlayerName").text=="Alex \u00b7 Demo player","Name");
                ProfileCheck(page.Q<Label>("ProfileCountry").text=="Cuba" && page.Q<Image>("ProfileFlag").vectorImage!=null,"Country flag");
                ProfileCheck(page.Q<Label>("ProfileJoined").text=="Joined January 21, 2022","Date");
                ProfileCheck(page.Q("PlayerAvatar").Q<Image>().image!=null && page.Q("PlayerAvatar").resolvedStyle.borderTopLeftRadius==38,"Circular avatar");
                ProfileCheck(page.Q<ThemeButton>("ProfilePrimaryAction").text=="Edit Profile" && !page.Query<ThemeButton>().ToList().Any(x=>x.text=="Add Friend"),"Own actions");
                ProfileCheck(page.Q("ShareProfile").tooltip=="Share Profile" && page.Q("PlayerAvatar").tooltip=="Player avatar","Accessible names");
                var back=page.Q<Button>("ShellBack");ProfileCheck(back.layout.width>=44 && back.layout.height>=44 && back.Q<Image>().vectorImage!=null && back.tooltip=="Back","Back contract");
                var rows=page.Query<ThemeButton>("ProfileGameRow").ToList();var games=shell.ProfileDataSource.ReadGames();
                ProfileCheck(rows.Count==5 && games.Count==8,"Five of eight");
                ProfileCheck(page.Q("HistoryCount")==null,"No redundant profile total");
                ProfileCheck(games.Take(5).Any(x=>x.Mode=="1v1") && games.Take(5).Any(x=>x.Mode=="2v2"),"Game modes");
                ProfileCheck(games.Take(5).Any(x=>x.Won)&&games.Take(5).Any(x=>!x.Won),"Results both");
                for(int i=0;i<rows.Count;i++){
                    var row=rows[i];var game=games[i];
                    ProfileCheck(row.Q<Label>("GameScore").text==game.Score && row.Q<Label>("GameScore").resolvedStyle.unityTextAlign==TextAnchor.MiddleRight,"Explicit score");
                    ProfileCheck(row.Q<Label>("GameResult").text==game.Result && row.Q<Label>("GameResult").resolvedStyle.color==(game.Won?theme.Colors.Win:theme.Colors.Loss),"Semantic result");
                    ProfileCheck(row.layout.height>=88,"Row touch");
                    var description=row.Q<Label>("GameMatchup");var result=row.Q<Label>("GameResult");var chevron=row.Q("GameChevron");var scoreLabel=row.Q<Label>("GameScore");
                    ProfileCheck(row.Q("PlayerAvatar").layout.width==32,"Fixed avatar column");
                    ProfileCheck(description.resolvedStyle.whiteSpace==WhiteSpace.Normal && description.layout.yMax+3.9f<=result.layout.y,"Result separated below wrapped name");
                    ProfileCheck(scoreLabel.layout.width>=76 && scoreLabel.resolvedStyle.whiteSpace==WhiteSpace.NoWrap,"Score column no wrap");
                    ProfileCheck(scoreLabel.layout.xMax<=chevron.layout.x && chevron.layout.xMax<=row.layout.width && chevron.layout.width==16,"Separate visible chevron");
                    ProfileCheck(row.Q("GameText").layout.y+result.layout.yMax<=row.layout.height-row.resolvedStyle.paddingBottom+.1f,"Auto height contains result");
                    var measured=description.MeasureTextSize(description.text,description.layout.width,VisualElement.MeasureMode.Exactly,0,VisualElement.MeasureMode.Undefined);
                    ProfileCheck(description.layout.height+1>=measured.y,"Full match name visible");
                    ProfileCheck(scoreLabel.MeasureTextSize(scoreLabel.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).x<=scoreLabel.layout.width+.1f,"Score readable");
                    var text=row.Q("GameText");var score=row.Q("GameScore");
                    ProfileCheck(text.layout.x>=row.Q("PlayerAvatar").layout.xMax && LogicalX(text,body)+text.layout.width<=LogicalX(score,body),"No avatar/score collision");
                }
                ProfileBounds(page);
                ProfileCheck(shell.Query<VisualElement>("BottomNavigation").ToList().Count==1 && page.Q("BottomNavigation")==null,"Single toolbar");
                ProfileCheck(page.Query<TextElement>().ToList().All(x=>x.text.IndexOfAny(new[]{'\u00c2','\u00c3','\u00e2','\ufffd'})<0),"Profile UTF8");
                Click(page.Q("ShareProfile"));ProfileCheck(page.Q<Label>("ProfileNotice").text=="Sharing coming soon." && shell.ActiveProfileSection==ProfileSection.Profile,"Share local only");
                Resize((profilePreset+1)%Sizes.Length);ProfileCheck(shell.ActiveProfileSection==ProfileSection.Profile && shell.HasSubpage,"Profile resize preserved");Resize(profilePreset);
                profileStep=0;rootVisualElement.schedule.Execute(ProfileChildOpen).ExecuteLater(250);
            }catch(Exception e){ProfileFail(e);}
        }
        void ProfileBounds(ProductionProfilePage page)
        {
            var body=page.Body;float safeLeft=shell.resolvedStyle.paddingLeft,safeWidth=shell.layout.width-safeLeft-shell.resolvedStyle.paddingRight;
            ProfileCheck(Mathf.Abs(LogicalX(body,shell)+body.layout.width/2-safeLeft-safeWidth/2)<1 && body.layout.width<=620.1f,"Profile centered");
            float end=Mathf.Max(0,page.contentContainer.layout.height-page.contentViewport.layout.height);
            foreach(var child in body.Children())ProfileCheck(child.layout.x>=0 && child.layout.xMax<=body.layout.width+.1f && child.layout.yMax<=end+page.contentViewport.layout.height+1,"Profile reachable "+child.name);
            float navY=shell.BottomNavigation.layout.y;page.scrollOffset=new Vector2(0,end);
            ProfileCheck(Mathf.Abs(page.scrollOffset.y-end)<1 && shell.BottomNavigation.layout.y==navY,"Profile scrolling fixed nav");
            if(profilePreset==0)ProfileCheck(end>0,"Small phone scroll");page.scrollOffset=Vector2.zero;
        }
        void ProfileChildOpen()
        {
            try {
                var control=profileStep==0?shell.Q("ProfilePrimaryAction"):profileStep==1?shell.Q("ProfileGameRow"):shell.Q("ViewAllGames");Click(control);
                Resize((profilePreset+1)%Sizes.Length);
                rootVisualElement.schedule.Execute(ProfileChildBack).ExecuteLater(250);
            }catch(Exception e){ProfileFail(e);}
        }
        void ProfileChildBack()
        {
            try {
                var expected=profileStep==0?ProfileSection.EditProfile:profileStep==1?ProfileSection.GameDetails:ProfileSection.GameHistory;
                ProfileCheck(shell.ActiveProfileSection==expected && shell.ActiveTab==ShellTab.Menu,"Child preserved on resize");
                if(expected==ProfileSection.GameHistory){ProfileCheck(shell.Query<ThemeButton>("ProfileGameRow").ToList().Count==8,"Full history");ProfileBounds(shell.Q<ProductionProfilePage>());}
                else ProfileCheck(shell.Q<Label>("SubpageMessage").text=="Coming Soon","Safe placeholder");
                Click(shell.Q("ShellBack"));ProfileCheck(shell.ActiveProfileSection==ProfileSection.Profile && shell.HasSubpage,"Child Back Profile");Resize(profilePreset);
                rootVisualElement.schedule.Execute(()=>{if(++profileStep<3)ProfileChildOpen();else ProfileOtherStates();}).ExecuteLater(250);
            }catch(Exception e){ProfileFail(e);}
        }
        void ProfileOtherStates()
        {
            try {
                Click(shell.Q("ShellBack"));ProfileCheck(!shell.HasSubpage && shell.ActiveTab==ShellTab.Menu,"Profile Back Menu");
                foreach(ProfileFriendState state in Enum.GetValues(typeof(ProfileFriendState))){
                    ProfileMount(new DemoProfileDataSource(false,state));var page=shell.Q<ProductionProfilePage>();var action=page.Q<ThemeButton>("ProfilePrimaryAction");
                    ProfileCheck(action.text==(state==ProfileFriendState.NotFriend?"Add Friend":state==ProfileFriendState.RequestSent?"Request Sent":"Friends"),"Other state "+state);
                    Click(action);ProfileCheck(page.Profile.FriendState==state && !page.Profile.IsOwnProfile && shell.ActiveProfileSection==ProfileSection.Profile,"No friend mutation");
                }
                ProfileMount(new DemoProfileDataSource(true,ProfileFriendState.NotFriend,"Alejandro Maximiliano de la Comunidad del Dominó · Demo player"));
                rootVisualElement.schedule.Execute(ProfileLongName).ExecuteLater(300);
            }catch(Exception e){ProfileFail(e);}
        }
        void ProfileLongName()
        {
            try {
                var page=shell.Q<ProductionProfilePage>();var card=page.Q("ProfilePlayerCard");var info=page.Q("ProfileInfo");
                ProfileCheck(info.layout.x>=card.Q("PlayerAvatar").layout.xMax && info.layout.xMax<=card.layout.width,"Long name avatar collision");
                var label=page.Q<Label>("ProfilePlayerName");ProfileCheck(label.layout.width<=info.layout.width && label.resolvedStyle.whiteSpace==WhiteSpace.Normal,"Long name wraps");
                ProfileBounds(page);File.AppendAllText(ProfileResult,"PRESET="+Sizes[profilePreset]+" CENTERING=PASS OVERFLOW=PASS REACHABILITY=PASS ROUTES=PASS LONG_NAME=PASS\n");
                if(++profilePreset<Sizes.Length){ProfilePreset();return;}
                File.AppendAllText(ProfileResult,"CHECKS="+profileChecks+"_PASS\nFAIL=0\n");index=1;ProfileMount(new DemoProfileDataSource());File.AppendAllText(ProfileResult,"FINAL=OWN_PROFILE_393x852\n");StartRootContentTests();
            }catch(Exception e){ProfileFail(e);}
        }
    }
}
