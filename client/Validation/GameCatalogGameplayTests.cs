using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domino.Catalog;
using Domino.Client;
using Domino.Configuration;
using Domino.Core;
using Domino.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

static class GameCatalogGameplayTests
{
    static int checks;
    static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
    sealed class Api:IGameCatalogApi {
        public string Json;public bool Fail;public int Calls;public TaskCompletionSource<string> Pending;
        public Task<string> FetchAsync(CancellationToken t){Calls++;if(Fail)throw new IOException();return Pending?.Task??Task.FromResult(Json);}
    }
    sealed class Cache:IGameCatalogCache { public CatalogCacheEntry Value;public CatalogCacheEntry Read()=>Value;public void Write(CatalogCacheEntry e)=>Value=e; }
    static async Task Main()
    {
        string root=Environment.GetEnvironmentVariable("DOMINO_M1_ROOT");
        string bundled=File.ReadAllText(Path.Combine(root,"client/Validation/GameCatalogV1Fixture.json"));
        string legacy=File.ReadAllText(Path.Combine(root,"client/DominoGame/Assets/_Domino/Config/double-nine-partners-v1.json"));
        var old=GameConfigurationValidator.Validate(JsonConvert.DeserializeObject<GameConfigurationDto>(legacy));
        var api=new Api{Json=bundled,Pending=new TaskCompletionSource<string>()};var cache=new Cache();
        var service=new GameCatalogService(api,cache,bundled);
        var inFlight=service.RefreshAsync();
        var frozen=service.ResolveMatch();var session=new SessionSetup(frozen);
        Check(!inFlight.IsCompleted&&frozen.Source==GameCatalogSource.Bundled,"START_BEFORE_REMOTE_READY");
        Check(session.LocalPlayerSeat==0&&session.Controls.SequenceEqual(new[]{ParticipantControl.LOCAL_HUMAN,ParticipantControl.BOT,ParticipantControl.BOT,ParticipantControl.BOT}),"EXPLICIT_CONTROLS");
        Check(session.Mode.TeamA.SequenceEqual(new[]{0,2})&&session.Mode.TeamB.SequenceEqual(new[]{1,3}),"CATALOG_TOPOLOGY");
        Check(JsonConvert.SerializeObject(old)==JsonConvert.SerializeObject(session.Configuration),"ALL_FIELDS_EQUIVALENT");
        api.Pending.SetResult(bundled);await inFlight;api.Pending=null;
        Check(service.ResolveMatch().Source==GameCatalogSource.Remote,"REMOTE_SOURCE");
        Check(frozen.Source==GameCatalogSource.Bundled&&frozen.CatalogVersion==1,"FREEZE_DURING_INITIAL_FETCH");
        for(int i=0;i<20;i++)service.ResolveMatch();Check(api.Calls==1,"RESOLVE_NEVER_FETCHES");
        string NewCatalog(){var o=JObject.Parse(bundled);o["catalogVersion"]=2;var r=(JObject)o["modes"][0]["ruleSet"];r["version"]=2;r["targetScore"]=250;r["contentHash"]=GameCatalogCodec.Hash(r);return o.ToString();}
        api.Json=NewCatalog();await service.RefreshAsync(true);
        Check(session.Rules.CatalogVersion==1&&session.Configuration.TargetScore==200,"ACTIVE_MATCH_UNCHANGED");
        var next=new SessionSetup(service.ResolveMatch());Check(next.Rules.CatalogVersion==2&&next.Configuration.TargetScore==250,"NEXT_MATCH_UPDATED");
        var currentCache=cache.Value;api.Fail=true;
        var restored=new GameCatalogService(api,cache,bundled);await restored.RefreshAsync(true);
        Check(restored.ResolveMatch().Source==GameCatalogSource.Cache&&restored.ResolveMatch().Configuration.TargetScore==250,"PERSISTED_RECONSTRUCTION");
        api.Fail=false;
        foreach(var bad in new[]{"schema","turn","capability","unsupported-mode","bots"}) {
            var o=JObject.Parse(bundled);o["catalogVersion"]=3;var m=(JObject)o["modes"][0];var r=(JObject)m["ruleSet"];
            if(bad=="schema")o["catalogSchemaVersion"]=99;
            if(bad=="turn")r["turnOrder"]=new JArray(0,1,1,3);
            if(bad=="capability")r["requiredCapabilities"]=new JArray("UNKNOWN");
            if(bad=="unsupported-mode")m["key"]="FUTURE_MODE";
            if(bad=="bots")m["botsAllowed"]=false;
            r["contentHash"]=GameCatalogCodec.Hash(r);api.Json=o.ToString();await restored.RefreshAsync(true);
            Check(ReferenceEquals(currentCache,cache.Value)&&restored.ResolveMatch().Configuration.TargetScore==250,"INVALID_NOT_USED_"+bad);
        }
        var changed=JObject.Parse(bundled);changed["catalogVersion"]=4;changed["modes"][0]["seatTeams"]=new JArray(new JArray(0,1),new JArray(2,3));
        api.Json=changed.ToString();await service.RefreshAsync(true);
        var topology=new SessionSetup(service.ResolveMatch());Check(topology.Mode.TeamA.SequenceEqual(new[]{0,1})&&topology.Configuration.GetTeamForPlayer(1)==0,"NO_SILENT_TEAM_OVERRIDE");
        api.Fail=true;cache.Value=new CatalogCacheEntry("corrupt",DateTimeOffset.UtcNow);
        var fallback=new GameCatalogService(api,cache,bundled);await fallback.RefreshAsync(true);
        Check(fallback.ResolveMatch().Source==GameCatalogSource.Bundled,"CORRUPT_CACHE_FALLBACK");
        var converted=fallback.ResolveMatch().Configuration;
        string before=RegressionTrace.Compute(()=>new ClientGame(new GameRules(old)));
        string after=RegressionTrace.Compute(()=>new ClientGame(new GameRules(converted)));
        Check(before==after&&after=="CF8D7610D54AF6C74C3A6F1A20A3FAE0881C2FAF8A46F6E78EC0273893DB26EC","CURRENT_SCORING_GOLDEN_100_MATCHES");
        Differential(old,converted);
        Golden(new GameRules(converted));
        Console.WriteLine("M2_UNITY_TESTS=PASS CHECKS="+checks+"; DIFFERENTIAL_ROUNDS=250; GOLDEN_MATCHES=100; CURRENT_TRACE="+after);
    }
    static void Differential(GameConfigurationSnapshot a,GameConfigurationSnapshot b)
    {
        for(int seed=0;seed<250;seed++) {
            var x=new ClientGame(new GameRules(a));var y=new ClientGame(new GameRules(b));var ex=new List<string>();var ey=new List<string>();
            string Event(GameEvent e)=>$"{e.Type}:{e.Player}:{e.Tile.SideA}:{e.Tile.SideB}:{e.ChainIndex}";
            x.Changed+=e=>ex.Add(Event(e));y.Changed+=e=>ey.Add(Event(e));x.Start(seed);y.Start(seed);
            Check(x.Reserve.SequenceEqual(y.Reserve),"RESERVE");
            while(!x.Finished) {
                int p=x.CurrentPlayer;Check(p==y.CurrentPlayer,"TURN");
                for(int s=0;s<4;s++)Check(x.Hand(s).SequenceEqual(y.Hand(s)),"HAND");
                foreach(var t in x.Hand(p))foreach(var end in new[]{ChainEnd.Left,ChainEnd.Right,ChainEnd.Auto})Check(x.CanPlay(t,end)==y.CanPlay(t,end),"LEGAL_ENDS");
                var legal=x.Hand(p).Where(t=>x.CanPlay(t)).ToArray();
                if(legal.Length==0)Check(x.TryPass(p)==y.TryPass(p),"PASS");
                else { var tile=legal[(seed+ex.Count)%legal.Length];var end=x.CanPlay(tile,ChainEnd.Left)&&seed%2==0?ChainEnd.Left:ChainEnd.Auto;Check(x.TryPlay(p,tile,end)==y.TryPlay(p,tile,end),"MOVE"); }
                Check(x.Chain.Select(t=>t.ToString()).SequenceEqual(y.Chain.Select(t=>t.ToString())),"CHAIN_ORIENTATION");
            }
            Check(y.Finished&&JsonConvert.SerializeObject(x.Result)==JsonConvert.SerializeObject(y.Result),"ROUND_RESULT");
            Check(x.Match.Score(0)==y.Match.Score(0)&&x.Match.Score(1)==y.Match.Score(1)&&x.Match.WinnerSide==y.Match.WinnerSide,"MATCH_SCORE");
            Check(ex.SequenceEqual(ey),"EVENTS");
        }
    }
    static void Golden(GameRules rules)
    {
        Check(RoundScoring.Evaluate(rules,new[]{0,24,99,31},0,false,1).Award==65,"SALIDA_OPPONENTS_PLUS10");
        Check(RoundScoring.Evaluate(rules,new[]{5,24,99,31},-1,true,1).Award==55,"TRANQUE_PLUS0");
        Check(RoundScoring.Evaluate(rules,new[]{5,20,5,30},-1,true,1).WinnerPlayer==0,"SAME_TEAM_MINIMUM_FIRST_SEAT");
        var tie=RoundScoring.Evaluate(rules,new[]{5,5,90,80},-1,true,1);Check(tie.Tie&&tie.Award==0,"CROSS_TEAM_TIE");
        var match=new MatchState(rules);match.Apply(tie);match.NextRound();match.Apply(tie);match.NextRound();Check(match.Multiplier==2,"REPEATED_TIE_X2");
        match.Apply(RoundScoring.Evaluate(rules,new[]{0,24,99,31},0,false,match.Multiplier));Check(match.Score(0)==130&&match.Multiplier==1,"MULTIPLY_TOTAL_RESET");
        foreach(int target in new[]{200,201}) {match=new MatchState(rules);match.Apply(RoundScoring.Evaluate(rules,new[]{0,target-10,50,0},0,false,1));Check(match.Finished,"TARGET_REACHED_OR_EXCEEDED");}
        var g=new ClientGame(rules);g.Start(42);Check(!g.TryPass(0),"PASS_DENIED_LEGAL");
        // Test-only fixture: isolate both-end and consecutive-pass transitions without changing production engine.
        var hands=(List<DominoTile>[])typeof(ClientGame).GetField("hands",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(g);
        var chain=(List<DominoTile>)typeof(ClientGame).GetField("chain",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(g);
        chain.Add(new DominoTile(6,6));hands[0].Clear();hands[0].Add(new DominoTile(6,5));hands[0].Add(new DominoTile(0,0));
        Check(g.CanPlay(hands[0][0],ChainEnd.Left)&&g.CanPlay(hands[0][0],ChainEnd.Right),"BOTH_ENDS_VALID");
        g.TryPlay(0,hands[0][0]);Check(g.RightEnd==5&&g.LeftEnd==6,"AUTO_PREFERS_RIGHT");
        hands[3].Clear();hands[3].Add(new DominoTile(1,1));Check(g.TryPass(3),"PASS_ALLOWED");
        hands[2].Clear();hands[2].Add(new DominoTile(5,4));hands[2].Add(new DominoTile(2,2));g.TryPlay(2,hands[2][0]);
        Check((int)typeof(ClientGame).GetField("consecutivePasses",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(g)==0,"PLAY_RESETS_PASSES");
        for(int i=0;i<4;i++){int p=g.CurrentPlayer;hands[p].Clear();hands[p].Add(new DominoTile(p,p));g.TryPass(p);}
        Check(g.Finished&&g.Blocked,"TRANQUE_FOUR_CONSECUTIVE");
    }
}
