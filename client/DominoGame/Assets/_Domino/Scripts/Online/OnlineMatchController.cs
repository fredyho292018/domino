using System;
using System.Collections;
using System.Collections.Generic;
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
        public event Action Closed;
        DominoTileView tilePrefab,selected;PlayerView playerPrefab;
        OnlineMatchSnapshot shown;bool rendering,disposed;
        readonly Queue<OnlineMatchSnapshot> presentation = new Queue<OnlineMatchSnapshot>();
        long queuedSequence=-1;
        bool resultDismissed;
        int acknowledgedRound=-1;
        readonly Queue<(long sequence,int seat)> passes = new Queue<(long,int)>();
        RectTransform controls;Button left,right,pass,next,first,second,resync;
        Text status,countdown;bool wasConnected;int shownSeconds=-1;
        public void Initialize(OnlineMatchClient client,DominoTileView tile,PlayerView player) {
            Client=client;tilePrefab=tile;playerPrefab=player;Client.Changed+=Changed;Client.Rejected+=Rejected;
            wasConnected=Connected;ApplicationServices.Realtime.Changed+=ConnectionChanged;Client.EventApplied+=Feedback;Changed();
            Client.PassPresented+=(sequence,seat)=>passes.Enqueue((sequence,seat));
        }
        void ConnectionChanged() {
            bool current=Connected;
            if(current!=wasConnected) {wasConnected=current;if(current)_=Client.ConnectionRestoredAsync();else Client.ConnectionLost();}
            RefreshInput();
        }
        void Feedback(string type) {
            string key=type=="TURN_TIMEOUT"?"game.time_expired":type=="AUTO_PLAYED"?"game.autoplay":type=="PLAYER_DISCONNECTED"?"game.player_disconnected":type=="PLAYER_RECONNECTED"?"online.player_reconnected":type=="PLAYER_ABANDONED"?"online.player_abandoned":null;
            if(key!=null&&Board)Board.ShowMessage(key);
        }
        void Changed() {
            if(disposed)return;
            if(Client.Snapshot!=null && Client.Snapshot.Sequence!=queuedSequence) {
                queuedSequence=Client.Snapshot.Sequence;presentation.Enqueue(Client.Snapshot);
            }
            if(presentation.Count>0&&!rendering)StartCoroutine(Render());
            RefreshInput();
        }
        IEnumerator Render() {
            rendering=true;
            yield return null; // Collect presentation payloads from the same authoritative update.
            while(presentation.Count>0) {
                shown=presentation.Dequeue();selected=null;
                if(!Board) {
                    var rules=shown.Rules;
                    var catalog=new JObject {["catalogSchemaVersion"]=1,["catalogVersion"]=rules["catalogVersion"].DeepClone(),["modes"]=new JArray(JObject.Parse((string)rules["effectiveModeJson"]))};
                    var configuration=new GameCatalogCodec().Read(catalog.ToString()).Modes.Single().RuleSet.Configuration;
                    Board=new GameObject("Online DUEL",typeof(RectTransform)).AddComponent<BoardView>();
                    Board.GetComponent<Transform>().SetParent(transform,false);
                    Board.Initialize(tilePrefab,playerPrefab,configuration,shown.Seat);
                    Board.GetComponent<Canvas>().sortingOrder=20;
                    Board.TileSelected+=t=>{selected=Board.ToggleSelection(t);RefreshInput();};
                    Board.TileDropped+=(t,end)=>{selected=t;Play(end==ChainEnd.Left?"LEFT":"RIGHT");};
                    Board.CanPlace=(t,end)=>CanPlace(t,end==ChainEnd.Left?"LEFT":"RIGHT");
                    Board.PlayRequested+=()=>{if(!shown.Hand.Any(t=>CanPlace(BoardView.OnlineTile(t),"LEFT")||CanPlace(BoardView.OnlineTile(t),"RIGHT")))Send("PASS");else Play("AUTO");};Board.ExitRequested+=()=>Destroy(gameObject);
                    Board.NextRoundRequested+=()=>{resultDismissed=true;if(Client.Snapshot.Phase=="ROUND_FINISHED")Send("NEXT_ROUND");};
                    Board.RestartRequested+=()=>Destroy(gameObject);
                    CreateControls();
                }
                RefreshInput();
                if(Board.StarterView&&Board.StarterView.gameObject.activeSelf&&shown.Phase!="STARTER_SELECTION") {
                    foreach(var choice in Board.StarterView.Choices)choice.interactable=false;
                    var result=shown.StarterResult;
                    var candidates=result?["chosenTiles"] as JArray;
                    if(candidates?.Count==2) {
                        yield return Board.StarterView.Reveal(0,BoardView.OnlineTile(candidates[0]),BoardView.OnlineTile(candidates[1]));
                        Board.StarterView.Result(true,(int)result["starterSeat"]);
                        yield return new WaitForSecondsRealtime(.8f);
                    }
                    Board.StarterView.Hide();
                }
                resultDismissed=false;
                yield return Board.RenderOnline(shown);
                while(passes.Count>0 && passes.Peek().sequence<=shown.Sequence)yield return Board.ShowPass(passes.Dequeue().seat);
                if(shown.Phase=="ROUND_FINISHED" && acknowledgedRound!=(int)shown.Public["currentRound"]) {
                    while(!resultDismissed)yield return null;
                    acknowledgedRound=(int)shown.Public["currentRound"];
                }
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
            countdown=Board.CreateOnlineCountdown();
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
            bool visible=shown?.Phase=="PLAYING"&&Client.TurnClock.HasDeadline;
            countdown.gameObject.SetActive(visible);
            int seconds=(int)Math.Ceiling(Client.TurnClock.RemainingSeconds);
            if(visible&&seconds!=shownSeconds){shownSeconds=seconds;DominoLocalization.Set(countdown,"online.turn_seconds",seconds);RefreshInput();}
        }
        bool Connected => ApplicationServices.Realtime?.State==RealtimeConnectionState.CONNECTED;
        bool OwnTurn => shown?.Phase=="PLAYING" && (int?)shown.Public["currentSeat"]==shown.Seat;
        bool CanPlace(DominoTile tile,string end) {
            if(!OwnTurn||Client.Pending||Client.NeedsResync||rendering||!Connected||Client.TurnClock.Expired)return false;
            var board=(JArray)shown.Public["board"];if(board.Count==0)return true;
            int pip=end=="LEFT"?(int)board[0]["tile"]["sideA"]:(int)board[board.Count-1]["tile"]["sideB"];
            return tile.SideA==pip||tile.SideB==pip; // Hint only; server still decides ownership and legality.
        }
        void RefreshInput() {
            if(!Board||!controls)return;
            bool enabled=Connected&&!Client.Pending&&!Client.NeedsResync&&!rendering&&!Client.TurnClock.Expired;
            var localParticipant=((JArray)shown.Public["participants"]).FirstOrDefault(p=>(int)p["seat"]==shown.Seat);
            enabled=enabled&&(string)localParticipant?["connectionState"]!="ABANDONED";
            bool hasMove=shown.Hand.Any(t=>CanPlace(BoardView.OnlineTile(t),"LEFT")||CanPlace(BoardView.OnlineTile(t),"RIGHT"));
            Board.SetOnlineAction(enabled&&OwnTurn&&!hasMove);
            Board.SetInteraction(enabled&&OwnTurn,enabled&&OwnTurn&&(selected||!hasMove));
            bool playing=shown.Phase=="PLAYING"&&Connected&&!Client.NeedsResync;
            pass.gameObject.SetActive(false);
            pass.interactable=enabled&&OwnTurn&&!shown.Hand.Any(t=>CanPlace(BoardView.OnlineTile(t),"LEFT")||CanPlace(BoardView.OnlineTile(t),"RIGHT"));
            next.gameObject.SetActive(false);next.interactable=false;
            var starter=shown.Starter;bool choosing=shown.Phase=="STARTER_SELECTION"&&starter!=null;
            first.gameObject.SetActive(choosing);second.gameObject.SetActive(choosing);
            if(choosing) {
                bool guess=(string)starter["method"]=="EVEN_ODD_GUESS";
                DominoLocalization.Set(first.GetComponentInChildren<Text>(),guess?"duel.even":"duel.tile_one");
                DominoLocalization.Set(second.GetComponentInChildren<Text>(),guess?"duel.odd":"duel.tile_two");
                bool turn=guess?(int)starter["guessingSeat"]==shown.Seat:!starter["selectedSeats"].Values<int>().Contains(shown.Seat);
                first.interactable=enabled&&turn&&(guess||starter["availableCandidates"].Values<int>().Contains(0));
                second.interactable=enabled&&turn&&(guess||starter["availableCandidates"].Values<int>().Contains(1));
                if(!guess) {
                    var view=Board.GetStarterView();if(!view.gameObject.activeSelf)view.Show();
                    int unavailable=starter["availableCandidates"].Values<int>().Contains(0)?(starter["availableCandidates"].Values<int>().Contains(1)?-1:1):0;
                    view.Prompt(shown.Seat,unavailable,Starter);
                    if(unavailable>=0)view.Tiles[unavailable].Select(true);
                    else foreach(var t in view.Tiles)t.Select(false);
                    for(int i=0;i<2;i++)view.Choices[i].interactable=enabled&&turn&&i!=unavailable;
                    first.gameObject.SetActive(false);second.gameObject.SetActive(false);
                }
            }
            resync.gameObject.SetActive(Client.NeedsResync||!Connected);
            status.gameObject.SetActive(!playing&&!(choosing&&(string)starter["method"]=="HIGH_TILE_SELECTION"));
            DominoLocalization.Set(status,!Connected?"realtime.disconnected":Client.NeedsResync?"system.loading":choosing?"duel.choose_starter":shown.Phase=="MATCH_FINISHED"?"result.game_over":OwnTurn?"game.your_turn":"realtime.connected");
            if(Connected&&!Client.NeedsResync&&shown.RoundResult!=null) {
                status.gameObject.SetActive(false);
                bool won=(int?)shown.RoundResult["winnerSeat"]==shown.Seat;
                DominoLocalization.Set(status,shown.Phase=="MATCH_FINISHED"?(won?"result.victory":"result.defeat"):(won?"result.round_won":"result.round_lost"));
            }
        }
        void Starter(int index) {
            if((string)shown.Starter["method"]=="EVEN_ODD_GUESS")Send("SUBMIT_EVEN_ODD_GUESS",new JObject {["even"]=index==0});
            else Send("SELECT_STARTER_TILE",new JObject {["candidate"]=index});
        }
        void Play(string end) {
            if(!selected)return;
            if(end=="AUTO")end=CanPlace(selected.Tile,"LEFT")?"LEFT":"RIGHT";
            if(CanPlace(selected.Tile,end))Send("PLAY_TILE",new JObject {["tile"]=new JObject {["sideA"]=selected.Tile.SideA,["sideB"]=selected.Tile.SideB},["chainEnd"]=end});
            else if(OwnTurn&&!Client.Pending)Board.ShowMessage("game.no_match",BoardView.OnlineLeft(shown),BoardView.OnlineRight(shown));
        }
        async void Send(string type,JObject payload=null) {try {await Client.SendAsync(type,payload);}catch {Rejected("TRANSPORT");}}
        void Rejected(string code) {if(Board)Board.ShowMessage("game.invalid_end");}
        void OnDestroy() {disposed=true;if(Client!=null)Client.EventApplied-=Feedback;Client?.Dispose();if(ApplicationServices.Realtime!=null)ApplicationServices.Realtime.Changed-=ConnectionChanged;Closed?.Invoke();}
    }
}
