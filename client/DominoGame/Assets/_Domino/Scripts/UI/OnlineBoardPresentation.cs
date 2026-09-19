using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Domino.Core;
using Domino.Online;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Domino.UI
{
    public sealed partial class BoardView
    {
        int onlineRound;
        OnlineMatchSnapshot onlineShown;
        public void SetOnlineAction(bool pass) => DominoLocalization.Set(playButton.GetComponentInChildren<UnityEngine.UI.Text>(),pass?"game.pass":"game.play");
        public UnityEngine.UI.Text CreateOnlineCountdown() => UiKit.Label("Turn countdown",scoreA.transform.parent,"",new Vector2(300,28),new Vector2(0,-46),18,UiKit.Muted);
        public static int OnlineLeft(OnlineMatchSnapshot snapshot) => snapshot.Public["board"].Any() ? (int)snapshot.Public["board"][0]["tile"]["sideA"] : 0;
        public static int OnlineRight(OnlineMatchSnapshot snapshot) => snapshot.Public["board"].Any() ? (int)snapshot.Public["board"].Last["tile"]["sideB"] : 0;
        public static DominoTile OnlineTile(JToken t) => new DominoTile((int)t["sideA"],(int)t["sideB"]);
        public IEnumerator RenderOnline(OnlineMatchSnapshot snapshot)
        {
            SetInteraction(false,false);CancelDrag();ResetEndpointZoom();
            var state=snapshot.Public;int number=(int)state["currentRound"];
            playButton.gameObject.SetActive(snapshot.Phase=="PLAYING");
            if(content.Find("Restart"))content.Find("Restart").gameObject.SetActive(false);
            var chain=(JArray)state["board"];
            if(number>onlineRound && snapshot.Phase=="PLAYING") {
                Domino.Infrastructure.ApplicationServices.RoundRewards?.LeaveRound();
                roundEnded=false;matchEnded=false;
                if(RoundRewardPanel)Destroy(RoundRewardPanel.gameObject);
                prompt.gameObject.SetActive(true);
            }
            // Animate only a confirmed, continuous one-tile addition. Resync gaps restore directly.
            if(onlineShown!=null && onlineRound==number && chain.Count==played.Count+1) {
                int added=-1;
                if(chain.Skip(1).Select(t=>OnlineTile(t["tile"]).Id).SequenceEqual(played.Select(t=>t.Tile.Id)))added=0;
                else if(chain.Take(chain.Count-1).Select(t=>OnlineTile(t["tile"]).Id).SequenceEqual(played.Select(t=>t.Tile.Id)))added=chain.Count-1;
                if(added>=0) {
                    int actor=(int)chain[added]["actorSeat"];var value=OnlineTile(chain[added]["tile"]);
                    if(actor!=LocalPlayerSeat&&hands[actor].Count>0)hands[actor][0].Orient(value);
                    if(hands[actor].Any(t=>t.Tile.Equals(value)))yield return Play(new GameEvent(GameEventType.TILE_PLAYED,actor,value,added));
                }
            }
            // A resync in mid-round restores counts directly; it must not deal the missing played tiles again.
            if(number>onlineRound && snapshot.Phase=="PLAYING" && ((JArray)state["board"]).Count==0 && snapshot.Hand.Count==10) {
                foreach(var hand in hands) {foreach(var tile in hand)ReleaseTile(tile);hand.Clear();}
                foreach(var tile in played)ReleaseTile(tile);played.Clear();
                foreach(var tile in washReserve)ReleaseTile(tile);washReserve.Clear();
                var deal=new IReadOnlyList<DominoTile>[configuration.PlayerCount];
                for(int p=0;p<configuration.PlayerCount;p++) {
                    var list=new List<DominoTile>();
                    if(p==LocalPlayerSeat)foreach(var t in snapshot.Hand)list.Add(OnlineTile(t));
                    else for(int n=0;n<(int)state["tilesRemainingPerSeat"][p];n++)list.Add(default);
                    deal[p]=list;
                }
                yield return Deal(deal);onlineRound=number;
            }
            foreach(var tile in dealStock)ReleaseTile(tile);dealStock.Clear();
            if(number>0 && washReserve.Count==0) {
                for(int i=0;i<configuration.ReserveCount;i++) {var tile=AcquireTile();tile.Initialize(default,false,false);washReserve.Add(tile);}
                reserveParked=true;DominoLocalization.Set(reserveLabel,"game.reserve",washReserve.Count);reserveLabel.gameObject.SetActive(true);
                for(int i=0;i<washReserve.Count;i++){washReserve[i].Rect.anchoredPosition=ReservePosition(i);washReserve[i].Rect.localScale=Vector3.one*.36f;}
            }
            onlineRound=number;
            foreach(var hand in hands) {foreach(var tile in hand)ReleaseTile(tile);hand.Clear();}
            for(int p=0;p<configuration.PlayerCount;p++) {
                int count=(int)state["tilesRemainingPerSeat"][p];
                for(int i=0;i<count;i++) {
                    var tile=AcquireTile();tile.Initialize(p==LocalPlayerSeat?OnlineTile(snapshot.Hand[i]):default,p==LocalPlayerSeat,true);
                    tile.transform.SetParent(p==LocalPlayerSeat?localHandLayer:opponentHandLayer,false);hands[p].Add(tile);
                }
                players[p].SetCount(count);
                foreach(var participant in (JArray)state["participants"])
                    if((int)participant["seat"]==p)players[p].SetDisplayName((string)participant["displayNameSnapshot"]);
            }
            foreach(var tile in played)ReleaseTile(tile);played.Clear();
            foreach(var placement in (JArray)state["board"]) {var tile=AcquireTile();tile.Initialize(OnlineTile(placement["tile"]),true,false);played.Add(tile);}
            SetDealPhase(DealPresentationPhase.Playing);RefreshHandLayout();ApplyChainLayout();
            boardHint.gameObject.SetActive(played.Count==0);
            displayedRound=number;
            if(configuration.PlayerCount==2) {
                DominoLocalization.Set(scoreA,"game.player_score",1,(int)state["scores"][0]);
                DominoLocalization.Set(scoreB,"game.player_score",2,(int)state["scores"][1]);
            } else {
                int team=configuration.GetTeamForPlayer(LocalPlayerSeat);
                DominoLocalization.Set(scoreA,"game.score_a",(int)state["scores"][team],0);
                DominoLocalization.Set(scoreB,"game.score_b",(int)state["scores"][1-team],0);
            }
            DominoLocalization.Set(round,"game.score_round",number,configuration.TargetScore,snapshot.RoundMultiplier);
            int turn=state["currentSeat"].Type==JTokenType.Null?-1:(int)state["currentSeat"];
            if(turn>=0 && (onlineShown==null || (int?)onlineShown.Public["currentSeat"]!=turn || (int?)onlineShown.Public["currentTurn"]!=(int?)state["currentTurn"]))SetTurn(turn);
            for(int p=0;p<configuration.PlayerCount;p++)players[p].SetTurn(p==turn);
            bannerTarget=turn==LocalPlayerSeat?1:0;UpdateTurnNotice(turn);
            if(turn>=0)ShowMessage(turn==LocalPlayerSeat?"game.your_turn":"realtime.connected");
            if(snapshot.RoundResult!=null && (onlineShown?.RoundResult==null || onlineShown.Public["currentRound"].Value<int>()!=number)) {
                float resultStarted=Time.realtimeSinceStartup;
                int winner=(int?)snapshot.RoundResult["winnerSeat"]??-1;
                string name=winner<0?DominoLocalization.Get("result.draw"):(string)((JArray)state["participants"]).First(p=>(int)p["seat"]==winner)["displayNameSnapshot"];
                if(winner>=0)yield return ShowWinner(name,snapshot.Phase=="MATCH_FINISHED",configuration.GetScoreOwner(winner)==configuration.GetScoreOwner(LocalPlayerSeat));
                // Only the aggregate pips are public in the current server contract; never fabricate opponent faces.
                for(int p=0;p<configuration.PlayerCount;p++)players[p].ShowPoints(hands[p].Count,(int)snapshot.RoundResult["remainingPips"][p]);
                int award=(int)snapshot.RoundResult["scoreAwarded"];
                bool won=winner>=0&&configuration.GetScoreOwner(winner)==configuration.GetScoreOwner(LocalPlayerSeat);
                bool lost=winner>=0&&!won;
                if(lost) {
                    Domino.Infrastructure.ApplicationServices.RoundRewards?.PresentRound(snapshot.MatchId+":"+number);
                    Domino.Ads.RewardedRoundPreload.Handle(new GameEvent(GameEventType.ROUND_FINISHED),Domino.Infrastructure.ApplicationServices.Rewarded);
                }
                while(Time.realtimeSinceStartup-resultStarted<5f)yield return null;
                Finish(()=>DominoLocalization.Get(winner<0?"result.draw":won?"result.round_won":"result.round_lost")+"\n"+DominoLocalization.Get("result.online_award",name,award),snapshot.Phase=="MATCH_FINISHED",lost);
            }
            onlineShown=snapshot;
        }
    }
}
