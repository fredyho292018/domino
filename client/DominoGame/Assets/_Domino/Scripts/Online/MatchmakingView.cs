using System;
using System.Threading.Tasks;
using Domino.Infrastructure;
using Domino.Realtime;
using Domino.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.Online
{
    public sealed class MatchmakingView : MonoBehaviour
    {
        RectTransform safe,panel;Text status,elapsed;Button search,cancel;
        DominoTileView tile;PlayerView player;Action back;
        OnlineMatchController match;bool closing,entering,checking,resuming;float started;
        RealtimeConnectionState connection;
        public MatchmakingClient Client {get;private set;}
        public OnlineMatchController Match=>match;
        public void Initialize(DominoTileView tilePrefab,PlayerView playerPrefab,Action onBack)
        {
            tile=tilePrefab;player=playerPrefab;back=onBack;
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=35;
            BoardView.ConfigureCanvas(gameObject);gameObject.AddComponent<GraphicRaycaster>();
            var bg=UiKit.Rect("Background",transform,Vector2.zero,Vector2.zero);bg.anchorMin=Vector2.zero;bg.anchorMax=Vector2.one;bg.sizeDelta=Vector2.zero;bg.gameObject.AddComponent<SoftBackdrop>();
            safe=UiKit.Rect("Safe area",transform,Vector2.zero,Vector2.zero);safe.gameObject.AddComponent<SafeArea>();
            panel=UiKit.Rect("Find opponent",safe,new Vector2(880,700),Vector2.zero);
            UiKit.LLabel("Title",panel,"matchmaking.title",new Vector2(820,110),new Vector2(0,220),38,UiKit.Cream);
            status=UiKit.Label("Status",panel,"",new Vector2(780,130),new Vector2(0,85),28,UiKit.Gold);
            elapsed=UiKit.Label("Elapsed",panel,"",new Vector2(780,60),new Vector2(0,-35),23,UiKit.Muted);
            search=UiKit.LButton("Search",panel,"matchmaking.search",new Vector2(680,80),new Vector2(0,-145),UiKit.Hex("397566"),()=>{started=Time.realtimeSinceStartup;_=Search();});
            cancel=UiKit.LButton("Cancel",panel,"system.cancel",new Vector2(500,65),new Vector2(0,-265),Color.clear,()=>{_=Cancel();});
            Client=new MatchmakingClient(ApplicationServices.OnlineApi,(IRealtimeMatchChannel)ApplicationServices.Realtime);
            Client.Changed+=Render;Client.Diagnostic+=Diagnostic;connection=ApplicationServices.Realtime.State;ApplicationServices.Realtime.Changed+=ConnectionChanged;
            started=Time.realtimeSinceStartup;_=Recover();
        }
        void Diagnostic(string category)=>Debug.LogWarning("[MATCHMAKING] failure category="+category);
        async Task Recover(){checking=true;resuming=true;Render();if(connection==RealtimeConnectionState.CONNECTED)await Client.RecoverAsync();else Client.ConnectionLost();checking=false;if(Client.MatchId==null)resuming=false;Render();}
        async Task Search()
        {
            if(checking||entering)return;
            checking=true;Render();
            try {
            if(ApplicationServices.Realtime.State!=RealtimeConnectionState.CONNECTED){Client.ConnectionLost();return;}
            await Client.RecoverAsync();
            if(!closing&&Client.State==MatchmakingState.IDLE)await Client.JoinAsync();
            }finally{checking=false;Render();}
        }
        void ConnectionChanged()
        {
            if(closing||match)return;
            if(connection==ApplicationServices.Realtime.State)return;
            connection=ApplicationServices.Realtime.State;
            if(ApplicationServices.Realtime.State==RealtimeConnectionState.CONNECTED)_=Recover();
            else Client.ConnectionLost();
        }
        void Render()
        {
            if(closing)return;
            var state=Client.State;
            DominoLocalization.Set(status,checking&&state==MatchmakingState.IDLE?"matchmaking.recovering":"matchmaking."+state.ToString().ToLowerInvariant());
            if(resuming&&Client.MatchId!=null&&state!=MatchmakingState.FAILED)DominoLocalization.Set(status,"matchmaking.resuming");
            search.gameObject.SetActive(state==MatchmakingState.IDLE||state==MatchmakingState.FAILED);
            search.interactable=!checking;
            cancel.interactable=!checking&&Client.MatchId==null&&state!=MatchmakingState.CANCELLING;
            if(state==MatchmakingState.MATCH_FOUND&&!entering&&!match)_=Enter();
        }
        async Task Enter()
        {
            entering=true;
            var online=new OnlineMatchClient(ApplicationServices.OnlineApi,(IRealtimeMatchChannel)ApplicationServices.Realtime);
            try {
                await Task.Delay(650);
                if(closing){online.Dispose();return;}
                Client.BeginEntering();
                await online.LoadAssignedAsync(Client.MatchId);
                if(closing){online.Dispose();return;}
                match=new GameObject("Online match presentation").AddComponent<OnlineMatchController>();
                match.Closed+=MatchClosed;match.Initialize(online,tile,player);gameObject.SetActive(false);
            } catch {
                online.Dispose();if(!closing)Client.EntryFailed();
            } finally {entering=false;}
        }
        async Task Cancel()
        {
            await Client.CancelAsync();
            if(!closing&&Client.State==MatchmakingState.IDLE)Close();
        }
        void MatchClosed(){match=null;Close();}
        public void Close(){if(closing)return;closing=true;back?.Invoke();Destroy(gameObject);}
        void LateUpdate()
        {
            panel.localScale=Vector3.one*Mathf.Min(safe.rect.width/930,safe.rect.height/800);
            bool waiting=Client.State==MatchmakingState.SEARCHING||Client.State==MatchmakingState.JOINING;
            elapsed.gameObject.SetActive(waiting);
            if(waiting)DominoLocalization.Set(elapsed,"matchmaking.elapsed",Mathf.FloorToInt(Time.realtimeSinceStartup-started));
        }
        void OnDestroy()
        {
            closing=true;
            if(ApplicationServices.Realtime!=null)ApplicationServices.Realtime.Changed-=ConnectionChanged;
            Client?.Dispose();if(match){match.Closed-=MatchClosed;Destroy(match.gameObject);}
        }
    }
}
