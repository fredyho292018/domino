using System;
using System.Collections;
using System.Collections.Generic;
using Domino.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    // Presentation only: candidates, comparison, RNG and winner belong to StarterSelection.
    public sealed class StarterTilePresentation : MonoBehaviour
    {
        BoardView board;
        Text title,instruction,player;
        bool revealing;
        float revealScale=1;
        public DominoTileView[] Tiles {get;private set;}
        public Button[] Choices {get;private set;}

        internal void Initialize(BoardView owner,DominoTileView prefab)
        {
            board=owner;
            var rect=(RectTransform)transform;
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.sizeDelta=Vector2.zero;
            title=UiKit.Label("Starter title",rect,"",new Vector2(800,76),Vector2.zero,34,DominoVisualTheme.Text);
            instruction=UiKit.Label("Starter instruction",rect,"",new Vector2(800,54),Vector2.zero,26,DominoVisualTheme.SecondaryText);
            player=UiKit.Label("Selecting player",rect,"",new Vector2(800,54),Vector2.zero,22,DominoVisualTheme.Accent);
            Tiles=new DominoTileView[2];Choices=new Button[2];
            for(int i=0;i<2;i++) {
                var tile=Instantiate(prefab,rect);Tiles[i]=tile;tile.name="Starter tile "+i;
                tile.Initialize(default,false,true);tile.SetCompactBack(false);
                // Transparent native hit target uses the actual tile mesh, never a button panel.
                var button=tile.gameObject.AddComponent<Button>();button.targetGraphic=tile.GetComponent<DominoFace>();
                button.transition=Selectable.Transition.None;Choices[i]=button;
            }
            gameObject.SetActive(false);
        }
        public void Show() {gameObject.SetActive(true);board.SetStarterMode(true);ResetTiles();}
        public void Hide() {board.SetStarterMode(false);gameObject.SetActive(false);}
        public void ResetTiles()
        {
            revealing=false;revealScale=1;
            foreach(var tile in Tiles){tile.Orient(default);tile.Conceal();tile.Select(false);tile.Rect.localScale=Vector3.one;}
        }
        public void Prompt(int seat,int unavailable,Action<int> pick)
        {
            DominoLocalization.Set(title,"duel.choose_starter");
            DominoLocalization.Set(instruction,"duel.choose_tile");instruction.gameObject.SetActive(true);
            DominoLocalization.Set(player,"duel.handoff",seat+1);player.gameObject.SetActive(true);
            for(int i=0;i<2;i++) {
                int index=i;Choices[i].onClick.RemoveAllListeners();Choices[i].interactable=i!=unavailable;
                Choices[i].onClick.AddListener(()=>{if(Choices[index].interactable)pick(index);});
            }
        }
        public void Highlight(int index)
        {
            Tiles[index].Select(true);
            foreach(var button in Choices)button.interactable=false;
        }
        public IEnumerator Reveal(int firstIndex,DominoTile first,DominoTile second)
        {
            revealing=true;bool shown=false;
            for(float elapsed=0;elapsed<.44f;elapsed+=Time.unscaledDeltaTime) {
                float t=elapsed/.44f;revealScale=Mathf.Max(.035f,Mathf.Abs(Mathf.Cos(t*Mathf.PI)));
                if(t>=.5f&&!shown) {
                    Tiles[firstIndex].Orient(first);Tiles[1-firstIndex].Orient(second);
                    foreach(var tile in Tiles)tile.Reveal();shown=true;
                }
                yield return null;
            }
            Tiles[firstIndex].Orient(first);Tiles[1-firstIndex].Orient(second);
            foreach(var tile in Tiles)tile.Reveal();
            revealScale=1;revealing=false;
        }
        public void Result(bool complete,int winner)
        {
            if(complete)DominoLocalization.Set(title,"duel.starts",winner+1);
            else DominoLocalization.Set(title,"duel.repeat");
            instruction.gameObject.SetActive(false);player.gameObject.SetActive(false);
        }
        void LateUpdate()
        {
            board.HideStarterChrome();
            var area=((RectTransform)transform).rect;
            float width=area.width,height=area.height;
            title.rectTransform.sizeDelta=new Vector2(width*.84f,76);
            instruction.rectTransform.sizeDelta=new Vector2(width*.84f,54);
            player.rectTransform.sizeDelta=new Vector2(width*.84f,54);
            title.rectTransform.anchoredPosition=new Vector2(0,height*.31f);
            instruction.rectTransform.anchoredPosition=new Vector2(0,height*.31f-66);
            player.rectTransform.anchoredPosition=new Vector2(0,height*.31f-120);
            float scale=Mathf.Min((board.IsPortrait?2.1f:1.16f)*1.45f,width/360,height/340);
            float lerp=1-Mathf.Exp(-18*Time.unscaledDeltaTime);
            for(int i=0;i<2;i++) {
                var tile=Tiles[i];float selected=tile.Selected?1.07f:1;
                var target=new Vector2((i==0?-1:1)*width*.20f,-height*.07f+(tile.Selected?14:0));
                tile.Rect.anchoredPosition=Vector2.Lerp(tile.Rect.anchoredPosition,target,lerp);
                var size=Vector3.one*(scale*selected);
                if(revealing)size.y*=revealScale;
                tile.Rect.localScale=revealing?size:Vector3.Lerp(tile.Rect.localScale,size,lerp);
            }
        }
    }

    public sealed partial class BoardView
    {
        public StarterTilePresentation StarterView {get;private set;}
        readonly Dictionary<GameObject,bool> starterChrome=new();
        public StarterTilePresentation GetStarterView()
        {
            if(!StarterView) {
                var root=UiKit.Rect("Starter selection",BoardSurface,Vector2.zero,Vector2.zero);
                StarterView=root.gameObject.AddComponent<StarterTilePresentation>();StarterView.Initialize(this,tilePrefab);
            }
            return StarterView;
        }
        internal void SetStarterMode(bool active)
        {
            if(active&&starterChrome.Count==0) {
                foreach(string name in new[]{"Score surface","Empty board","Prompt","Turn notice","Play","Restart","Menu","Client menu"}) {
                    var item=content.Find(name);if(item)starterChrome[item.gameObject]=item.gameObject.activeSelf;
                }
                foreach(var p in players)starterChrome[p.gameObject]=p.gameObject.activeSelf;
                SetInteraction(false,false);HideStarterChrome();
            }
            if(!active) {foreach(var item in starterChrome)if(item.Key)item.Key.SetActive(item.Value);starterChrome.Clear();}
        }
        internal void HideStarterChrome(){foreach(var item in starterChrome)if(item.Key)item.Key.SetActive(false);}
    }
}
