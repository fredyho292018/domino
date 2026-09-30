using System;
using System.IO;
using System.Linq;
using Domino.AppShellMock;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.Editor
{
    public static class MockCoachValidation
    {
        const string Request="Library/AppShellCoaches.request",Result="Library/AppShellCoaches.result.txt";
        static MockShellPreview window;static int device,checks;
        [InitializeOnLoadMethod] static void Register(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(Request);Run();
        }
        [MenuItem("Domino/App Shell Mock/Validate coach cards")]
        public static void Run()
        {
            device=0;checks=0;File.WriteAllText(Result,"RUNNING\n");
            MockShellPreview.OpenCoaches();window=EditorWindow.GetWindow<MockShellPreview>();Next();
        }
        static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        static void Next(){window.SetPreviewDevice(device);window.rootVisualElement.schedule.Execute(Validate).ExecuteLater(600);}
        static void Validate()
        {
            try
            {
                var view=window.rootVisualElement.Q<MockShellView>();var grid=view.Q("CoachGrid");
                var cards=grid.Children().OfType<Button>().ToArray();Check(cards.Length==10,"Ten coaches");
                var next=view.Query<Button>().ToList().First(b=>b.text=="Continue");
                foreach(var card in cards)
                {
                    var id=card.name.Substring("CoachCard_".Length);var coach=MockCatalog.Coaches.First(c=>c.Id==id);
                    Check(card.Q<Image>().image!=null,"Portrait "+id);
                    Check(card.Q<Label>("CoachName").text==coach.DisplayName,"Plain name");
                    Check(card.style.unityFont.value.name=="SourceSans3-Semibold","Name weight 600");
                    Check(card.style.backgroundColor.value==MockShellTheme.Surface,"Dark surface");
                    var avatar=card.Q("CoachPortrait");
                    Check(avatar.layout.width>=90&&Mathf.Abs(avatar.worldBound.width-avatar.worldBound.height)<1.1f,"Large circular avatar");
                    Check(avatar.worldBound.xMin>=card.worldBound.xMin&&avatar.worldBound.xMax<=card.worldBound.xMax+1,"Avatar bounds");
                    using(var e=NavigationSubmitEvent.GetPooled()){e.target=card;card.SendEvent(e);}
                    Check(view.State.SelectedCoachId==id,"Entire card selects "+id);
                    Check(view.Q<Label>("CoachGreeting").text==coach.ShortGreeting,"Greeting");
                    Check(card.style.borderTopColor.value==MockShellTheme.Primary,"Selected border");
                    Check(next.enabledSelf,"Continue enabled");
                    Check(cards.Count(c=>c.style.borderTopColor.value==MockShellTheme.Primary)==1,"Single selection");
                }
                for(int i=0;i<10;i+=2)Check(Mathf.Abs(cards[i].worldBound.yMin-cards[i+1].worldBound.yMin)<1,"Two columns");
                Check(next.style.backgroundColor.value==MockShellTheme.Primary&&next.style.color.value==MockShellTheme.Text,"Continue colors");
                Check(next.style.unityFont.value.name=="SourceSans3-Bold","Continue weight 700");
                File.AppendAllText(Result,"DEVICE_"+device+"=PASS\n");
                if(++device<3){Next();return;}
                File.AppendAllText(Result,"COACH_CHECKS="+checks+"_PASS\n");MockShellPreview.OpenCoaches();
            }
            catch(Exception e){File.AppendAllText(Result,"FAIL="+e.Message);Debug.LogException(e);}
        }
    }
}
