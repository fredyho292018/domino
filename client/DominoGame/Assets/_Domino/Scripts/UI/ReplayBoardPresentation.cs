using System.Collections;
using System.Linq;
using Domino.Core;
using Domino.Replay;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Domino.UI
{
    public sealed partial class BoardView
    {
        bool replayPresentation;
        public void ConfigureReplay()
        {
            replayPresentation=true;
            var viewport=UiKit.Rect("Replay table viewport",safe,Vector2.zero,Vector2.zero);
            viewport.anchorMin=new Vector2(0,.23f);viewport.anchorMax=new Vector2(1,.91f);viewport.sizeDelta=Vector2.zero;
            content.SetParent(viewport,false);safe=viewport;previousLayout=Vector2.zero;
            foreach(var button in content.GetComponentsInChildren<UnityEngine.UI.Button>(true))button.interactable=false;
            playButton.gameObject.SetActive(false);menu.SetActive(false);
            foreach(string name in new[]{"Restart","Menu"})if(content.Find(name))content.Find(name).gameObject.SetActive(false);
            SetInteraction(false,false);
        }
        public IEnumerator RenderReplay(ReplayState replay,int perspective,bool animate,float speed=1)
        {
            var state=replay.Data;int selected=perspective<0?0:perspective;
            SetInteraction(false,false);CancelDrag();ResetEndpointZoom();
            if(!animate)effects.Clear();
            if(LocalPlayerSeat!=selected) {Perspective=new SeatPerspectiveMapper(selected,configuration);previousLayout=Vector2.zero;Fit();}
            if(animate&&(string)state["eventType"]=="TILE_PLAYED") {
                var p=state["eventPayload"];int seat=(int)p["seat"];var value=OnlineTile(p["tile"]);
                var tile=hands[seat].FirstOrDefault(v=>v.Tile.Equals(value));
                if(!tile&&hands[seat].Count>0){tile=hands[seat][0];tile.Orient(value);}
                if(tile)yield return Play(new GameEvent(GameEventType.TILE_PLAYED,seat,value,(string)p["chainEnd"]=="LEFT"?0:played.Count),speed);
            }
            if(animate&&(string)state["eventType"]=="PLAYER_PASSED") {
                int seat=(int)state["eventPayload"]["seat"];
                yield return effects.Knock((int)Perspective.PositionFor(seat),KnockPosition(seat),speed);
            }
            foreach(var hand in hands){foreach(var tile in hand)ReleaseTile(tile);hand.Clear();}
            for(int seat=0;seat<hands.Length;seat++) {
                var historical=(JArray)state["hands"][seat];int count=(int)state["counts"][seat];
                for(int i=0;i<count;i++) {
                    var tile=AcquireTile();bool show=seat==perspective&&i<historical.Count;
                    tile.Initialize(show?OnlineTile(historical[i]):default,show,false);
                    tile.transform.SetParent(seat==LocalPlayerSeat?localHandLayer:opponentHandLayer,false);hands[seat].Add(tile);
                }
                players[seat].SetCount(count);players[seat].SetDisplayName((string)((JArray)state["participants"]).Single(p=>(int)p["seat"]==seat)["displayNameSnapshot"]);
                var initials=players[seat].transform.Find("Initials").GetComponent<UnityEngine.UI.Text>();
                initials.text=string.IsNullOrEmpty(players[seat].DisplayName)?"":players[seat].DisplayName.Substring(0,1).ToUpperInvariant();
                players[seat].SetTurn((int)state["turn"]==seat);
            }
            foreach(var tile in played)ReleaseTile(tile);played.Clear();
            foreach(var p in (JArray)state["board"]) {var tile=AcquireTile();tile.Initialize(OnlineTile(p["tile"]),true,false);played.Add(tile);}
            SetDealPhase(DealPresentationPhase.Playing);RefreshHandLayout();ApplyChainLayout();
            displayedRound=replay.Round;boardHint.gameObject.SetActive(played.Count==0);
            DominoLocalization.Set(round,"game.score_round",replay.Round,configuration.TargetScore,(int)state["multiplier"]);
            if(configuration.PlayerCount==2) {
                DominoLocalization.Set(scoreA,"replay.score",players[0].DisplayName,(int)state["scores"][0]);
                DominoLocalization.Set(scoreB,"replay.score",players[1].DisplayName,(int)state["scores"][1]);
            } else {
                DominoLocalization.Set(scoreA,"replay.team_score","A",(int)state["scores"][0]);
                DominoLocalization.Set(scoreB,"replay.team_score","B",(int)state["scores"][1]);
            }
            bannerTarget=0;UpdateTurnNotice(-1);prompt.gameObject.SetActive(false);
            turnNotice.gameObject.SetActive(false);
            if(state["roundResult"] is JObject result)for(int i=0;i<hands.Length;i++)players[i].ShowPoints(hands[i].Count,(int)result["remainingPips"][i]);
            SetInteraction(false,false);
        }
    }
}
