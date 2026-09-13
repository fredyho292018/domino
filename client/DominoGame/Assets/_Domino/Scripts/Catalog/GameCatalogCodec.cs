using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Domino.Configuration;

namespace Domino.Catalog
{
    public sealed class GameCatalogCodec
    {
        public const int MaxBytes = 262144;
        public static readonly string[] DuelCapabilities = { "DUEL_TOPOLOGY", "RANDOM_START_METHOD", "HIGH_TILE_SELECTION", "EVEN_ODD_GUESS", "PREVIOUS_ROUND_WINNER_START", "BLOCKED_TIE_STARTER_WINS", "CAPICUA_SCORING_V1" };
        public GameCatalogSnapshot Read(string json)
        {
            if (string.IsNullOrEmpty(json) || Encoding.UTF8.GetByteCount(json)>MaxBytes) throw new FormatException("SIZE");
            using var reader=new JsonTextReader(new StringReader(json)) { MaxDepth=24,DateParseHandling=DateParseHandling.None };
            var root=JObject.Load(reader,new JsonLoadSettings { DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error });
            if(reader.Read()) throw new FormatException("TRAILING_DATA");
            Require(Int(root,"catalogSchemaVersion")==1,"SCHEMA");
            int version=Int(root,"catalogVersion"); Require(version>0,"VERSION");
            var modes=Array(root,"modes"); Require(modes.Count<=32,"MODES");
            var result=new List<GameModeSnapshot>(); var ids=new HashSet<string>();var keys=new HashSet<string>();
            foreach(var token in modes)
            {
                var m=token as JObject ?? throw new FormatException("MODE");
                string id=Text(m,"id"),key=Text(m,"key");Require(ids.Add(id)&&keys.Add(key),"DUPLICATE");
                int topology=Int(m,"topologyVersion"),players=Int(m,"playerCount");
                string teamMode=Text(m,"teamMode"); bool duel=teamMode=="NONE";
                Require(topology>0 && (duel ? players==2&&m["teamSize"]?.Type==JTokenType.Null : players==4&&Int(m,"teamSize")==2&&teamMode=="FIXED_TEAMS"),"TOPOLOGY");
                Require(Text(m,"availability")=="ALL","AVAILABILITY");
                var executions=Array(m,"executionModesSupported");Require(executions.Count==1&&executions[0].Type==JTokenType.String&&(string)executions[0]=="LOCAL","EXECUTION");
                int min=Int(m,"minHumans"),max=Int(m,"maxHumans");bool bots=Bool(m,"botsAllowed");
                Require(min>=1&&min<=max&&max<=players&&(bots||min==players),"HUMANS");
                var teams=Array(m,"seatTeams");
                Require(duel ? teams.Count==0 : teams.Count==2&&teams.All(t=>t is JArray a&&a.Count==2&&a.All(x=>x.Type==JTokenType.Integer)),"TEAMS");
                var r=m["ruleSet"] as JObject ?? throw new FormatException("RULES");
                Require(Int(r,"ruleSchemaVersion")==1,"RULE_SCHEMA");
                var capabilities=Array(r,"requiredCapabilities");
                Require(capabilities.All(x=>x.Type==JTokenType.String&&DuelCapabilities.Contains((string)x)),"CAPABILITY");
                if(duel) Require(capabilities.Count==DuelCapabilities.Length && DuelCapabilities.All(x=>capabilities.Values<string>().Contains(x)),"DUEL_CAPABILITIES");
                Require(Text(r,"tileSet")=="DOUBLE_N"&&Text(r,"reservePolicy")=="RESERVE","TILE_SET");
                string ruleId=Text(r,"id"),hash=Text(r,"contentHash");
                Require(ruleId==Text(m,"defaultRuleSetId")&&hash==Hash(r),"HASH_OR_BINDING");
                Require(DateTimeOffset.TryParse(Text(r,"createdAt"),out _),"METADATA");
                // Enforce JSON scalar types before mapping; Json.NET must not coerce strings into integers.
                foreach(var field in new[]{"version","maxPip","tilesPerPlayer","targetScore"}) Int(r,field);
                foreach(var field in new[]{"dealPolicy","firstRoundStarting","followingRoundStarting","blockedPolicy","finishScoring","blockedScoring","tiePolicy"})
                    Require(r[field] is JObject,"POLICY");
                Int((JObject)r["firstRoundStarting"],"seat");Int((JObject)r["followingRoundStarting"],"seat");
                Int((JObject)r["finishScoring"],"bonus");Int((JObject)r["blockedScoring"],"bonus");
                Int((JObject)r["tiePolicy"],"award");Int((JObject)r["tiePolicy"],"nextRoundMultiplier");
                Require(Array(r,"turnOrder").All(x=>x.Type==JTokenType.Integer)&&Array((JObject)r["dealPolicy"],"seatOrder").All(x=>x.Type==JTokenType.Integer),"ORDER");
                var dto=new GameConfigurationDto {
                    id=ruleId,version=Int(r,"version"),schemaVersion=1,rulesetVersion="1.0",playerCount=players,teamMode=teamMode,
                    teamAssignments=teams.Select(t=>new TeamAssignmentDto {members=t.Values<int>().ToArray()}).ToArray(),
                    maxPip=Int(r,"maxPip"),tilesPerPlayer=Int(r,"tilesPerPlayer"),targetScore=Int(r,"targetScore"),
                    deal=r["dealPolicy"].ToObject<DealPolicyDto>(),drawPolicy=Text(r,"drawPolicy"),turnOrder=r["turnOrder"].ToObject<int[]>(),
                    firstRoundStarting=r["firstRoundStarting"].ToObject<StartingPolicyDto>(),followingRoundStarting=r["followingRoundStarting"].ToObject<StartingPolicyDto>(),
                    openingTilePolicy=Text(r,"openingTilePolicy"),passPolicy=Text(r,"passPolicy"),blocked=r["blockedPolicy"].ToObject<BlockedPolicyDto>(),
                    finishScoring=r["finishScoring"].ToObject<ScoringPolicyDto>(),blockedScoring=r["blockedScoring"].ToObject<ScoringPolicyDto>(),tie=r["tiePolicy"].ToObject<TiePolicyDto>(), turnPolicy=r["turnPolicy"]?.ToObject<TurnPolicyDto>(),capicuaPolicy=r["capicuaPolicy"]?.ToObject<CapicuaPolicyDto>() };
                DisconnectPolicySnapshot online=null;
                if(duel) {
                    Require(!bots&&min==2&&max==2,"DUEL_HUMANS");
                    var o=m["onlinePolicy"] as JObject??throw new FormatException("ONLINE_POLICY");
                    Require(Int(o,"reconnectWindowSeconds")==180&&Bool(o,"turnClockContinuesWhileDisconnected")&&Bool(o,"autoPlayWhileDisconnected"),"ONLINE_POLICY");
                    online=new DisconnectPolicySnapshot(180,true,true);
                    var t=r["turnPolicy"] as JObject??throw new FormatException("TURN_POLICY");
                    Require(Int(t,"timeLimitSeconds")==60&&Bool(t,"autoPlayOnTimeout")&&Text(t,"autoPlayPolicy")=="FIRST_VALID_MOVE","TURN_POLICY");
                    var c=r["capicuaPolicy"] as JObject??throw new FormatException("CAPICUA_POLICY");
                    Require(Int(c,"pipMultiplier")==2&&!Bool(c,"multiplyBonus"),"CAPICUA_POLICY");
                }
                result.Add(new GameModeSnapshot(id,key,Text(m,"nameKey"),Text(m,"descriptionKey"),Text(m,"iconKey"),Bool(m,"active"),Int(m,"sortOrder"),topology,min,max,bots,
                    new RuleSetSnapshot(GameConfigurationValidator.Validate(dto),hash,capabilities.Values<string>().Select(x=>(RuleCapability)Enum.Parse(typeof(RuleCapability),x)).ToArray()), online));
            }
            return new GameCatalogSnapshot(version,result);
        }
        public static string Hash(JObject rule)
        {
            var semantic=(JObject)rule.DeepClone();semantic.Remove("createdAt");semantic.Remove("updatedAt");semantic.Remove("contentHash");
            using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Sort(semantic).ToString(Formatting.None)))).Replace("-","").ToLowerInvariant();
        }
        static JToken Sort(JToken t) => t is JObject o ? new JObject(o.Properties().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>new JProperty(p.Name,Sort(p.Value))))
            : t is JArray a ? new JArray(a.Select(Sort)) : t.DeepClone();
        static JArray Array(JObject o,string k)=>o[k] as JArray??throw new FormatException(k);
        static string Text(JObject o,string k)=>o[k]?.Type==JTokenType.String&&!string.IsNullOrWhiteSpace((string)o[k])?(string)o[k]:throw new FormatException(k);
        static int Int(JObject o,string k)=>o[k]?.Type==JTokenType.Integer?checked((int)o[k]):throw new FormatException(k);
        static bool Bool(JObject o,string k)=>o[k]?.Type==JTokenType.Boolean?(bool)o[k]:throw new FormatException(k);
        static void Require(bool value,string category) { if(!value)throw new FormatException(category); }
    }
}
