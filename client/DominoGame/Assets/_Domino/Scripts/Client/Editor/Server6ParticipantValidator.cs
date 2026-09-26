using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Domino.Editor {
 // Validation tooling only. Public participant DTOs contain seats, not UID identities.
 // Identity evidence must come from authenticated selfSeat responses bound to this match.
 public static class Server6ParticipantValidator {
  static void Require(bool ok,string code){if(!ok)throw new InvalidOperationException(code);}
  static Dictionary<int,JToken> Seats(JToken value){
   Require(value is JArray && ((JArray)value).Count==4,"PARTICIPANT_COUNT");
   var rows=((JArray)value).ToArray();
   Require(rows.All(p=>p["seat"]?.Type==JTokenType.Integer),"SEAT_TYPE");
   var keys=rows.Select(p=>(int)p["seat"]).ToArray();
   Require(keys.Distinct().Count()==4&&keys.OrderBy(x=>x).SequenceEqual(new[]{0,1,2,3}),"SEAT_MEMBERSHIP");
   return rows.ToDictionary(p=>(int)p["seat"]);
  }
  static Dictionary<int,int> Teams(JToken value){
   Require(value is JArray && ((JArray)value).Count==2,"TEAM_COUNT");
   var result=new Dictionary<int,int>();int team=0;
   foreach(var group in (JArray)value){
    Require(group is JArray && ((JArray)group).Count==2,"TEAM_SIZE");
    foreach(var seat in (JArray)group){Require(seat.Type==JTokenType.Integer,"TEAM_SEAT_TYPE");int n=(int)seat;Require(n>=0&&n<4&&!result.ContainsKey(n),"TEAM_SEAT_MEMBERSHIP");result.Add(n,team);}
    team++;
   }
   return result;
  }
  public static void Validate(JObject authority,JObject history,JObject replay,
      IReadOnlyDictionary<string,int> expected,IReadOnlyDictionary<string,int> authenticated,int selfSeat){
   Require(expected.Count==4&&authenticated.Count==4,"IDENTITY_COUNT");
   Require(expected.Values.Distinct().Count()==4&&expected.Values.OrderBy(x=>x).SequenceEqual(new[]{0,1,2,3}),"EXPECTED_IDENTITIES");
   Require(authenticated.Values.Distinct().Count()==4&&expected.All(p=>authenticated.TryGetValue(p.Key,out var s)&&s==p.Value),"IDENTITY_SEAT_MISMATCH");
   Require(authenticated.TryGetValue("UNITY_USER",out var unity)&&unity==selfSeat,"CURRENT_IDENTITY_SEAT");
   var a=Seats(authority["publicState"]?["participants"]);var h=Seats(history["participants"]);var r=Seats(replay["participants"]);
   string id=(string)authority["publicState"]?["matchId"];
   Require(!string.IsNullOrWhiteSpace(id)&&(string)replay["matchId"]==id&&(string)history["history"]?["matchId"]==id,"MATCH_BINDING");
   Require((int?)authority["privateState"]?["seat"]==selfSeat&&(int?)history["selfSeat"]==selfSeat&&(int?)replay["selfSeat"]==selfSeat,"SELF_SEAT");
   Require(JToken.DeepEquals(authority["ruleSnapshot"],replay["ruleSnapshot"]),"FROZEN_RULES_DIFFER");
   var mode=JObject.Parse((string)authority["ruleSnapshot"]["effectiveModeJson"]);
   Require((int?)mode["playerCount"]==4,"RULE_PLAYER_COUNT");
   var teams=Teams(mode["seatTeams"]);var ht=Teams(history["teams"]);var rt=Teams(replay["teams"]);
   // Match 1's approved frozen topology; reject a changed, internally consistent mapping too.
   Require(teams.All(p=>p.Value==p.Key%2),"FROZEN_TEAM_MAPPING");
   foreach(int seat in Enumerable.Range(0,4)){
    Require(ht[seat]==teams[seat]&&rt[seat]==teams[seat],"TEAM_MEMBERSHIP");
    Require((int?)h[seat]["teamId"]==teams[seat]&&(int?)r[seat]["teamId"]==teams[seat],"PERSISTED_TEAM");
    // The online projection omits teamId. A populated value must still be correct.
    Require(a[seat]["teamId"]==null||a[seat]["teamId"].Type==JTokenType.Null||(int?)a[seat]["teamId"]==teams[seat],"SNAPSHOT_TEAM");
    Require((string)a[seat]["controlType"]==(string)h[seat]["controlType"]&&(string)a[seat]["controlType"]==(string)r[seat]["controlType"],"CONTROL_TYPE");
   }
  }
  public static Dictionary<string,int> ReadEvidence(JObject evidence,string matchId){
   Require((string)evidence["matchId"]==matchId&&(string)evidence["environment"]=="TEST"&&(string)evidence["project"]=="teamfho-domino","EVIDENCE_SCOPE");
   Require((string)evidence["source"]=="AUTHENTICATED_SELF_SEATS","EVIDENCE_SOURCE");
   var stamp=evidence["observedAt"];
   var at=stamp is JValue v && v.Value is DateTime date ? new DateTimeOffset(date.ToUniversalTime()) : DateTimeOffset.Parse((string)stamp,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind);
   Require(at<=DateTimeOffset.UtcNow.AddMinutes(1)&&at>=DateTimeOffset.UtcNow.AddHours(-2),"EVIDENCE_EXPIRED");
   var rows=(JArray)evidence["slots"];Require(rows.Count==3,"EVIDENCE_COUNT");
   var result=new Dictionary<string,int>();
   foreach(var row in rows){string label=(string)row["label"];Require(new[]{"SLOT_01","SLOT_02","SLOT_03"}.Contains(label)&&!result.ContainsKey(label),"EVIDENCE_IDENTITY");Require((int?)row["selfSeat"]==(int?)row["snapshotSeat"],"EVIDENCE_SEAT");result.Add(label,(int)row["selfSeat"]);}
   return result;
  }
  public static readonly IReadOnlyDictionary<string,int> Match1Expected=new Dictionary<string,int>{{"UNITY_USER",0},{"SLOT_01",1},{"SLOT_02",3},{"SLOT_03",2}};
 }
}
