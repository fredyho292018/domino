using System;
using System.Collections;
using System.Linq;
using Domino.Catalog;
using Domino.Core;
using Domino.Infrastructure;
using Domino.Realtime;
using Domino.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.Online
{
    public sealed class OnlineMatchController : MonoBehaviour
    {
        public OnlineMatchClient Client {get;private set;}
        public BoardView Board {get;private set;}
        DominoTileView tilePrefab,selected;PlayerView playerPrefab;
        OnlineMatchSnapshot shown;bool rendering,disposed;
        RectTransform controls;Button left,right,pass,next,first,second,resync;
        Text status;
        public void Initialize(OnlineMatchClient client,DominoTileView tile,PlayerView player) {
            Client=client;tilePrefab=tile;playerPrefab=player;Client.Changed+=Changed;Client.Rejected+=Rejected;
            ApplicationServices.Realtime.Changed+=RefreshInput;Changed();
        }
        void Changed() {
            if(disposed)return;
            if(Client.Snapshot!=null && (shown==null||Client.Snapshot.Sequence!=shown.Sequence) && !rendering)StartCoroutine(Render());
            RefreshInput();
        }
        IEnumerator Render() {
            rendering=true;
            while(Client.Snapshot!=null && (shown==null||shown.Sequence!=Client.Snapshot.Sequence)) {
                shown=Client.Snapshot;selected=null;
                if(!Board) {
                    var rules=shown.Rules;
                    var catalog=new JObject {["catalogSchemaVersion"]=1,["catalogVersion"]=rules["catalogVersion"].DeepClone(),["modes"]=new JArray(JObject.Parse((string)rules["effectiveModeJson"]))};
                    var configuration=new GameCatalogCodec().Read(catalog.ToString()).Modes.Single().RuleSet.Configuration;
                    Board=new GameObject("Online DUEL",typeof(RectTransform)).AddComponent<BoardView>();
                    Board.GetComponent<Transform>().SetParent(transform,false);
                    Board.Initialize(tilePrefab,playerPrefab,configuration,shown.Seat);
                    Board.GetComponent<Canvas>().sortingOrder=20;
                    Board.TileSelected+=t=>{selected=t;Board.SetSelected(t);RefreshInput();};
                    Board.TileDropped+=(t,end)=>{selected=t;Play(end==ChainEnd.Left?"LEFT":"RIGHT");};
                    Board.CanPlace=(t,end)=>CanPlace(t,end==ChainEnd.Left?"LEFT":"RIGHT");
                    Board.PlayRequested+=()=>Play("RIGHT");Board.ExitRequested+=()=>Destroy(gameObject);
                    CreateControls();
                }
                yield return Board.RenderOnline(shown);
            }
            rendering=false;RefreshInput();
        }
        void CreateControls() {
            var overlay=new GameObject("Online controls",typeof(RectTransform));overlay.transform.SetParent(transform,false);
            var canvas=overlay.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30;
            BoardView.ConfigureCanvas(overlay);overlay.AddComponent<GraphicRaycaster>();
            var safe=UiKit.Rect("Safe area",overlay.transform,Vector2.zero,Vector2.zero);safe.gameObject.AddComponent<SafeArea>();
            controls=UiKit.Rect("Actions",safe,new Vector2(900,320),Vector2.zero);
            status=UiKit.Label("Online status",controls,"",new Vector2(860,80),new Vector2(0,110),24,UiKit.Cream);
            left=UiKit.Button("Left",controls,"←",new Vector2(125,64),new Vector2(-350,-50),UiKit.Hex("397566"),()=>Play("LEFT"));
            right=UiKit.Button("Right",controls,"→",new Vector2(125,64),new Vector2(-200,-50),UiKit.Hex("397566"),()=>Play("RIGHT"));
            pass=UiKit.LButton("Pass",controls,"game.pass",new Vector2(220,64),new Vector2(290,-50),UiKit.Hex("397566"),()=>Send("PASS"));
            next=UiKit.LButton("Next",controls,"game.next_round",new Vector2(400,74),new Vector2(0,-20),UiKit.Hex("397566"),()=>Send("NEXT_ROUND"));
            first=UiKit.LButton("First",controls,"duel.tile_one",new Vector2(340,90),new Vector2(-190,-20),UiKit.Hex("397566"),()=>Starter(0));
            second=UiKit.LButton("Second",controls,"duel.tile_two",new Vector2(340,90),new Vector2(190,-20),UiKit.Hex("397566"),()=>Starter(1));
            resync=UiKit.LButton("Resync",controls,"system.retry",new Vector2(340,74),new Vector2(0,-20),UiKit.Hex("397566"),()=>{_=Client.ResyncAsync();});
        }
        void LateUpdate() {
            if(!controls)return;var safe=(RectTransform)controls.parent;
            controls.localScale=Vector3.one*Mathf.Min(safe.rect.width/940,safe.rect.height/950);
            controls.anchoredPosition=shown?.Phase=="PLAYING"&&Connected&&!Client.NeedsResync?new Vector2(0,-safe.rect.height*.27f):Vector2.zero;
        }
        bool Connected => ApplicationServices.Realtime?.State==RealtimeConnectionState.CONNECTED;
        bool OwnTurn => shown?.Phase=="PLAYING" && (int?)shown.Public["currentSeat"]==shown.Seat;
        bool CanPlace(DominoTile tile,string end) {
            if(!OwnTurn||Client.Pending||Client.NeedsResync||rendering||!Connected)return false;
            var board=(JArray)shown.Public["board"];if(board.Count==0)return true;
            int pip=end=="LEFT"?(int)board[0]["tile"]["sideA"]:(int)board[board.Count-1]["tile"]["sideB"];
            return tile.SideA==pip||tile.SideB==pip; // Hint only; server still decides ownership and legality.
        }
        void RefreshInput() {
            if(!Board||!controls)return;
            bool enabled=Connected&&!Client.Pending&&!Client.NeedsResync&&!rendering;
            Board.SetInteraction(enabled&&OwnTurn,false);
            bool playing=shown.Phase=="PLAYING"&&Connected&&!Client.NeedsResync;
            left.gameObject.SetActive(playing);right.gameObject.SetActive(playing);pass.gameObject.SetActive(playing);
            left.interactable=selected&&CanPlace(selected.Tile,"LEFT");right.interactable=selected&&CanPlace(selected.Tile,"RIGHT");
            pass.interactable=enabled&&OwnTurn&&!shown.Hand.Any(t=>CanPlace(BoardView.OnlineTile(t),"LEFT")||CanPlace(BoardView.OnlineTile(t),"RIGHT"));
            next.gameObject.SetActive(shown.Phase=="ROUND_FINISHED");next.interactable=enabled;
            var starter=shown.Starter;bool choosing=shown.Phase=="STARTER_SELECTION"&&starter!=null;
            first.gameObject.SetActive(choosing);second.gameObject.SetActive(choosing);
            if(choosing) {
                bool guess=(string)starter["method"]=="EVEN_ODD_GUESS";
                DominoLocalization.Set(first.GetComponentInChildren<Text>(),guess?"duel.even":"duel.tile_one");
                DominoLocalization.Set(second.GetComponentInChildren<Text>(),guess?"duel.odd":"duel.tile_two");
                bool turn=guess?(int)starter["guessingSeat"]==shown.Seat:!starter["selectedSeats"].Values<int>().Contains(shown.Seat);
                first.interactable=enabled&&turn&&(guess||starter["availableCandidates"].Values<int>().Contains(0));
                second.interactable=enabled&&turn&&(guess||starter["availableCandidates"].Values<int>().Contains(1));
            }
            resync.gameObject.SetActive(Client.NeedsResync||!Connected);
            status.gameObject.SetActive(!playing);
            DominoLocalization.Set(status,!Connected?"realtime.disconnected":Client.NeedsResync?"system.loading":choosing?"duel.choose_starter":shown.Phase=="MATCH_FINISHED"?"result.game_over":OwnTurn?"game.your_turn":"realtime.connected");
            if(Connected&&!Client.NeedsResync&&shown.RoundResult!=null) {
                bool won=(int?)shown.RoundResult["winnerSeat"]==shown.Seat;
                DominoLocalization.Set(status,shown.Phase=="MATCH_FINISHED"?(won?"result.victory":"result.defeat"):(won?"result.round_won":"result.round_lost"));
            }
        }
        void Starter(int index) {
            if((string)shown.Starter["method"]=="EVEN_ODD_GUESS")Send("SUBMIT_EVEN_ODD_GUESS",new JObject {["even"]=index==0});
            else Send("SELECT_STARTER_TILE",new JObject {["candidate"]=index});
        }
        void Play(string end) {if(selected&&CanPlace(selected.Tile,end))Send("PLAY_TILE",new JObject {["tile"]=new JObject {["sideA"]=selected.Tile.SideA,["sideB"]=selected.Tile.SideB},["chainEnd"]=end});}
        async void Send(string type,JObject payload=null) {try {await Client.SendAsync(type,payload);}catch {Rejected("TRANSPORT");}}
        void Rejected(string code) {if(Board)Board.ShowMessage("game.invalid_end");}
        void OnDestroy() {disposed=true;Client?.Dispose();if(ApplicationServices.Realtime!=null)ApplicationServices.Realtime.Changed-=RefreshInput;}
    }
}
