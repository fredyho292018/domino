using System.Collections;
using System.Collections.Generic;
using Domino.Core;
using Domino.Online;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Domino.UI
{
    public sealed partial class BoardView
    {
        int onlineRound;
        public static DominoTile OnlineTile(JToken t) => new DominoTile((int)t["sideA"],(int)t["sideB"]);
        public IEnumerator RenderOnline(OnlineMatchSnapshot snapshot)
        {
            SetInteraction(false,false);CancelDrag();ResetEndpointZoom();
            var state=snapshot.Public;int number=(int)state["currentRound"];
            playButton.gameObject.SetActive(false);
            if(content.Find("Restart"))content.Find("Restart").gameObject.SetActive(false);
            // A resync in mid-round restores counts directly; it must not deal the missing played tiles again.
            if(number>onlineRound && snapshot.Phase=="PLAYING" && ((JArray)state["board"]).Count==0 && snapshot.Hand.Count==10) {
                foreach(var hand in hands) {foreach(var tile in hand)ReleaseTile(tile);hand.Clear();}
                foreach(var tile in played)ReleaseTile(tile);played.Clear();
                foreach(var tile in washReserve)ReleaseTile(tile);washReserve.Clear();
                var deal=new IReadOnlyList<DominoTile>[2];
                for(int p=0;p<2;p++) {
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
            for(int p=0;p<2;p++) {
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
            DominoLocalization.Set(scoreA,"game.player_score",1,(int)state["scores"][0]);
            DominoLocalization.Set(scoreB,"game.player_score",2,(int)state["scores"][1]);
            DominoLocalization.Set(round,"game.score_round",number,configuration.TargetScore,1);
            int turn=state["currentSeat"].Type==JTokenType.Null?-1:(int)state["currentSeat"];
            for(int p=0;p<2;p++)players[p].SetTurn(p==turn);
            bannerTarget=turn==LocalPlayerSeat?1:0;UpdateTurnNotice(turn);
            if(turn>=0)ShowMessage(turn==LocalPlayerSeat?"game.your_turn":"realtime.connected");
        }
    }
}
