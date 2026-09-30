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
        const string MenuResult="Library/UI02B2.result.txt";
        int menuPreset,menuRoute,menuChecks;
        void MenuCheck(bool ok,string message){if(!ok)throw new Exception(message);menuChecks++;}
        void MenuFail(Exception e){File.AppendAllText(MenuResult,"FAIL="+e.Message+"\n");Debug.LogException(e);}
        void StartMenuTests(){menuPreset=menuChecks=0;File.WriteAllText(MenuResult,"RUNNING\n");MenuMount();}
        void MenuMount(){index=menuPreset;Mount(false);shell.Select(ShellTab.Menu);rootVisualElement.schedule.Execute(MenuGeometry).ExecuteLater(300);}
        void MenuGeometry()
        {
            try {
                var page=(ProductionMenuPage)shell.Pages[4];var body=page.Body;
                var rows=body.Children().OfType<ThemeButton>().Where(x=>x.name.StartsWith("MenuRow")).ToArray();
                var expected=new[]{"Friends","Messages","Stats","Coach","Theme","Membership","Settings","Help & Support"};
                MenuCheck(rows.Length==8,"Eight rows");
                MenuCheck(rows.Select(x=>x.Q<Label>("PrimaryLabel").text).SequenceEqual(expected),"Exact row order");
                MenuCheck(body.Children().OfType<Label>().Where(x=>x.name.StartsWith("MenuGroup")).Select(x=>x.text).SequenceEqual(new[]{"SOCIAL","ACTIVITY","PERSONALIZATION","APP"}),"Exact groups");
                MenuCheck(body.Children().Where(x=>x.name.StartsWith("MenuGroup")||x.name.StartsWith("MenuRow")).Select(x=>x.name).SequenceEqual(new[]{"MenuGroup0","MenuRowFriends","MenuRowMessages","MenuGroup1","MenuRowStats","MenuRowCoach","MenuGroup2","MenuRowTheme","MenuRowMembership","MenuGroup3","MenuRowSettings","MenuRowSupport"}),"Group membership");
                MenuCheck(page.Q<Label>("MenuEyebrow").text=="YOUR CORNER" && page.Q<Label>("MenuTitle").text=="Menu","Heading");
                MenuCheck(page.Q<Label>("MenuDisplayName").text=="Alex \u00b7 Demo player" && page.Q<Label>("MenuMembership").text=="FREE \u00b7 Cuban Domino Club","Profile text");
                MenuCheck(page.Q<Image>("MenuAvatar").vectorImage!=null && page.Q<Image>("MenuAvatar").layout.width>0,"Avatar");
                MenuCheck(page.Q("MenuProfile").layout.height>=44,"Profile touch");
                MenuCheck(shell.Q("ShellBack")==null && shell.Q<ThemeButton>("TabMenu").Selected,"Root tab");
                float safeLeft=shell.resolvedStyle.paddingLeft,safeWidth=shell.layout.width-safeLeft-shell.resolvedStyle.paddingRight;
                MenuCheck(Mathf.Abs(LogicalX(body,shell)+body.layout.width/2-safeLeft-safeWidth/2)<1 && body.layout.width<=620.1f,"Menu centered");
                MenuCheck(body.resolvedStyle.paddingLeft==24 && body.resolvedStyle.paddingRight==24,"Inner margins");
                float end=Mathf.Max(0,page.contentContainer.layout.height-page.contentViewport.layout.height);
                foreach(var child in body.Children())MenuCheck(child.layout.x>=0 && child.layout.xMax<=body.layout.width+.1f && child.layout.yMax<=end+page.contentViewport.layout.height+1,"Reachable bounds "+child.name);
                foreach(var row in rows){
                    MenuCheck(Mathf.Abs(row.layout.height-56)<.1f,"Row touch "+row.name);
                    MenuCheck(row.Q<Image>("MenuRowIcon").vectorImage!=null && row.Q<Image>("MenuRowChevron").vectorImage!=null,"Vector assets");
                    foreach(var part in new[]{"MenuRowIcon","PrimaryLabel","MenuRowChevron"})MenuCheck(Mathf.Abs(LogicalX(row.Q(part),body)-LogicalX(rows[0].Q(part),body))<.1f,"Aligned "+part);
                    MenuCheck(row.Q<Label>("PrimaryLabel").resolvedStyle.unityTextAlign==TextAnchor.MiddleLeft,"Left label");
                }
                MenuCheck(page.Query<TextElement>().ToList().All(x=>x.text.IndexOfAny(new[]{'\u00c2','\u00c3','\u00e2','\ufffd'})<0),"Menu UTF8");
                float navY=shell.BottomNavigation.layout.y;page.scrollOffset=new Vector2(0,end);
                MenuCheck(Mathf.Abs(page.scrollOffset.y-end)<1,"Scroll end");
                MenuCheck(shell.BottomNavigation.layout.y==navY && shell.BottomNavigation.layout.yMax<=Sizes[index].y-24+.1f,"Fixed bottom nav");
                if(index==0)MenuCheck(end>0,"Small phone scroll required");
                page.scrollOffset=Vector2.zero;menuRoute=0;MenuRouteOpen();
            }catch(Exception e){MenuFail(e);}
        }
        void MenuRouteOpen()
        {
            try {
                var destination=(MenuDestination)menuRoute;
                Click(shell.Q(menuRoute==0?"MenuProfile":"MenuRow"+destination));
                MenuCheck(shell.ActiveMenuDestination==destination,"Click destination");
                Resize((menuPreset+1)%Sizes.Length);
                rootVisualElement.schedule.Execute(()=>MenuRouteBack(destination)).ExecuteLater(250);
            }catch(Exception e){MenuFail(e);}
        }
        void MenuRouteBack(MenuDestination destination)
        {
            try {
                MenuCheck(shell.ActiveTab==ShellTab.Menu && shell.ActiveMenuDestination==destination,"Subpage resize preserved");
                MenuCheck(shell.Q<Label>("SubpageTitle").text==ProductionMenuPage.Title(destination) && (destination==MenuDestination.Profile?shell.Q<ProductionProfilePage>()!=null:shell.Q<Label>("SubpageMessage").text=="Coming Soon"),"Placeholder content");
                var back=shell.Q<Button>("ShellBack");MenuCheck(back.layout.width>=44 && back.layout.height>=44 && back.Q<Image>().vectorImage!=null,"Arrow Back touch");
                Click(back);MenuCheck(!shell.HasSubpage && shell.ActiveTab==ShellTab.Menu,"Back Menu");
                Resize(menuPreset);
                rootVisualElement.schedule.Execute(()=>{
                    try {
                        if(++menuRoute<9){MenuRouteOpen();return;}
                        File.AppendAllText(MenuResult,"PRESET="+Sizes[menuPreset]+" CENTERING=PASS OVERFLOW=PASS REACHABILITY=PASS ROUTES=9/9_PASS\n");
                        if(++menuPreset<Sizes.Length){MenuMount();return;}
                        File.AppendAllText(MenuResult,"CHECKS="+menuChecks+"_PASS\nFAIL=0\n");index=1;Mount(false);shell.Select(ShellTab.Menu);
                        File.AppendAllText(MenuResult,"FINAL=MENU_393x852\n");StartProfileTests();
                    }catch(Exception e){MenuFail(e);}
                }).ExecuteLater(250);
            }catch(Exception e){MenuFail(e);}
        }
    }
}
