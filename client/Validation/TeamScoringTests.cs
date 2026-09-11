using System;
using System.Linq;
using Domino.Configuration;
using Domino.Game;

static class TeamScoringTests
{
    static int checks;
    static void Check(bool value, string description)
    { checks++; if (!value) throw new Exception("Team scoring: " + description); }
    public static void Run()
    {
        var rules = ConfigurationTests.Rules();
        var cases = new[] {
            (seat:0, points:new[]{0,24,18,31}, expected:55),
            (seat:2, points:new[]{12,14,0,25}, expected:39),
            (seat:1, points:new[]{17,0,29,20}, expected:46),
            (seat:3, points:new[]{11,15,22,0}, expected:33)
        };
        foreach(var item in cases)
        {
            var result = RoundScoring.Evaluate(rules,item.points,item.seat,false,1);
            Check(result.BasePoints == item.expected, "mandatory finisher seat " + item.seat);
            Check(result.WinnerSide == rules.GetTeamForPlayer(item.seat), "winner side by assignment");
            int partner = rules.GetTeamMembers(result.WinnerSide).Single(p=>p!=item.seat);
            var changed=(int[])item.points.Clone(); changed[partner]+=100;
            Check(RoundScoring.Evaluate(rules,changed,item.seat,false,1).Award == result.Award, "partner points never charged");
            var match = new MatchState(rules); match.Apply(result);
            Check(match.Score(result.WinnerSide) == item.expected+10 && match.Score(1-result.WinnerSide)==0, "correct team credited");
        }
        Check(RoundScoring.Evaluate(rules,new[]{0,24,18,31},0,false,1).Award==65, "case 5: finish bonus ten");
        Check(RoundScoring.Evaluate(rules,new[]{0,24,18,31},0,false,2).Award==130, "case 6: multiply base plus bonus");
        var blockedA=RoundScoring.Evaluate(rules,new[]{14,25,21,30},-1,true,1);
        var blockedB=RoundScoring.Evaluate(rules,new[]{17,12,26,18},-1,true,1);
        Check(blockedA.WinnerSide==0 && blockedA.BasePoints==55 && blockedA.Award==55, "case 7: blocked A");
        Check(blockedB.WinnerSide==1 && blockedB.BasePoints==43 && blockedB.Award==43, "case 8: blocked B");
        Check(RoundScoring.Evaluate(rules,new[]{5,20,100,25},-1,true,1).WinnerSide==0, "lowest individual still wins even if winning team has more pips");
        var reassigned=ConfigurationTests.Rules(d=>{d.teamAssignments[0].members=new[]{0,1};d.teamAssignments[1].members=new[]{2,3};});
        Check(RoundScoring.Evaluate(reassigned,new[]{0,100,24,31},0,false,1).BasePoints==55, "nonstandard assignments, no seat/name shortcuts");
        var separate=ConfigurationTests.Rules(d=>{d.finishScoring.bonus=11;d.blockedScoring.bonus=3;});
        Check(RoundScoring.Evaluate(separate,new[]{0,24,18,31},0,false,1).Award==66, "finish bonus independent");
        Check(RoundScoring.Evaluate(separate,new[]{14,25,21,30},-1,true,2).Award==116, "blocked bonus independent and multiplied");
        var tie=RoundScoring.Evaluate(rules,new[]{5,5,80,90},-1,true,1);
        var tiedMatch=new MatchState(rules); tiedMatch.Apply(tie); tiedMatch.NextRound();
        Check(tie.Tie && tie.Award==0 && tiedMatch.Multiplier==2, "tie does not award points");
        tiedMatch.Apply(RoundScoring.Evaluate(rules,new[]{5,5,80,90},-1,true,2)); tiedMatch.NextRound();
        Check(tiedMatch.Multiplier==2, "repeated tie policy preserved");
        tiedMatch.Apply(RoundScoring.Evaluate(rules,new[]{0,24,18,31},0,false,tiedMatch.Multiplier));
        Check(tiedMatch.Score(0)==130 && tiedMatch.Multiplier==1 && !tiedMatch.Finished, "tie multiplier awarded once then reset");
        tiedMatch.NextRound(); tiedMatch.Apply(RoundScoring.Evaluate(rules,new[]{0,30,100,40},0,false,1));
        Check(tiedMatch.Finished && tiedMatch.Score(0)==210 && !tiedMatch.NextRound(), "200 target and match termination preserved");

        // Same seeded hands and actions under old/new policy. Only payouts may differ.
        var legacy=ConfigurationTests.Rules(d=>{d.finishScoring.source="ALL_OTHER_PLAYERS";d.blockedScoring.source="ALL_OTHER_PLAYERS";});
        for(int seed=0;seed<250;seed++)
        {
            var before=new ClientGame(legacy); var after=new ClientGame(rules);
            before.Start(seed); after.Start(seed);
            Check(before.Reserve.SequenceEqual(after.Reserve), "reserve regression");
            for(int p=0;p<4;p++) Check(before.Hand(p).SequenceEqual(after.Hand(p)), "deal regression");
            int steps=0;
            while(!before.Finished)
            {
                Check(++steps<200 && !after.Finished && before.CurrentPlayer==after.CurrentPlayer, "turns and termination regression");
                int p=before.CurrentPlayer;
                var oldMoves=before.Hand(p).Where(t=>before.CanPlay(t)).ToArray();
                var newMoves=after.Hand(p).Where(t=>after.CanPlay(t)).ToArray();
                Check(oldMoves.SequenceEqual(newMoves), "legal moves regression");
                if(oldMoves.Length==0) Check(before.TryPass(p)==after.TryPass(p), "pass regression");
                else { int index=(seed+steps)%oldMoves.Length; Check(before.TryPlay(p,oldMoves[index])==after.TryPlay(p,newMoves[index]), "play regression"); }
            }
            var oldResult=before.Result; var result=after.Result;
            Check(after.Finished && oldResult.WinnerPlayer==result.WinnerPlayer && oldResult.WinnerSide==result.WinnerSide && oldResult.Blocked==result.Blocked && oldResult.Tie==result.Tie, "blocked/finish winner regression");
            Check(oldResult.HandPoints.SequenceEqual(result.HandPoints) && oldResult.Bonus==result.Bonus && oldResult.Multiplier==result.Multiplier, "remaining hands and bonus/multiplier regression");
            if(!result.Tie)
            {
                int expected=Enumerable.Range(0,4).Where(p=>rules.GetTeamForPlayer(p)!=result.WinnerSide).Sum(p=>result.HandPoints[p]);
                Check(result.BasePoints==expected, "seeded round charges opponents only");
                int partner=rules.GetTeamMembers(result.WinnerSide).Single(p=>p!=result.WinnerPlayer);
                Check(oldResult.BasePoints-result.BasePoints==result.HandPoints[partner], "only difference is exclusion of partner");
            }
        }
        Console.WriteLine($"SCORING_TESTS=PASS ({checks} checks; 8 required cases, assignment independence, ties and 250 differential rounds)");
    }
}
