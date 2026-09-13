using System;
using System.Collections;
using Domino.Game;
using Domino.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    public sealed class SharedDevicePrompt : MonoBehaviour
    {
        RectTransform body;
        public Button ContinueButton { get; private set; }
        public Button FirstChoice { get; private set; }
        public Button SecondChoice { get; private set; }
        public bool AwaitingInput { get; private set; }
        void Awake()
        {
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            BoardView.ConfigureCanvas(gameObject);gameObject.AddComponent<GraphicRaycaster>();
            var backdrop=UiKit.Panel("Private handoff",transform,Vector2.zero,Vector2.zero,UiKit.Hex("102C2B")).rectTransform;
            backdrop.anchorMin=Vector2.zero;backdrop.anchorMax=Vector2.one;backdrop.sizeDelta=Vector2.zero;
            var safe=UiKit.Rect("Safe area",transform,Vector2.zero,Vector2.zero);safe.gameObject.AddComponent<SafeArea>();
            body=UiKit.Rect("Choices",safe,new Vector2(900,600),Vector2.zero);
        }
        void LateUpdate()
        {
            var safe=(RectTransform)body.parent;
            body.localScale=Vector3.one*Mathf.Min(safe.rect.width/960,safe.rect.height/700);
        }
        void Clear()
        {
            foreach(Transform child in body) {child.gameObject.SetActive(false);Destroy(child.gameObject);}
            ContinueButton=null;FirstChoice=null;SecondChoice=null;
        }
        public IEnumerator Handoff(int seat)
        {
            gameObject.SetActive(true);Clear();AwaitingInput=true;
            DominoLocalization.Set(UiKit.Label("Player",body,"",new Vector2(860,85),new Vector2(0,100),36,UiKit.Cream),"duel.handoff",seat+1);
            UiKit.LLabel("Privacy",body,"duel.privacy",new Vector2(800,100),Vector2.zero,24,UiKit.Muted);
            ContinueButton=UiKit.LButton("Continue",body,"game.continue",new Vector2(360,85),new Vector2(0,-160),UiKit.Hex("397566"),()=>AwaitingInput=false);
            while(AwaitingInput)yield return null;
            gameObject.SetActive(false);
        }
        public IEnumerator ChooseStarter(StarterSelection selection)
        {
            while(!selection.Complete) {
                int seat=selection.WaitingForGuess?selection.GuesserSeat:selection.NextSelectionSeat;
                yield return Handoff(seat);gameObject.SetActive(true);Clear();
                UiKit.LLabel("Title",body,"duel.choose_starter",new Vector2(880,65),new Vector2(0,200),34,UiKit.Cream);
                UiKit.LLabel("Instruction",body,selection.WaitingForGuess?"duel.guess":"duel.choose_tile",new Vector2(840,70),new Vector2(0,95),26,UiKit.Muted);
                bool chosen=false;AwaitingInput=true;
                bool guess=selection.WaitingForGuess;
                Action<int> pick=index=> {if(guess?selection.Guess(seat,index==0):selection.Choose(seat,index)){chosen=true;AwaitingInput=false;}};
                FirstChoice=UiKit.LButton("Choice 1",body,guess?"duel.even":"duel.tile_one",new Vector2(340,115),new Vector2(-190,-75),UiKit.Hex("397566"),()=>pick(0));
                SecondChoice=UiKit.LButton("Choice 2",body,guess?"duel.odd":"duel.tile_two",new Vector2(340,115),new Vector2(190,-75),UiKit.Hex("397566"),()=>pick(1));
                if(!guess && selection.SelectedCandidateIndex>=0) { FirstChoice.interactable=selection.SelectedCandidateIndex!=0;SecondChoice.interactable=selection.SelectedCandidateIndex!=1; }
                while(!chosen)yield return null;
                if(selection.FirstRevealed.HasValue) {
                    Clear();
                    string tiles=selection.FirstRevealed.Value.ToString();
                    if(selection.SecondRevealed.HasValue)tiles+="   /   "+selection.SecondRevealed.Value;
                    UiKit.Label("Revealed selection",body,tiles,new Vector2(850,90),new Vector2(0,90),40,UiKit.Cream);
                    var message=UiKit.Label("Result",body,"",new Vector2(850,90),Vector2.zero,30,UiKit.Gold);
                    if(selection.Complete)DominoLocalization.Set(message,"duel.starts",selection.WinnerSeat+1);
                    else DominoLocalization.Set(message,"duel.repeat");
                    AwaitingInput=true;
                    ContinueButton=UiKit.LButton("Continue",body,"game.continue",new Vector2(360,85),new Vector2(0,-160),UiKit.Hex("397566"),()=>AwaitingInput=false);
                    while(AwaitingInput)yield return null;
                }
            }
            gameObject.SetActive(false);
        }
    }
}
