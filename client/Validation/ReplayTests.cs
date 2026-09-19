using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Domino.Replay;
using Newtonsoft.Json.Linq;

class ReplayTests
{
    static int checks;static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    static void Reject(Action action){try{action();}catch(ReplayDataException){checks++;return;}throw new Exception("Corruption accepted");}
    static void Main()
    {
        string dir=Environment.GetEnvironmentVariable("DOMINO_REPLAY_EXPORTS"),output=Environment.GetEnvironmentVariable("DOMINO_REPLAY_OUTPUT");
        if(!Directory.Exists(dir))throw new Exception("Retained I3.1 exports required; do not contact Firestore to replace them automatically");
        int count=0,total=0;var modes=new HashSet<string>();var eventTypes=new HashSet<string>();var finishes=new HashSet<string>();
        foreach(string file in Directory.GetFiles(dir,"*.json")) {
            var export=JObject.Parse(File.ReadAllText(file));var m=(JObject)export["match"];
            if((string)m["status"]!="FINISHED")continue;
            var mode=JObject.Parse((string)m["ruleSnapshot"]["effectiveModeJson"]);var events=((JArray)export["events"]).Cast<JObject>().OrderBy(e=>(long)e["sequence"]).ToList();
            var runtime=JObject.Parse((string)export["runtime"]["stateJson"]);
            var manifest=new JObject {["replaySchemaVersion"]=1,["eventSchemaVersion"]=1,["matchId"]=m["matchId"].DeepClone(),["modeKey"]=m["modeKey"].DeepClone(),["ruleSnapshot"]=m["ruleSnapshot"].DeepClone(),["participants"]=new JArray(((JArray)m["participants"]).Select(p=>(object)new JObject {["seat"]=p["seatIndex"].DeepClone(),["displayNameSnapshot"]=p["displayNameSnapshot"].DeepClone(),["teamId"]=p["teamId"].DeepClone(),["controlType"]=p["controlType"].DeepClone()})),["selfSeat"]=0,["teams"]=mode["seatTeams"].DeepClone(),["rounds"]=export["rounds"].DeepClone(),["finalScore"]=m["score"].DeepClone(),["result"]=m["result"].DeepClone(),["firstSequence"]=1,["lastSequence"]=m["lastSequence"].DeepClone(),["perspectives"]=new JArray(Enumerable.Range(0,(int)mode["playerCount"])),["replayAvailable"]=true,["replayAvailabilityReason"]="AVAILABLE"};
            var timeline=new ReplayTimeline(manifest,events);var final=timeline.Seek(timeline.Count).Data;
            Check(JToken.DeepEquals(final["board"],runtime["board"]),"FINAL_BOARD");
            Check(JToken.DeepEquals(final["scores"],m["score"]),"FINAL_SCORE");
            Check(JToken.DeepEquals(final["matchResult"],m["result"]),"FINAL_WINNER");
            Check((long)final["sequence"]==(long)m["lastSequence"],"FINAL_SEQUENCE");
            Check((int)final["round"]==(int)m["currentRoundNumber"],"FINAL_ROUND");
            for(int seat=0;seat<(int)mode["playerCount"];seat++)Check(JToken.DeepEquals(final["hands"][seat],runtime["hands"][seat.ToString()]),"FINAL_HAND");
            var targets=new[]{0,200,50,400,125,timeline.Count,timeline.Count/2,timeline.Count-1}.Concat(timeline.RoundStarts).Select(n=>Math.Min(n,timeline.Count)).Distinct().ToArray();
            var clean=ReplayReducer.Initial(manifest);
            for(int i=0;i<=events.Count;i++) {
                if(targets.Contains(i))Check(JToken.DeepEquals(clean.Data,timeline.Seek(i).Data),"SEEK_PARITY");
                if(i<events.Count)clean=ReplayReducer.Apply(clean,events[i]);
            }
            var bad=(JObject)events[0].DeepClone();bad["sequence"]=2;Reject(()=>ReplayReducer.Apply(ReplayReducer.Initial(manifest),bad));
            bad["sequence"]=1;bad["type"]="UNKNOWN_GAMEPLAY";Reject(()=>ReplayReducer.Apply(ReplayReducer.Initial(manifest),bad));
            Reject(()=>new ReplayTimeline(manifest,events.Skip(1)));
            var mutated=timeline.Seek(50).Data;mutated["scores"]=new JArray(999,999);Check(!JToken.DeepEquals(mutated,timeline.Seek(50).Data),"IMMUTABLE_CHECKPOINT");
            foreach(var e in events){eventTypes.Add((string)e["type"]);if((string)e["type"]=="ROUND_FINISHED")finishes.Add((string)e["payload"]["finishType"]);}
            string key=(string)m["modeKey"];
            if(modes.Add(key))File.WriteAllText(Path.Combine(output,key+".json"),new JObject {["manifest"]=manifest,["events"]=new JArray(events),["expected"]=runtime}.ToString());
            count++;total+=events.Count;
        }
        Check(modes.SetEquals(new[]{"DUEL_1V1","PARTNERS_2V2_ONLINE"}),"BOTH_MODES");
        Console.WriteLine($"I4_REPLAY_CHECKS={checks} PASS MATCHES={count} EVENTS={total} REAL_FIRESTORE_CALLS=0");
        Console.WriteLine("HISTORICAL_FINISHES="+string.Join(",",finishes));Console.WriteLine("HISTORICAL_EVENTS="+string.Join(",",eventTypes));
    }
}
