using System;
using System.IO;
using System.Linq;
using Domino.UI.AppShell;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.Editor
{
    public sealed partial class ProductionShellPreview
    {
        const string RootResult="Library/UI02B4.result.txt";
        int rootCase,rootChecks,rootAction;string[] rootActions;
        ShellTab TestedRoot=>(ShellTab)(rootCase/8+1);
        void RootCheck(bool value,string message){if(!value)throw new Exception(message);rootChecks++;}
        void RootFail(Exception e){File.AppendAllText(RootResult,"FAIL="+e.Message+"\n");Debug.LogException(e);}
        void StartRootContentTests(){rootCase=rootChecks=0;File.WriteAllText(RootResult,"RUNNING\n");RootMount();}
        void RootMount(){index=rootCase%8;Mount(false);shell.Select(TestedRoot);rootVisualElement.schedule.Execute(RootGeometry).ExecuteLater(300);}
        void RootGeometry()
        {
            try {
                var page=(ProductionContentRoot)shell.Pages[(int)TestedRoot];var body=page.Body;
                RootCheck(page.Q<Label>("RootTitle").text==TestedRoot.ToString(),"Root title");
                RootCheck(shell.ActiveTab==TestedRoot && shell.Q("ShellBack")==null,"Root no Back");
                RootCheck(ReferenceEquals(page.Theme,ThemeProvider.Current),"Root theme");
                RootCheck(shell.BottomNavigation.childCount==5 && shell.BottomNavigation[2].name=="TabLearn" && page.Q("BottomNavigation")==null,"Single fixed nav");
                RootCheck(shell.BottomNavigation.Children().Cast<ThemeButton>().Count(x=>x.Selected)==1,"Single active tab");
                float safeLeft=shell.resolvedStyle.paddingLeft,safeWidth=shell.layout.width-safeLeft-shell.resolvedStyle.paddingRight;
                RootCheck(Mathf.Abs(LogicalX(body,shell)+body.layout.width/2-safeLeft-safeWidth/2)<1 && body.layout.width<=620.1f,"Centered");
                RootCheck(page.contentViewport.layout.width==page.layout.width && page.verticalScrollerVisibility==ScrollerVisibility.Hidden,"No scrollbar gutter");
                RootCheck(body.resolvedStyle.paddingLeft==24 && body.resolvedStyle.paddingRight==24,"Margins");
                float end=Mathf.Max(0,page.contentContainer.layout.height-page.contentViewport.layout.height);
                foreach(var child in body.Children())RootCheck(child.layout.x>=0 && child.layout.xMax<=body.layout.width+.1f && child.layout.yMax<=end+page.contentViewport.layout.height+1,"Reachable "+child.name);
                foreach(var button in body.Query<ThemeButton>().ToList())RootCheck(button.layout.width>=44 && button.layout.height>=44,"Touch "+button.name);
                foreach(var label in body.Query<Label>().ToList()){
                    RootCheck(LogicalX(label,body)>=0 && LogicalX(label,body)+label.layout.width<=body.layout.width+.1f,"Label horizontal bounds");
                    RootCheck(label.text.IndexOfAny(new[]{'\u00c2','\u00c3','\u00e2','\ufffd'})<0,"UTF8");
                }
                float y=shell.BottomNavigation.layout.y;page.scrollOffset=new Vector2(0,end);RootCheck(Mathf.Abs(page.scrollOffset.y-end)<1 && shell.BottomNavigation.layout.y==y,"Scroll fixed nav");page.scrollOffset=Vector2.zero;
                RootCheck(shell.PageHost.layout.y>=24 && shell.BottomNavigation.layout.yMax<=Sizes[index].y-24+.1f,"Safe area");
                if(TestedRoot!=ShellTab.Watch){
                    var cta=page.Q<Button>(TestedRoot==ShellTab.Puzzles?"StartPuzzle":"LearnContinue");RootCheck(cta.resolvedStyle.unityTextAlign==TextAnchor.MiddleCenter,"Polish CTA centered");
                    foreach(var section in body.Query<Label>().ToList().Where(x=>x.name.StartsWith("Section")))RootCheck(section.resolvedStyle.fontSize==20&&section.style.unityFont.value==ThemeProvider.Current.Typography.FontFor(TextRole.SectionTitle),"Section typography");
                    RootCheck(page.Q(TestedRoot==ShellTab.Puzzles?"DailyPuzzle":"LearnCoachCard").resolvedStyle.borderTopLeftRadius==ThemeProvider.Current.Radius.Card,"Polish card radius");
                }
                if(TestedRoot==ShellTab.Learn)foreach(var rowName in new[]{"LearnLessons","LearnPuzzles","LearnCoachGames"})RootCheck(page.Q(rowName).layout.height==56&&page.Q(rowName).Q<Image>().vectorImage!=null,"Training actionable row");
                if(TestedRoot==ShellTab.Puzzles){
                    RootCheck(page.Q("DailyPuzzle")!=null && page.Q<Label>("PuzzleDifficulty").text=="Intermediate","Daily puzzle");
                    RootCheck(page.Query<Label>("CategoryName").ToList().Select(x=>x.text).SequenceEqual(new[]{"Opening","Strategy","Counting","Endgame"}),"Categories");rootActions=new[]{"StartPuzzle"};
                }else if(TestedRoot==ShellTab.Learn){
                    var coach=((ProductionLearnPage)page).Coach;var homeCoach=new DemoHomeDataSource().Read().Coach;
                    RootCheck(coach.CoachId==homeCoach.CoachId && coach.DisplayName=="Amara" && coach.Avatar==homeCoach.Avatar && coach.Greeting==homeCoach.Greeting,"Shared coach consistency");
                    RootCheck(page.Q<Label>("LearnCoachGreeting").text=="Hola, soy Amara.\nTe ense\u00f1ar\u00e9 a jugar domin\u00f3." && page.Q("LearnCoachAvatar").Q<Image>().image!=null,"Coach content");rootActions=new[]{"LearnContinue","LearnLessons","LearnCoachGames"};
                }else{
                    RootCheck(page.Q("SectionFeatured")!=null && page.Q("SectionRecentGames")!=null,"Watch sections");
                    RootCheck(page.Query<ThemeButton>("WatchCard").ToList().Count==3 && page.Query<Label>("WatchMode").ToList().Select(x=>x.text).Distinct().Count()==2,"Watch demo cards");rootActions=new[]{"WatchCard"};
                }
                Resize((rootCase%8+1)%8);RootCheck(shell.ActiveTab==TestedRoot && !shell.HasSubpage,"Root resize");Resize(rootCase%8);rootAction=0;
                rootVisualElement.schedule.Execute(RootOpenAction).ExecuteLater(250);
            }catch(Exception e){RootFail(e);}
        }
        void RootOpenAction()
        {
            try {
                Click(shell.Q(rootActions[rootAction]));var expected=TestedRoot==ShellTab.Puzzles?RootDestination.Puzzle:TestedRoot==ShellTab.Watch?RootDestination.WatchGame:rootActions[rootAction]=="LearnCoachGames"?RootDestination.CoachGames:RootDestination.Lesson;
                RootCheck(shell.ActiveRootDestination==expected,"Action destination");Resize((rootCase%8+1)%8);
                rootVisualElement.schedule.Execute(()=>RootBack(expected)).ExecuteLater(250);
            }catch(Exception e){RootFail(e);}
        }
        void RootBack(RootDestination expected)
        {
            try {
                RootCheck(shell.ActiveRootDestination==expected && shell.ActiveTab==TestedRoot,"Subpage resize");
                string title=expected==RootDestination.CoachGames?"Coach Games":expected==RootDestination.WatchGame?"Watch Game":expected.ToString();
                RootCheck(shell.Q<Label>("SubpageTitle").text==title && shell.Q<Label>("SubpageMessage").text=="Coming Soon","Placeholder");
                var back=shell.Q<Button>("ShellBack");RootCheck(back.layout.width>=44 && back.layout.height>=44 && back.Q<Image>().vectorImage!=null,"Arrow Back");Click(back);
                RootCheck(shell.ActiveTab==TestedRoot && !shell.HasSubpage,"Back root");Resize(rootCase%8);
                rootVisualElement.schedule.Execute(()=>{
                    try {
                        if(++rootAction<rootActions.Length){RootOpenAction();return;}
                        shell.Select(ShellTab.Home);Click(shell.Q("HomeContinueLearning"));RootCheck(shell.ActiveTab==ShellTab.Learn,"Home to Learn");
                        Click(shell.Q("LearnPuzzles"));RootCheck(shell.ActiveTab==ShellTab.Puzzles,"Learn to Puzzles");
                        foreach(var tab in new[]{ShellTab.Puzzles,ShellTab.Learn,ShellTab.Watch,ShellTab.Menu,ShellTab.Home}){Click(shell.Q("Tab"+tab));RootCheck(shell.ActiveTab==tab && shell.BottomNavigation.Children().Cast<ThemeButton>().Count(x=>x.Selected)==1,"Cross-root navigation");}
                        File.AppendAllText(RootResult,TestedRoot+" "+Sizes[rootCase%8]+" CENTERING=PASS OVERFLOW=PASS REACHABILITY=PASS ROUTES=PASS\n");
                        if(++rootCase<24){RootMount();return;}
                        File.AppendAllText(RootResult,"CHECKS="+rootChecks+"_PASS\nFAIL=0\n");index=1;Mount(false);shell.Select(ShellTab.Puzzles);File.AppendAllText(RootResult,"FINAL=PUZZLES_393x852\n");
                    }catch(Exception e){RootFail(e);}
                }).ExecuteLater(250);
            }catch(Exception e){RootFail(e);}
        }
    }
}
