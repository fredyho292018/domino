using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domino.Catalog;
using Domino.Client;
using Domino.Configuration;
using Domino.Core;
using Domino.Game;

static class DuelGameplayTests
{
    static int checks;
    static void Check(bool b,string message) {checks++;if(!b)throw new Exception(message);}
    sealed class Api:IGameCatalogApi {public string Json; public Task<string> FetchAsync(CancellationToken t)=>Task.FromResult(Json);}
    sealed class Cache:IGameCatalogCache {public CatalogCacheEntry Value;public CatalogCacheEntry Read()=>Value;public void Write(CatalogCacheEntry e)=>Value=e;}
    static async Task Main()
    {
        var root=Environment.GetEnvironmentVariable("DOMINO_M1_ROOT");
        string v2=File.ReadAllText(Path.Combine(root,"client/DominoGame/Assets/_Domino/Resources/GameCatalogFallback.json"));
        string v1=File.ReadAllText(Path.Combine(root,"client/Validation/GameCatalogV1Fixture.json"));
        var api=new Api{Json=v2};var cache=new Cache();var catalog=new GameCatalogService(api,cache,v2);
        var frozen=catalog.ResolveMatch("DUEL_1V1");var c=frozen.Configuration;var rules=new GameRules(c);
        var session=new SessionSetup(frozen);
        Check(c.PlayerCount==2&&c.TeamCount==0&&c.ScoreOwner==ScoreOwnerKind.PLAYER&&c.ScoreOwnerCount==2,"TOPOLOGY");
        Check(session.SharedDevice&&session.Controls.All(x=>x==ParticipantControl.LOCAL_HUMAN)&&session.Mode.BotCount==0,"TWO_HUMANS_ZERO_BOTS");
        Check(c.TotalTiles==55&&c.ReserveCount==35&&c.TilesPerPlayer==10&&c.TargetScore==150,"RULES");
        Check(c.TurnPolicy.TimeLimitSeconds==60&&c.TurnPolicy.AutoPlayOnTimeout&&c.TurnPolicy.AutoPlayPolicy==AutoPlayPolicy.FIRST_VALID_MOVE,"TURN_POLICY");
        Check(frozen.Mode.OnlinePolicy.ReconnectWindowSeconds==180&&frozen.Mode.OnlinePolicy.TurnClockContinuesWhileDisconnected&&frozen.Mode.OnlinePolicy.AutoPlayWhileDisconnected,"DISCONNECT_POLICY");
        Check(RoundScoring.Evaluate(rules,new[]{0,32},0,false,1).Award==42,"NORMAL_42");
        var cap=RoundScoring.Evaluate(rules,new[]{0,32},0,false,1,0,true);
        Check(cap.Award==74&&cap.Bonus==10&&cap.FinishType==RoundFinishType.CAPICUA,"CAPICUA_74_BONUS_NOT_DOUBLED");
        Check(RoundScoring.Evaluate(rules,new[]{20,30},-1,true,1,0).WinnerPlayer==0,"BLOCK_LOW_0");
        Check(RoundScoring.Evaluate(rules,new[]{30,20},-1,true,1,0).WinnerPlayer==1,"BLOCK_LOW_1");
        foreach(var sample in new[]{(a:18,b:27,starter:0,winner:0,award:27),(a:27,b:18,starter:0,winner:1,award:27),(a:24,b:24,starter:0,winner:0,award:24),(a:24,b:24,starter:1,winner:1,award:24)}) {
            var blocked=RoundScoring.Evaluate(rules,new[]{sample.a,sample.b},-1,true,1,sample.starter);
            Check(blocked.WinnerPlayer==sample.winner&&blocked.Award==sample.award,"FINAL_TRANQUE_EXAMPLE");
            Check(blocked.Bonus==0&&blocked.Multiplier==1&&blocked.BasePoints==sample.award&&blocked.FinishType==RoundFinishType.BLOCKED,"TRANQUE_NO_BONUS_NO_CAPICUA_NO_DIFFERENCE");
            var g=new ClientGame(rules);g.Start(1,sample.starter);
            var h=Hands(g);Chain(g).Add(new DominoTile(0,0));
            h[0].Clear();h[1].Clear();
            // Totals 18/27/24, no zeros: both passes must be legitimate.
            h[0].Add(new DominoTile(9,9));h[1].Add(new DominoTile(9,9));
            if(sample.a>18)h[0].Add(sample.a==27?new DominoTile(4,5):new DominoTile(3,3));
            if(sample.b>18)h[1].Add(sample.b==27?new DominoTile(4,5):new DominoTile(3,3));
            Check(g.TryPass(sample.starter)&&g.TryPass(1-sample.starter),"FINAL_TRANQUE_LEGITIMATE_PASSES");
            Check(g.Result.WinnerPlayer==sample.winner&&g.Match.Score(sample.winner)==sample.award&&g.Match.Score(1-sample.winner)==0,"FINAL_TRANQUE_ENGINE_AWARD");
        }
        foreach(int starter in new[]{0,1})Check(RoundScoring.Evaluate(rules,new[]{25,25},-1,true,1,starter).WinnerPlayer==starter,"BLOCK_TIE_STARTER");
        Check(StarterSelection.Compare(new DominoTile(9,7),new DominoTile(8,6))==0,"HIGH_TILE");
        Check(StarterSelection.Compare(new DominoTile(9,5),new DominoTile(8,6))==-1,"REPEAT_EQUAL_SUM");
        Check(StarterSelection.ResolveGuess(new DominoTile(8,5),0,false)==1,"ODD_CORRECT");
        Check(StarterSelection.ResolveGuess(new DominoTile(8,5),0,true)==0,"EVEN_WRONG");
        var methods=new HashSet<StarterMethod>();
        for(int seed=0;seed<200;seed++) {
            var starter=new StarterSelection(c,seed);methods.Add(starter.Method);
            int attempts=0;
            while(!starter.Complete&&attempts++<100) {
                if(starter.WaitingForGuess)Check(starter.Guess(starter.GuesserSeat,true),"GUESS");
                else Check(starter.Choose(starter.NextSelectionSeat,starter.SelectedCandidateIndex==0?1:0),"CHOOSE");
            }
            Check(starter.Complete,"STARTER_COMPLETES");
            var game=new ClientGame(rules);
            var emitted=new List<GameEventType>();game.Changed+=e=>emitted.Add(e.Type);
            game.Start(seed,starter.WinnerSeat);
            Check(game.RoundStarterSeat==starter.WinnerSeat&&game.CurrentPlayer==starter.WinnerSeat,"STARTER_APPLIED");
            Check(game.Reserve.Count==35&&game.Hand(0).Count==10&&game.Hand(1).Count==10,"DEAL_COUNTS");
            Check(game.Hand(0).Concat(game.Hand(1)).Concat(game.Reserve).Distinct().Count()==55,"VIRTUAL_TILES_DONT_REMOVE_DECK");
            int moves=0;
            while(!game.Finished&&moves++<100) {
                int p=game.CurrentPlayer;var legal=game.Hand(p).Where(t=>game.CanPlay(t)).ToArray();
                if(legal.Length>0) {Check(!game.TryPlay(1-p,legal[0]),"WRONG_HUMAN_REJECTED");Check(!game.TryPass(p),"NO_PASS_WITH_LEGAL");Check(game.TryPlay(p,legal[0]),"PLAY");}
                else Check(game.TryPass(p),"LEGAL_PASS");
                if(!game.Finished)Check(game.CurrentPlayer==1-p,"ALTERNATION");
            }
            Check(game.Finished&&!game.Result.Tie,"ROUND_ENDS_WITH_WINNER");int winner=game.Winner;
            Check(emitted.All(e=>e!=GameEventType.TURN_STARTED&&e!=GameEventType.TURN_TIMEOUT&&e!=GameEventType.AUTO_PLAYED&&e!=GameEventType.PLAYER_DISCONNECTED&&e!=GameEventType.PLAYER_RECONNECTED&&e!=GameEventType.PLAYER_ABANDONED),"NO_FUTURE_ONLINE_EVENTS_EXECUTED");
            Check(session.Controls.All(x=>x==ParticipantControl.LOCAL_HUMAN),"HUMAN_OWNERSHIP_REMAINS_UNCHANGED");
            if(!game.Match.Finished) {Check(game.StartNextRound(seed+1),"NEXT_ROUND");Check(game.CurrentPlayer==winner&&game.RoundStarterSeat==winner&&game.PreviousRoundWinnerSeat==winner,"PREVIOUS_WINNER_STARTS");}
        }
        Check(methods.Count==2,"RANDOM_METHODS_BOTH_USED");
        foreach(int starter in new[]{0,1}) {
            var g=new ClientGame(rules);g.Start(1,starter);var hands=Hands(g);Chain(g).Add(new DominoTile(9,9));
            hands[0].Clear();hands[1].Clear();hands[0].Add(new DominoTile(1,1));hands[1].Add(new DominoTile(1,1));
            Check(g.TryPass(starter)&&!g.Finished&&g.TryPass(1-starter)&&g.Finished&&g.Winner==starter,"TWO_PASSES_TIE_STARTER");
        }
        foreach(var last in new[]{new DominoTile(6,6),new DominoTile(6,3)}) {
            var g=new ClientGame(rules);g.Start(1,0);var h=Hands(g);Chain(g).Add(new DominoTile(6,6));h[0].Clear();h[0].Add(last);h[1].Clear();h[1].Add(new DominoTile(9,9));h[1].Add(new DominoTile(8,6));
            Check(g.TryPlay(0,last)&&g.Result.FinishType==RoundFinishType.CAPICUA&&g.Result.Award==74,"SAME_END_VALUES_CAPICUA");
        }
        {
            var g=new ClientGame(rules);g.Start(1,0);var h=Hands(g);Chain(g).Add(new DominoTile(6,6));
            h[0].Clear();h[0].Add(new DominoTile(1,1));h[1].Clear();h[1].Add(new DominoTile(6,5));h[1].Add(new DominoTile(2,2));
            Check(g.TryPass(0)&&g.TryPlay(1,new DominoTile(6,5)),"PLAY_BETWEEN_PASSES");
            Check(g.TryPass(0)&&!g.Finished&&g.TryPass(1)&&g.Finished,"PASS_COUNT_RESET");
        }
        foreach(int points in new[]{140,155}) {var match=new MatchState(rules);match.Apply(RoundScoring.Evaluate(rules,new[]{0,points},0,false,1));Check(match.Finished&&match.Score(0)>=150,"TARGET_GE_150");}
        await catalog.RefreshAsync(true);Check(catalog.Source==GameCatalogSource.Remote,"REMOTE_DUEL");
        var restored=new GameCatalogService(api,cache,v2);Check(restored.Source==GameCatalogSource.Cache&&restored.ResolveMatch("DUEL_1V1").Configuration.PlayerCount==2,"CACHE_DUEL");
        api.Json=v1;await catalog.RefreshAsync(true);Check(catalog.Current.CatalogVersion==1&&catalog.Current.Modes.Count==1,"ROLLBACK_V1");
        Check(frozen.Configuration.PlayerCount==2&&frozen.Configuration.TargetScore==150,"ACTIVE_DUEL_FROZEN");
        bool rejected=false;try{catalog.ResolveMatch("DUEL_1V1");}catch(ArgumentException){rejected=true;}Check(rejected,"NO_NEW_DUEL_ON_V1");
        api.Json=v2;await catalog.RefreshAsync(true);Check(catalog.ResolveMatch("DUEL_1V1").CatalogVersion==2,"RESTORE_V2");
        Console.WriteLine("M3_UNITY_TESTS=PASS CHECKS="+checks+"; DUEL_ROUNDS=200; BOT_ACTION_COUNT=0; ONLINE_TIMER_ACTIVE=NO");
    }
    static List<DominoTile>[] Hands(ClientGame g)=>(List<DominoTile>[])typeof(ClientGame).GetField("hands",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(g);
    static List<DominoTile> Chain(ClientGame g)=>(List<DominoTile>)typeof(ClientGame).GetField("chain",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(g);
}
