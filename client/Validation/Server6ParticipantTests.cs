using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Domino.Editor;
class Server6ParticipantTests {
 static int checks;
 static JObject a,h,r;static Dictionary<string,int> ids;
 static void Reset(){
  ids=Server6ParticipantValidator.Match1Expected.ToDictionary(x=>x.Key,x=>x.Value);
  JArray Rows(bool online)=>new JArray(Enumerable.Range(0,4).Select(n=>new JObject{["seat"]=n,["teamId"]=online?JValue.CreateNull():new JValue(n%2),["displayNameSnapshot"]="same name",["controlType"]="REMOTE_HUMAN"}));
  var rules=new JObject{["effectiveModeJson"]=new JObject{["playerCount"]=4,["seatTeams"]=new JArray(new JArray(0,2),new JArray(1,3))}.ToString()};
  a=new JObject{["publicState"]=new JObject{["matchId"]="synthetic",["participants"]=Rows(true)},["privateState"]=new JObject{["seat"]=0},["ruleSnapshot"]=rules};
  h=new JObject{["history"]=new JObject{["matchId"]="synthetic"},["selfSeat"]=0,["participants"]=Rows(false),["teams"]=new JArray(new JArray(0,2),new JArray(1,3))};
  r=new JObject{["matchId"]="synthetic",["selfSeat"]=0,["participants"]=Rows(false),["teams"]=h["teams"].DeepClone(),["ruleSnapshot"]=rules.DeepClone()};
 }
 static void Validate()=>Server6ParticipantValidator.Validate(a,h,r,Server6ParticipantValidator.Match1Expected,ids,0);
 static void Test(string name,Action mutation,bool pass=false){Reset();mutation();bool accepted=true;try{Validate();}catch(InvalidOperationException){accepted=false;}if(accepted!=pass)throw new Exception(name);checks++;Console.WriteLine(name+"=PASS");}
 static void Main(){
  Test("NULL_SNAPSHOT_TEAM_REGRESSION",()=>{},true);
  Test("WRONG_IDENTITY",()=>{ids.Remove("SLOT_01");ids.Add("UNKNOWN",1);});
  Test("WRONG_SEAT_SAME_TEAM",()=>{ids["SLOT_01"]=3;ids["SLOT_02"]=1;});
  Test("WRONG_TEAM",()=>r["participants"][0]["teamId"]=1);
  Test("DUPLICATE_SEAT",()=>r["participants"][1]["seat"]=0);
  Test("DUPLICATE_IDENTITY",()=>ids["SLOT_02"]=1);
  Test("MISSING",()=>((JArray)r["participants"]).RemoveAt(3));
  Test("EXTRA",()=>((JArray)r["participants"]).Add(r["participants"][0].DeepClone()));
  Test("UNEXPECTED_SEAT",()=>r["participants"][3]["seat"]=4);
  Test("DISPLAY_NAME_NOT_IDENTITY",()=>r["participants"][0]["displayNameSnapshot"]="renamed",true);
  Test("COLLECTION_ORDER",()=>r["participants"]=new JArray(((JArray)r["participants"]).Reverse().Select(x=>x.DeepClone())),true);
  Test("TEAM_MEMBER_ORDER",()=>r["teams"]=new JArray(new JArray(2,0),new JArray(3,1)),true);
  Test("WRONG_FROZEN_MAPPING",()=>{a["ruleSnapshot"]["effectiveModeJson"]=new JObject{["playerCount"]=4,["seatTeams"]=new JArray(new JArray(0,1),new JArray(2,3))}.ToString();r["ruleSnapshot"]=a["ruleSnapshot"].DeepClone();});
  Test("WRONG_HISTORY_TEAM",()=>h["participants"][2]["teamId"]=1);
  Test("WRONG_SELF",()=>r["selfSeat"]=1);
  Test("WRONG_MATCH",()=>r["matchId"]="other");
  Test("DUPLICATE_RULE_SEAT",()=>r["teams"]=new JArray(new JArray(0,0),new JArray(1,3)));
  var oldCulture=System.Globalization.CultureInfo.CurrentCulture;
  try{System.Globalization.CultureInfo.CurrentCulture=new System.Globalization.CultureInfo("es-ES");
   var proof=JObject.Parse(new JObject{["matchId"]="synthetic",["environment"]="TEST",["project"]="teamfho-domino",["source"]="AUTHENTICATED_SELF_SEATS",["observedAt"]=DateTimeOffset.UtcNow.ToString("O"),["slots"]=new JArray(new[]{1,2,3}.Select(n=>new JObject{["label"]="SLOT_0"+n,["selfSeat"]=n,["snapshotSeat"]=n}))}.ToString());
   var observed=Server6ParticipantValidator.ReadEvidence(proof,(string)proof["matchId"]);
   if(observed.Count!=3)throw new Exception("Evidence shape");checks++;Console.WriteLine("EVIDENCE_DATE_ES=PASS");
  }finally{System.Globalization.CultureInfo.CurrentCulture=oldCulture;}
  Console.WriteLine("PARTICIPANT_VALIDATOR_CHECKS="+checks);
 }
}
