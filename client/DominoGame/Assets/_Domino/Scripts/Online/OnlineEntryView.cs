#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading.Tasks;
using Domino.Infrastructure;
using Domino.Realtime;
using Domino.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.Online
{
    // Manual diagnostic entry. Normal players use MatchmakingView.
    public sealed class OnlineEntryView : MonoBehaviour
    {
        RectTransform safe,panel;Text feedback;DominoTileView tile;PlayerView player;Action back;
        OnlineMatchClient pending;OnlineMatchController match;bool busy,closing;
        public Button CreateButton {get;private set;}
        public Button JoinButton {get;private set;}
        public Button BackButton {get;private set;}
        public InputField MatchId {get;private set;}
        public OnlineMatchController Match => match;
        public void Initialize(DominoTileView tilePrefab,PlayerView playerPrefab,Action onBack)
        {
            if(!Domino.Online.OnlineDevelopmentAccess.Allowed)throw new InvalidOperationException("Development entry unavailable");
            tile=tilePrefab;player=playerPrefab;back=onBack;
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=35;
            BoardView.ConfigureCanvas(gameObject);gameObject.AddComponent<GraphicRaycaster>();
            var bg=UiKit.Rect("Background",transform,Vector2.zero,Vector2.zero);bg.anchorMin=Vector2.zero;bg.anchorMax=Vector2.one;bg.sizeDelta=Vector2.zero;bg.gameObject.AddComponent<SoftBackdrop>();
            safe=UiKit.Rect("Safe area",transform,Vector2.zero,Vector2.zero);safe.gameObject.AddComponent<SafeArea>();
            panel=UiKit.Rect("Online entry panel",safe,new Vector2(880,820),Vector2.zero);
            UiKit.LLabel("Title",panel,"online.entry_title",new Vector2(840,110),new Vector2(0,300),36,UiKit.Cream);
            UiKit.LLabel("Development",panel,"online.development_entry",new Vector2(800,90),new Vector2(0,205),24,UiKit.Muted);
            CreateButton=UiKit.LButton("Create match",panel,"online.create",new Vector2(700,78),new Vector2(0,95),UiKit.Hex("397566"),()=>{_=EnterAsync(false);});
            UiKit.LLabel("Match ID label",panel,"online.match_id",new Vector2(700,40),new Vector2(0,10),24,UiKit.Cream);
            var field=UiKit.Panel("Match ID input",panel,new Vector2(740,72),new Vector2(0,-50),UiKit.Hex("102C2C"));field.raycastTarget=true;
            MatchId=field.gameObject.AddComponent<InputField>();MatchId.targetGraphic=field;
            MatchId.textComponent=UiKit.Label("Text",field.transform,"",new Vector2(700,65),Vector2.zero,24,UiKit.Cream,TextAnchor.MiddleLeft);
            MatchId.characterLimit=128;MatchId.lineType=InputField.LineType.SingleLine;
            JoinButton=UiKit.LButton("Join match",panel,"online.join",new Vector2(700,78),new Vector2(0,-155),UiKit.Hex("397566"),()=>{_=EnterAsync(true);});
            feedback=UiKit.Label("Feedback",panel,"",new Vector2(820,95),new Vector2(0,-265),23,UiKit.Gold);
            BackButton=UiKit.LButton("Back",panel,"menu.back",new Vector2(500,65),new Vector2(0,-370),Color.clear,Close);
        }
        public async Task EnterAsync(bool join)
        {
            if(busy||closing||match)return;
            if(ApplicationServices.Realtime?.State!=RealtimeConnectionState.CONNECTED){Message("online.network_unavailable");return;}
            if(join&&!Guid.TryParse(MatchId.text.Trim(),out _)){Message("online.invalid_id");return;}
            busy=true;Message("system.loading");
            var client=new OnlineMatchClient(ApplicationServices.OnlineApi,(IRealtimeMatchChannel)ApplicationServices.Realtime);pending=client;
            try {
                if(join)await client.JoinAsync(MatchId.text.Trim());else await client.CreateAsync();
                if(closing){client.Dispose();return;}
                pending=null;
                match=new GameObject("Online match presentation").AddComponent<OnlineMatchController>();
                match.Closed+=MatchClosed;match.Initialize(client,tile,player);
                MatchId.text=client.Snapshot.MatchId;
                if(client.Snapshot.Phase=="WAITING_FOR_PLAYER") {
                    Message("online.waiting");client.Changed+=WaitingChanged;
                    CreateButton.gameObject.SetActive(false);JoinButton.gameObject.SetActive(false);
                } else gameObject.SetActive(false);
            } catch(Exception e) {
                client.Dispose();pending=null;
                if(!closing)Message(e is OnlineEntryException failure?failure.LocalizationKey:"online.join_failed");
            } finally {busy=false;}
        }
        void WaitingChanged(){if(match&&match.Client.Snapshot.Phase!="WAITING_FOR_PLAYER")gameObject.SetActive(false);}
        void Message(string key)=>DominoLocalization.Set(feedback,key);
        void LateUpdate(){if(!panel)return;panel.localScale=Vector3.one*Mathf.Min(safe.rect.width/930,safe.rect.height/920);CreateButton.interactable=!busy;JoinButton.interactable=!busy;MatchId.interactable=!busy;}
        void MatchClosed(){match=null;if(!closing)Close();}
        public void Close(){if(closing)return;closing=true;pending?.Dispose();if(match){match.Client.Changed-=WaitingChanged;match.Closed-=MatchClosed;Destroy(match.gameObject);}back?.Invoke();Destroy(gameObject);}
        void OnDestroy(){pending?.Dispose();if(match){match.Client.Changed-=WaitingChanged;match.Closed-=MatchClosed;Destroy(match.gameObject);}}
    }
}
#endif
