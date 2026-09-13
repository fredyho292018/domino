using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Domino.Catalog;
using Domino.Configuration;
using Domino.Game;
using Domino.Identity;
using Domino.Infrastructure.Api;

static class GameCatalogTests
{
    static int checks;
    static void Check(bool b,string message) { checks++;if(!b)throw new Exception(message); }
    static void Reject(Action action,string name) { bool failed=false;try{action();}catch{failed=true;}Check(failed,name); }
    sealed class Api:IGameCatalogApi { public string Json;public int Calls;public bool Fail; public Task<string> FetchAsync(CancellationToken t){Calls++;if(Fail)throw new IOException();return Task.FromResult(Json);} }
    sealed class Cache:IGameCatalogCache { public CatalogCacheEntry Value;public CatalogCacheEntry Read()=>Value;public void Write(CatalogCacheEntry e)=>Value=e; }
    sealed class Tokens:IAuthTokenProvider { public int Calls;public Task<string> GetIdTokenAsync(bool refresh,CancellationToken t){Calls++;return Task.FromResult("test-fixture");} }
    sealed class Transport:IApiTransport { public string Json;public int Calls;public Uri Url;public Task<ApiHttpResponse> SendAsync(string method,Uri url,string json,string token,int seconds,CancellationToken t){Url=url;Calls++;return Task.FromResult(new ApiHttpResponse(Calls==1?401:200,Json));} }
    static async Task Main()
    {
        var root=Environment.GetEnvironmentVariable("DOMINO_M1_ROOT");
        string json=File.ReadAllText(Path.Combine(root,"client/DominoGame/Assets/_Domino/Resources/GameCatalogFallback.json"));
        string canonical=File.ReadAllText(Path.Combine(root,"client/DominoGame/Assets/_Domino/Config/double-nine-partners-v1.json"));
        var codec=new GameCatalogCodec();var snapshot=codec.Read(json);
        var current=GameConfigurationValidator.Validate(JsonConvert.DeserializeObject<GameConfigurationDto>(canonical));
        var converted=snapshot.Modes.Single().RuleSet.Configuration;
        Check(JsonConvert.SerializeObject(current)==JsonConvert.SerializeObject(converted),"FULL_CANONICAL_PARITY");
        Check(current.ReserveCount==15&&current.TotalTiles==55,"derived reserve");
        Check(RegressionTrace.Compute(()=>new ClientGame(new GameRules(current)))==RegressionTrace.Compute(()=>new ClientGame(new GameRules(converted))),"CURRENT_SCORING_GOLDEN_TRACE_PARITY");
        string Mutate(string path,JToken value,bool rehash=true){var o=JObject.Parse(json);o.SelectToken(path).Replace(value);if(rehash){var r=(JObject)o["modes"][0]["ruleSet"];r["contentHash"]=GameCatalogCodec.Hash(r);}return o.ToString();}
        foreach(var entry in new[]{("catalogSchemaVersion",(JToken)2),("modes[0].playerCount",(JToken)2),("modes[0].ruleSet.ruleSchemaVersion",(JToken)2),
            ("modes[0].ruleSet.targetScore",(JToken)0),("modes[0].ruleSet.tilesPerPlayer",(JToken)20),("modes[0].ruleSet.firstRoundStarting.seat",(JToken)4),
            ("modes[0].ruleSet.followingRoundStarting.seat",(JToken)(-1)),("modes[0].ruleSet.requiredCapabilities",new JArray("DRAW")),
            ("modes[0].ruleSet.dealPolicy.seatOrder",new JArray(0,1,2,2)),("modes[0].ruleSet.turnOrder",new JArray(0,3,2,9)),
            ("modes[0].ruleSet.maxPip",(JToken)1),("modes[0].ruleSet.finishScoring.bonus",(JToken)(-1)),
            ("modes[0].ruleSet.targetScore",(JToken)"200"),("modes[0].defaultRuleSetId",(JToken)"wrong"),
            ("modes[0].seatTeams",new JArray(new JArray(0,2),new JArray(1,2)))})
            Reject(()=>codec.Read(Mutate(entry.Item1,entry.Item2)),entry.Item1);
        Reject(()=>codec.Read(Mutate("modes[0].ruleSet.targetScore",201,false)),"hash tamper");
        Reject(()=>codec.Read(json+"{}"),"trailing");Reject(()=>codec.Read("{}"),"missing");
        var at=DateTimeOffset.UtcNow;var api=new Api{Json=json};var cache=new Cache();
        var service=new GameCatalogService(api,cache,json,now:()=>at);
        Check(service.Source==GameCatalogSource.Bundled,"first install offline fallback");
        await service.RefreshAsync();Check(api.Calls==1&&service.Source==GameCatalogSource.Remote&&cache.Value!=null,"download persisted");
        await service.RefreshAsync();Check(api.Calls==1,"ttl");
        var restart=new GameCatalogService(api,cache,json,now:()=>at);Check(restart.Source==GameCatalogSource.Cache,"restart cache");
        at=at.AddSeconds(301);api.Fail=true;await restart.RefreshAsync();Check(restart.Source==GameCatalogSource.Cache,"offline cache");
        api.Fail=false;api.Json="{}";var good=cache.Value;await restart.RefreshAsync(true);Check(ReferenceEquals(good,cache.Value),"invalid preserves cache");
        foreach(var bad in new[]{Mutate("catalogSchemaVersion",2),Mutate("modes[0].ruleSet.requiredCapabilities",new JArray("future"))}){
            api.Json=bad;await restart.RefreshAsync(true);Check(ReferenceEquals(good,cache.Value),"incompatible preserves cache");}
        cache.Value=new CatalogCacheEntry("broken",at);Check(new GameCatalogService(api,cache,json).Source==GameCatalogSource.Bundled,"corrupt cache fallback");
        var temp=Path.Combine(root,"client/Validation/Generated/M1Tests/cache-"+Guid.NewGuid()+".json");
        var disk=new FileGameCatalogCache(temp);disk.Write(new CatalogCacheEntry(json,at));disk.Write(new CatalogCacheEntry(json,at.AddSeconds(1)));
        Check(codec.Read(disk.Read().Json).CatalogVersion==1,"atomic file replace reload");
        File.WriteAllText(temp,"corrupt");Check(new GameCatalogService(api,disk,json).Source==GameCatalogSource.Bundled,"disk corruption");File.Delete(temp);
        var tokens=new Tokens();var transport=new Transport{Json=json};var http=new GameCatalogApi(new DominoApiConfiguration(true,"http://127.0.0.1:8080",15,"LOCAL",true),tokens,transport);
        Check(await http.FetchAsync(default)==json&&tokens.Calls==2&&transport.Url.AbsolutePath=="/api/v1/game-modes","auth refresh and exact endpoint");
        Reject(()=>codec.Read(json.Replace("\"catalogVersion\": 1","\"catalogVersion\": 1, \"catalogVersion\": 2")),"duplicates");
        var rules=new GameRules(current);
        Check(RoundScoring.Evaluate(rules,new[]{5,20,5,30},-1,true,1).WinnerPlayer==0,"same team minimum first seat");
        Check(RoundScoring.Evaluate(rules,new[]{5,5,50,60},-1,true,1).Tie,"cross team tie");
        foreach(int target in new[]{200,210}){
            var match=new MatchState(rules);match.Apply(RoundScoring.Evaluate(rules,new[]{0,target-10,70,0},0,false,1));
            Check(match.Finished&&match.Score(0)==target,"at or exceeds target");}
        var translations=JObject.Parse(File.ReadAllText(Path.Combine(root,"client/DominoGame/Assets/_Domino/Editor/Localization/Translations.json")));
        foreach(string key in new[]{"mode.team_match.title","mode.team_match.subtitle","rules.double_nine","rules.no_draw"})
            Check(((JArray)translations["entries"]).Any(t=>(string)t["key"]==key&&!string.IsNullOrWhiteSpace((string)t["en"])&&!string.IsNullOrWhiteSpace((string)t["es"])),"localization "+key);
        Console.WriteLine("M1_UNITY_TESTS=PASS CHECKS="+checks);
    }
}
