using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Domino.Replay
{
    public sealed class ReplayDataException : Exception
    {
        public string Code { get; }
        public ReplayDataException(string code) : base(code) { Code=code; }
    }
    // Detached value snapshots. No Unity, clock, random, transport or persistence dependency.
    public sealed class ReplayState
    {
        readonly JObject value;
        internal ReplayState(JObject value) { this.value=(JObject)value.DeepClone(); }
        public JObject Data => (JObject)value.DeepClone();
        public long Sequence => (long)value["sequence"];
        public int Round => (int)value["round"];
    }
    public static class ReplayReducer
    {
        public static readonly string[] SupportedEvents={"STARTER_PROGRESS","MATCH_STARTED","ROUND_STARTED","HAND_DEALT","STARTER_SELECTION_PRIVATE","TURN_STARTED","TILE_PLAYED","TURN_CHANGED","PLAYER_PASSED","ROUND_FINISHED","MATCH_FINISHED","TURN_TIMEOUT","AUTO_PLAYED","PLAYER_DISCONNECTED","PLAYER_RECONNECTED","PLAYER_ABANDONED"};
        static void Require(bool value,string code="INCOMPLETE_REPLAY") { if(!value)throw new ReplayDataException(code); }
        public static ReplayState Initial(JObject manifest)
        {
            Require((int?)manifest["replaySchemaVersion"]==1,"UNSUPPORTED_SCHEMA");
            var mode=JObject.Parse((string)manifest["ruleSnapshot"]["effectiveModeJson"]);
            int count=(int)mode["playerCount"];
            Require(count==2||count==4,"UNSUPPORTED_SCHEMA");
            return new ReplayState(new JObject {
                ["sequence"]=0,["round"]=0,["turnNumber"]=0,["turn"]=-1,["phase"]="STARTING",
                ["matchId"]=manifest["matchId"].DeepClone(),["rules"]=mode,["participants"]=manifest["participants"].DeepClone(),
                ["board"]=new JArray(),["hands"]=new JArray(Enumerable.Range(0,count).Select(_=>(object)new JArray())),
                ["dealt"]=new JArray(Enumerable.Repeat(false,count)),["counts"]=new JArray(Enumerable.Repeat(0,count)),
                ["scores"]=new JArray(0,0),["multiplier"]=1,["nextMultiplier"]=1,["roundResult"]=null,["matchResult"]=null,
                ["leftEnd"]=null,["rightEnd"]=null,["eventType"]="",["eventPayload"]=new JObject()
            });
        }
        public static ReplayState Apply(ReplayState previous,JObject e)
        {
            var s=previous.Data;string type=(string)e["type"];var p=e["payload"] as JObject;
            Require((long?)e["sequence"]==previous.Sequence+1&&(string)e["matchId"]==(string)s["matchId"]);
            Require((int?)e["eventSchemaVersion"]==1,"UNSUPPORTED_SCHEMA");
            Require(SupportedEvents.Contains(type),"REPLAY_UNSUPPORTED_EVENT");
            Require(p!=null&&(string)p["kind"]==type,"UNSUPPORTED_SCHEMA");
            Require((int?)e["roundNumber"]==previous.Round+(type=="ROUND_STARTED"?1:0));
            int count=((JArray)s["hands"]).Count;
            int Seat() {int seat=(int?)p["seat"]??-1;Require(seat>=0&&seat<count);return seat;}
            switch(type) {
                case "MATCH_STARTED": s["scores"]=p["initialScores"].DeepClone();break;
                case "STARTER_PROGRESS": s["starter"]=p.DeepClone();break;
                case "STARTER_SELECTION_PRIVATE": s["starterResult"]=p["result"].DeepClone();break;
                case "ROUND_STARTED":
                    Require((int)e["roundNumber"]==previous.Round+1);
                    s["board"]=new JArray();s["hands"]=new JArray(Enumerable.Range(0,count).Select(_=>(object)new JArray()));
                    s["dealt"]=new JArray(Enumerable.Repeat(false,count));s["counts"]=p["tilesRemainingPerSeat"].DeepClone();
                    s["turn"]=p["starterSeat"].DeepClone();s["roundResult"]=null;s["phase"]="DEALING";
                    s["multiplier"]=s["nextMultiplier"].DeepClone();break;
                case "HAND_DEALT": {
                    int seat=Seat();Require(!(bool)s["dealt"][seat]&&(string)e["visibility"]=="PLAYER_PRIVATE"&&(int?)e["targetSeat"]==seat);
                    Require(((JArray)p["tiles"]).Count==(int)s["counts"][seat]);
                    s["hands"][seat]=p["tiles"].DeepClone();s["dealt"][seat]=true;break; }
                case "TURN_STARTED":case "TURN_CHANGED":
                    Require(((JArray)s["dealt"]).All(v=>(bool)v));
                    s["turn"]=Seat();s["phase"]="PLAYING";break;
                case "TILE_PLAYED": {
                    int seat=Seat();var t=p["tile"];var hand=(JArray)s["hands"][seat];int key=TileKey(t);
                    var owned=hand.FirstOrDefault(v=>TileKey(v)==key);Require(owned!=null);owned.Remove();
                    var board=(JArray)s["board"];string end=(string)p["chainEnd"];Require(end=="LEFT"||end=="RIGHT");
                    if(board.Count>0)Require(end=="LEFT"?(int)t["sideB"]==(int)board[0]["tile"]["sideA"]:(int)t["sideA"]==(int)board.Last["tile"]["sideB"]);
                    var placed=new JObject {["tile"]=t.DeepClone(),["actorSeat"]=seat,["chainEnd"]=end};
                    if(end=="LEFT")board.Insert(0,placed);else board.Add(placed);
                    s["counts"][seat]=hand.Count;break; }
                case "ROUND_FINISHED":
                    s["roundResult"]=p.DeepClone();s["scores"]=p["scoresAfter"].DeepClone();s["turn"]=-1;s["phase"]="ROUND_FINISHED";
                    s["nextMultiplier"]=p["winnerSeat"].Type==JTokenType.Null?(int)s["rules"]["ruleSet"]["tiePolicy"]["nextRoundMultiplier"]:1;
                    for(int i=0;i<count;i++)Require(((JArray)s["hands"][i]).Sum(t=>(int)t["sideA"]+(int)t["sideB"])==(int)p["remainingPips"][i]);
                    break;
                case "MATCH_FINISHED":
                    s["matchResult"]=p["result"].DeepClone();s["scores"]=p["result"]["scores"].DeepClone();s["turn"]=-1;s["phase"]="MATCH_FINISHED";break;
                case "PLAYER_DISCONNECTED":case "PLAYER_RECONNECTED":case "PLAYER_ABANDONED":
                    var participant=((JArray)s["participants"]).Single(v=>(int)v["seat"]==Seat());
                    participant["connectionState"]=type=="PLAYER_DISCONNECTED"?"DISCONNECTED":type=="PLAYER_RECONNECTED"?"CONNECTED":"ABANDONED";break;
                // Audit events never apply a tile twice; the subsequent TILE_PLAYED/PASSED is canonical.
                case "PLAYER_PASSED":case "TURN_TIMEOUT":case "AUTO_PLAYED": Seat();break;
            }
            var chain=(JArray)s["board"];s["leftEnd"]=chain.Count==0?null:chain[0]["tile"]["sideA"].DeepClone();
            s["rightEnd"]=chain.Count==0?null:chain.Last["tile"]["sideB"].DeepClone();
            s["sequence"]=e["sequence"].DeepClone();s["round"]=e["roundNumber"].DeepClone();s["turnNumber"]=e["turnNumber"].DeepClone();
            s["eventType"]=type;s["eventPayload"]=p.DeepClone();
            var known=chain.Select(v=>TileKey(v["tile"])).Concat(((JArray)s["hands"]).SelectMany(h=>h.Select(TileKey))).ToList();
            Require(known.Count==known.Distinct().Count()&&known.Count<=55);
            s["unseenCount"]=55-known.Count; // Includes not-yet-delivered private snapshots during the deal; never invented tiles.
            return new ReplayState(s);
        }
        static int TileKey(JToken tile) {int a=(int)tile["sideA"],b=(int)tile["sideB"];Require(a>=0&&a<=9&&b>=0&&b<=9);return Math.Min(a,b)*10+Math.Max(a,b);}
    }
    public sealed class ReplayTimeline
    {
        readonly List<JObject> events;
        readonly SortedDictionary<int,ReplayState> checkpoints=new SortedDictionary<int,ReplayState>();
        public int Count=>events.Count;
        public IReadOnlyList<int> RoundStarts {get;}
        public ReplayTimeline(JObject manifest,IEnumerable<JObject> source)
        {
            events=source.Select(e=>(JObject)e.DeepClone()).ToList();
            if(events.Count!=(long)manifest["lastSequence"])throw new ReplayDataException("INCOMPLETE_REPLAY");
            var state=ReplayReducer.Initial(manifest);checkpoints[0]=state;var rounds=new List<int>();
            for(int i=0;i<events.Count;i++) {
                state=ReplayReducer.Apply(state,events[i]);
                if((string)events[i]["type"]=="ROUND_STARTED")rounds.Add(i+1);
                if((i+1)%50==0||(string)events[i]["type"]=="ROUND_STARTED")checkpoints[i+1]=state;
            }
            if(!JToken.DeepEquals(state.Data["scores"],manifest["finalScore"])||!JToken.DeepEquals(state.Data["matchResult"],manifest["result"]))throw new ReplayDataException("INCOMPLETE_REPLAY");
            RoundStarts=rounds.AsReadOnly();
        }
        public ReplayState Seek(int sequence)
        {
            if(sequence<0||sequence>Count)throw new ArgumentOutOfRangeException(nameof(sequence));
            int at=checkpoints.Keys.Last(k=>k<=sequence);var state=checkpoints[at];
            while(at<sequence)state=ReplayReducer.Apply(state,events[at++]);return state;
        }
    }
}
