using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Domino.Configuration;
using Domino.Game;
using Domino.Core;
using Newtonsoft.Json;

// Runs the existing local engine, exporting independent expectations for the server engine.
static class M5ParityFixtures
{
    static void Main(string[] args)
    {
        var config=GameConfigurationValidator.Validate(JsonConvert.DeserializeObject<GameConfigurationDto>(File.ReadAllText(args[0])));
        var games=new List<object>();int rounds=0,turns=0;
        object Tiles(IEnumerable<DominoTile> ts)=>ts.Select(t=>new[]{t.SideA,t.SideB}).ToArray();
        for(int match=0;match<100;match++) {
            var game=new ClientGame(new GameRules(config));var trace=new List<object>();var randoms=new List<int>();
            for(int round=0;round<100;round++) {
                int seed=match*1000+round;var rng=new Random(seed);
                for(int i=54;i>0;i--)randoms.Add(rng.Next(i+1));
                if(round==0)game.Start(seed);else if(!game.StartNextRound(seed))throw new Exception("ROUND");
                trace.Add(new{kind="DEAL",hands=Enumerable.Range(0,4).Select(p=>Tiles(game.Hand(p))).ToArray(),reserve=Tiles(game.Reserve),turn=game.CurrentPlayer});
                while(!game.Finished) {
                    int seat=game.CurrentPlayer;var moves=game.Hand(seat).SelectMany(t=>new[]{ChainEnd.Left,ChainEnd.Right}.Where(e=>game.CanPlay(t,e)).Select(e=>(t,e))).ToArray();
                    bool pass=moves.Length==0;DominoTile tile=default;ChainEnd end=ChainEnd.Left;
                    if(pass){if(!game.TryPass(seat))throw new Exception("PASS");}
                    else {var chosen=moves[(match+turns)%moves.Length];tile=chosen.t;end=chosen.e;if(!game.TryPlay(seat,tile,end))throw new Exception("PLAY");}
                    turns++;
                    trace.Add(new{kind=pass?"PASS":"PLAY_TILE",seat,tile=new[]{tile.SideA,tile.SideB},end=end.ToString().ToUpperInvariant(),board=Tiles(game.Chain),hands=Enumerable.Range(0,4).Select(p=>Tiles(game.Hand(p))).ToArray(),turn=game.Finished?-1:game.CurrentPlayer,finished=game.Finished});
                }
                rounds++;
                trace.Add(new{kind="RESULT",winner=game.Winner,team=game.Result.WinnerSide,award=game.Result.Award,type=game.Result.FinishType.ToString(),scores=new[]{game.Match.Score(0),game.Match.Score(1)},multiplier=game.Match.Multiplier,matchFinished=game.Match.Finished});
                if(game.Match.Finished)break;
            }
            if(!game.Match.Finished)throw new Exception("TARGET");
            games.Add(new{randoms,trace});
        }
        File.WriteAllText(args[1],JsonConvert.SerializeObject(games));
        Console.WriteLine($"LOCAL_PARITY_FIXTURES=100 MATCHES / {rounds} ROUNDS / {turns} TURNS");
    }
}
