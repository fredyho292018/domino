using System;
using System.IO;
using System.Linq;
using Domino.AppShellMock;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor
{
    public static class MockPremiumValidation
    {
        const string Request="Library/AppShellPremium.request",Result="Library/AppShellPremium.result.txt";
        static MockShellPreview window;static int device,checks;
        [InitializeOnLoadMethod] static void Register(){EditorApplication.update+=Poll;}
        static void Poll(){if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;try{File.Delete(Request);}catch(IOException){return;}Run();}
        [MenuItem("Domino/App Shell Mock/Validate Premium selector")]
        public static void Run(){device=0;checks=0;File.WriteAllText(Result,"RUNNING\n");MockShellPreview.OpenMembership();window=EditorWindow.GetWindow<MockShellPreview>();Next();}
        static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
        static void Tap(Button button){using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}}
        static void Next(){window.SetPreviewDevice(device);window.rootVisualElement.schedule.Execute(Validate).ExecuteLater(600);}
        static void Validate()
        {
            try
            {
                var view=window.rootVisualElement.Q<MockShellView>();
                var tabs=view.Q("PremiumTabs").Children().OfType<Button>().ToArray();
                Check(tabs.Length==4,"Four tabs");
                foreach(var tab in tabs){Check(tab.Q<Image>().vectorImage!=null,"Plan vector");Check(tab.layout.width>=70&&tab.worldBound.xMax<=view.worldBound.xMax,"Tab width and bounds");}
                var featureRows=view.Q("PremiumFeatures").Children().ToArray();
                float iconX=featureRows[0].Q<Image>().worldBound.xMin,labelX=featureRows[0].Q<Label>().worldBound.xMin,checkX=featureRows[0].Children().Last().worldBound.xMax;
                foreach(var row in featureRows)Check(Mathf.Abs(row.Q<Image>().worldBound.xMin-iconX)<1&&Mathf.Abs(row.Q<Label>().worldBound.xMin-labelX)<1&&Mathf.Abs(row.Children().Last().worldBound.xMax-checkX)<1,"Feature alignment");
                foreach(var tab in tabs)Check(Mathf.Abs(tab.worldBound.width-tabs[0].worldBound.width)<=1.01f&&Mathf.Abs(tab.Q<Image>().worldBound.center.x-tab.Q<Label>().worldBound.center.x)<=1.01f,"Equal tabs and icon label centering");
                foreach(MockMembershipPlan plan in Enum.GetValues(typeof(MockMembershipPlan)))
                foreach(MockBillingPeriod period in Enum.GetValues(typeof(MockBillingPeriod)))
                {
                    Tap(view.Q<Button>("PremiumTab_"+plan));Tap(view.Q<Button>("Billing_"+period));
                    Check(view.State.selectedMembershipPlan==plan&&view.State.selectedBillingPeriod==period,"Selection event");
                    var rows=view.Q("PremiumFeatures").Children().ToArray();
                    Check(rows.Select(r=>r.Q<Label>().text).SequenceEqual(MockMembershipPricing.Features(plan)),"Exact features");
                    Check(rows.All(r=>r.Query<Image>().ToList().All(i=>i.vectorImage!=null)),"Feature vectors");
                    Check(view.Q("PremiumTabs").Children().Count(t=>t.style.borderBottomColor.value==MockShellTheme.Primary)==1,"One selected plan");
                    Check(view.Q("PremiumBilling").Children().Count(t=>t.style.borderTopColor.value==MockShellTheme.Primary)==1,"One billing selection");
                    bool yearly=period==MockBillingPeriod.YEARLY;
                    Check(view.Q<Label>("PremiumPrice").text==MockMembershipPricing.Money(yearly?MockMembershipPricing.MonthlyEquivalent(plan):MockMembershipPricing.Monthly(plan),yearly)+" / month","Effective price");
                    Check((view.Q("PremiumSavings")!=null)==yearly,"Savings only yearly");
                    if(yearly){Check(view.Q<Label>("PremiumSavings").text.Contains(MockMembershipPricing.Money(MockMembershipPricing.Savings(plan))),"Savings amount");Check(view.Q<Label>("PremiumBillingExplanation").text=="billed annually at "+MockMembershipPricing.Price(plan,period),"Annual charge explicit");}
                    Check(string.Join("\n",view.Q("MembershipSummary").Query<Label>().ToList().Select(l=>l.text))==view.State.MembershipSummary,"Summary follows state");
                    Check(view.Q("PremiumBillingColumn").Query<Label>().ToList().All(l=>l.style.unityTextAlign.value==TextAnchor.MiddleCenter),"All billing labels centered");
                    Check(view.Query<Label>().ToList().All(l=>!l.text.Contains("\u00C2")&&!l.text.Contains("\u00C3")),"No mojibake");
                    if(yearly)Check(view.Q<Label>("PremiumSavings").text=="SAVE "+MockMembershipPricing.Money(MockMembershipPricing.Savings(plan))+" \u00B7 "+MockMembershipPricing.SavingsPercent(plan)+"%","Exact middle dot");
                    Check(view.Q<Button>("Billing_"+period).style.unityFont.value.name=="SourceSans3-Bold","Selected billing bold");
                    Check(view.Q("PremiumBilling").style.height.value.value==52,"Compact billing container");
                    var colors=new[]{new Color32(66,165,245,255),new Color32(192,192,192,255),new Color32(244,197,66,255),new Color32(113,168,75,255)};
                    var planTabs=view.Q("PremiumTabs").Children().OfType<Button>().ToArray();
                    for(int t=0;t<4;t++){Check(planTabs[t].Q<Image>().tintColor==(Color)colors[t],"Plan icon tint");Check(planTabs[t].Q<Label>().style.unityTextAlign.value==TextAnchor.MiddleCenter,"Centered plan label");}
                    Check(view.Query<Button>().ToList().Any(b=>b.text=="Try 7 Days for $0"),"Trial CTA");
                    Check(!view.Query<Button>().ToList().Any(b=>b.text=="View All Plans"),"Old CTA removed");
                }
                view.State.Start(MockEntry.EXISTING_REGISTERED);view.State.selectedMembershipPlan=MockMembershipPlan.GOLD;view.State.Tab("Menu");view.State.Go(MockPage.Membership);view.Rebuild();
                Check(view.State.selectedMembershipPlan==MockMembershipPlan.GOLD&&view.Q<Button>("BackAction")!=null,"Menu preserves plan and Back");
                Check(!view.Query<Button>().ToList().Any(b=>b.text=="Not Now"),"Menu footer context");
                view.State.Start(MockEntry.NEW_GUEST);view.State.Go(MockPage.Trial);view.Rebuild();
                Check(view.Query<Button>().ToList().Any(b=>b.text=="Not Now"),"Onboarding footer");
                File.AppendAllText(Result,"DEVICE_"+device+"=PASS\n");if(++device<3){Next();return;}
                File.AppendAllText(Result,"PREMIUM_CHECKS="+checks+"_PASS\n");MockShellPreview.OpenMembership();
            }
            catch(Exception e){File.AppendAllText(Result,"FAIL="+e.Message);Debug.LogException(e);}
        }
    }
}
