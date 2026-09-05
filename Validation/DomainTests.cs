using System;
using System.Linq;
using System.Collections.Generic;
using Domino.Core;
using Domino.Game;
using Domino.UI;
using Domino.Configuration;

static class DomainTests
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Main()
    {
        ConfigurationTests.Run();
        string trace = RegressionTrace.Compute(() => new ClientGame(ConfigurationTests.Rules()));
        Check(trace == "EFADD088F8AB7A9E11CC109A322267C5B7800F7F85BB2D64B6F8F30F95998D52", "Exact baseline: 100 complete matches, deals, events, rounds and scores");
        Console.WriteLine("REGRESSION_TRACE=MATCH " + trace);
        ScoringTests();
        CompactLayoutTests();
        var set = DominoTile.CreateSet(9);
        Check(set.Count == 55 && set.Distinct().Count() == 55, "Unique Double Nine set");
        for (int a = 0; a <= 9; a++) for (int b = a; b <= 9; b++)
            Check(set.Contains(new DominoTile(a,b)), "All combinations through nine");
        foreach (int bad in new[] {-1,10})
        {
            bool rejected = false;
            try { _ = new DominoTile(bad,0); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid value rejected");
        }
        int leftPlays = 0, rightPlays = 0, passes = 0, blocked = 0, wins = 0, flips = 0;
        var game = new ClientGame(ConfigurationTests.Rules());
        var events = new List<GameEvent>(); game.Changed += events.Add;
        for (int seed = 0; seed < 1000; seed++)
        {
            events.Clear();
            game.Start(seed);
            Check(game.Chain.Count == 0 && game.CurrentPlayer == 0 && !game.Finished, "Restart resets state");
            Check(game.Reserve.Count == 15, "Fifteen reserved tiles");
            var total = Enumerable.Range(0,4).SelectMany(p => game.Hand(p)).Concat(game.Reserve).ToList();
            Check(total.Count == 55 && total.Distinct().Count() == 55, "Deal conserves all tiles");
            Check(Enumerable.Range(0,4).All(p => game.Hand(p).Count == 10), "Ten per player");
            var random = new Random(seed);
            int actions = 0;
            while (!game.Finished)
            {
                Check(++actions < 165, "Round terminates");
                int p = game.CurrentPlayer;
                var hand = game.Hand(p).ToArray();
                int oldCount = game.Chain.Count;
                var legal = new List<(DominoTile tile, ChainEnd end)>();
                foreach (var tile in hand)
                foreach (var end in new[] {ChainEnd.Left, ChainEnd.Right})
                {
                    int endpoint = end == ChainEnd.Left ? game.LeftEnd : game.RightEnd;
                    bool expected = oldCount == 0 || tile.SideA == endpoint || tile.SideB == endpoint;
                    Check(game.CanPlay(tile,end) == expected, "Only exposed endpoints accept matching values");
                    if (expected) legal.Add((tile,end));
                    else
                    {
                        int eventCount = events.Count;
                        Check(!game.TryPlay(p,tile,end), "Reject mismatched placement");
                        Check(game.Chain.Count == oldCount && game.Hand(p).Count == hand.Length && game.CurrentPlayer == p && events.Count == eventCount, "Invalid play changes nothing");
                    }
                }
                Check(!game.TryPlay((p+1)%4, hand[0]), "Reject out of turn");
                if (legal.Count == 0) { Check(game.TryPass(p), "Pass without legal moves"); passes++; }
                else
                {
                    Check(!game.TryPass(p), "Cannot pass with legal move");
                    var move = legal[random.Next(legal.Count)];
                    int before = events.Count;
                    Check(game.TryPlay(p,move.tile,move.end), "Accept legal placement");
                    var e = events[before];
                    Check(e.Type == GameEventType.TILE_PLAYED && e.ChainIndex == (move.end == ChainEnd.Left ? 0 : oldCount), "Correct insertion event");
                    if (move.end == ChainEnd.Left) leftPlays++; else rightPlays++;
                    if (e.Tile.SideA != move.tile.SideA) flips++;
                    Check(!game.TryPlay(p,move.tile), "No duplicate placement");
                    for (int i = 1; i < game.Chain.Count; i++) Check(game.Chain[i-1].SideB == game.Chain[i].SideA, "Adjacent ends match across whole chain");
                }
                if (!game.Finished) Check(game.CurrentPlayer == new[] {3,0,1,2}[p], "Fredy John Maria Alex cycle");
            }
            if (game.Blocked) { blocked++; Check(Enumerable.Range(0,4).All(p => game.Hand(p).All(t => t.SideA != game.LeftEnd && t.SideB != game.LeftEnd && t.SideA != game.RightEnd && t.SideB != game.RightEnd)), "Blocked round");
                if (!game.Result.Tie) Check(game.Result.HandPoints[game.Winner] == game.Result.HandPoints.Min(), "Lowest individual wins tranca"); }
            else { wins++; Check(game.Hand(game.Winner).Count == 0, "Winner empties hand"); }
            Check(events.Any(e => e.Type == GameEventType.ROUND_FINISHED), "Round finish event");
            Check(events.Any(e => e.Type == GameEventType.GAME_FINISHED) == game.Match.Finished, "Match ends only at target score");
            Check(!game.TryPass(game.CurrentPlayer), "No action after finish");
        }
        Check(leftPlays > 0 && rightPlays > 0 && flips > 0 && passes > 0 && blocked > 0 && wins > 0, "All rule branches exercised");
        for (int count = 1; count <= 40; count++)
        {
            var offset = BoardLayout.CenterOffset(count, i => i % 3 == 0);
            for (int i = 0; i < count; i++)
            {
                var pose = BoardLayout.Slot(i, i % 3 == 0);
                float w = (pose.Angle % 180 == 90 ? 46 : 96) * pose.Scale;
                float h = (pose.Angle % 180 == 90 ? 96 : 46) * pose.Scale;
                Check(Math.Abs(pose.Position.x-offset.x)+w/2 < 487 && Math.Abs(pose.Position.y-offset.y)+h/2 < 196, "Forty tiles fit on felt");
            }
        }
        for (int i = 0; i < 40; i++) for (int j = i+1; j < 40; j++) for (int mask=0;mask<4;mask++)
        {
            var a=BoardLayout.Slot(i,(mask&1)!=0); var b=BoardLayout.Slot(j,(mask&2)!=0);
            float aw=(a.Angle%180==90?46:96)*a.Scale, ah=(a.Angle%180==90?96:46)*a.Scale;
            float bw=(b.Angle%180==90?46:96)*b.Scale, bh=(b.Angle%180==90?96:46)*b.Scale;
            Check(Math.Abs(a.Position.x-b.Position.x)>=(aw+bw)/2 || Math.Abs(a.Position.y-b.Position.y)>=(ah+bh)/2,"No overlap with doubles or bends");
        }
        Console.WriteLine($"SUCCESS: {checks} checks, 1000 games, {leftPlays} left / {rightPlays} right, {flips} flips, {passes} passes, {wins} wins, {blocked} blocked rounds. Geometry up to 40 tiles verified; visual tests pending.");
    }
    static void CompactLayoutTests()
    {
        for (int seed = 0; seed < 80; seed++)
        {
            var random = new Random(seed);
            var doubles = Enumerable.Range(0,40).Select(i => seed == 0 || (seed != 1 && random.Next(4) == 0)).ToArray();
            for (int count = 1; count <= 40; count++)
            {
                var poses = BoardLayout.Arrange(count, i => doubles[i]);
                for (int i = 0; i < count; i++)
                {
                    var a = poses[i]; var ah = BoardLayout.HalfSize(a);
                    Check(Math.Abs(a.Position.x)+ah.x <= 461 && Math.Abs(a.Position.y)+ah.y <= 181,"Compact chain fits at every stage");
                    if (!doubles[i] && a.Angle == 270)
                    {
                        Check(i > 0 && !doubles[i-1] && poses[i-1].Angle % 180 == 0, "Corner enters from a horizontal normal tile");
                        Check(Math.Abs(poses[i-1].Position.y-a.Position.y-ah.y)<.002, "Entry aligns with upper corner end");
                        if (i+1 < count)
                        {
                            Check(!doubles[i+1] && poses[i+1].Angle % 180 == 0, "Corner exits to a horizontal normal tile");
                            Check(Math.Abs(a.Position.y-poses[i+1].Position.y-ah.y)<.002, "Exit aligns with lower corner end");
                        }
                    }
                    if (i > 0)
                    {
                        var b = poses[i-1]; var bh = BoardLayout.HalfSize(b);
                        float fit = a.Scale / (doubles[i] ? .684f : .76f);
                        Check(Math.Abs(Math.Abs(a.Position.x-b.Position.x)-ah.x-bh.x-BoardLayout.TileGap*fit)<.002,"Constant small gap including doubles and bends");
                    }
                    for (int j = i+1; j < count; j++)
                    {
                        var b = poses[j]; var bh = BoardLayout.HalfSize(b);
                        Check(Math.Abs(a.Position.x-b.Position.x)+.001 >= ah.x+bh.x || Math.Abs(a.Position.y-b.Position.y)+.001 >= ah.y+bh.y,"Compact tiles do not overlap");
                    }
                }
            }
        }
    }
    static void ScoringTests()
    {
        var rules = ConfigurationTests.Rules(d=> { d.targetScore=500; d.tie.repeatedTie="MULTIPLY"; });
        Check(rules.Side(0) == rules.Side(2) && rules.Side(1) == rules.Side(3), "Fredy Maria vs Alex John");
        var finish = RoundScoring.Evaluate(rules, new[] {0,20,30,40}, 0, false, 1);
        Check(finish.BasePoints == 90 && finish.Bonus == 10 && finish.Award == 100, "All other hands plus ten for going out");
        var blocked = RoundScoring.Evaluate(rules, new[] {5,20,100,25}, -1, true, 1);
        Check(blocked.WinnerPlayer == 0 && blocked.WinnerSide == 0 && blocked.Bonus == 0, "Individual minimum wins regardless of teammate");
        var tie = RoundScoring.Evaluate(rules, new[] {5,5,80,90}, -1, true, 1);
        Check(tie.Tie && tie.Award == 0, "Cross-team minimum tie");
        Check(!RoundScoring.Evaluate(rules, new[] {5,20,5,25}, -1, true, 1).Tie, "Same-team minimum not opposing tie");
        var match = new MatchState(rules);
        match.Apply(tie); Check(match.Multiplier == 2 && match.Score(0) == 0, "Tie doubles next round without scoring");
        Check(match.NextRound() && match.RoundNumber == 2, "Next round preserves match");
        match.Apply(RoundScoring.Evaluate(rules, new[] {5,5,80,90}, -1, true, 2));
        Check(match.Multiplier == 4, "Repeated tie compounds");
        match.NextRound();
        match.Apply(RoundScoring.Evaluate(rules, new[] {0,20,30,40}, 0, false, 4));
        Check(match.Score(0) == 400 && match.Multiplier == 1 && !match.Finished, "Multiplier includes bonus then resets");
        bool rejected = false; try { match.Apply(finish); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && match.Score(0) == 400, "Cannot score twice");
        match.NextRound(); match.Apply(finish);
        Check(match.Finished && match.WinnerSide == 0 && match.Score(0) == 500 && !match.NextRound(), "First side to 500 ends match");
        match.Reset(); Check(match.Score(0) == 0 && match.RoundNumber == 1 && match.Multiplier == 1 && !match.Finished, "New match resets totals");
        var solo = ConfigurationTests.Rules(d=> {d.teamMode="INDIVIDUAL";d.teamAssignments=Enumerable.Range(0,4).Select(p=>new TeamAssignmentDto{members=new[]{p}}).ToArray();});
        Check(RoundScoring.Evaluate(solo,new[]{20,25,2,40},-1,true,1).WinnerSide == 2, "Individual mode");
        var alternative = ConfigurationTests.Rules(d=>d.finishScoring.source="OPPONENTS_ONLY");
        Check(RoundScoring.Evaluate(alternative,new[]{0,20,30,40},0,false,1).Award == 70,"Configurable opponent-only score");
        var standard = ConfigurationTests.Rules();
        Check(standard.TargetScore == 200, "Default match target is 200");
        var to200 = new MatchState(standard);
        to200.Apply(RoundScoring.Evaluate(standard,new[]{0,20,30,40},0,false,1));
        Check(!to200.Finished && to200.NextRound(), "Continue below 200");
        to200.Apply(RoundScoring.Evaluate(standard,new[]{0,20,30,40},0,false,1));
        Check(to200.Finished && to200.Score(0) == 200, "Default match ends at 200");
        var doubled = new MatchState(standard);
        doubled.Apply(RoundScoring.Evaluate(standard,new[]{5,5,80,90},-1,true,1));
        doubled.NextRound();
        var thirty = RoundScoring.Evaluate(standard,new[]{0,5,7,8},0,false,doubled.Multiplier);
        Check(thirty.Award == 60, "User example: thirty becomes sixty after tied tranca");
        doubled.Apply(thirty);
        Check(doubled.Score(0) == 60 && doubled.Multiplier == 1,"Double award paid once and cleared");
    }
}
