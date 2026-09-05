using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Domino.Configuration;
using Domino.Core;
using Domino.Game;

static class ConfigurationTests
{
    static int checks;
    static readonly string Json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "double-nine-partners-v1.json"));
    public static GameConfigurationDto Dto() => JsonSerializer.Deserialize<GameConfigurationDto>(Json, new JsonSerializerOptions { IncludeFields = true });
    public static GameRules Rules(Action<GameConfigurationDto> edit = null)
    {
        var dto = Dto(); edit?.Invoke(dto);
        return new GameRules(GameConfigurationValidator.Validate(dto));
    }
    static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception("Configuration: " + message); }
    static void Reject(Action<GameConfigurationDto> edit, string field)
    {
        var dto = Dto(); edit(dto);
        try { GameConfigurationValidator.Validate(dto); }
        catch (ArgumentException error) { Check(error.Message.Contains(field), "Expected error for " + field + ": " + error.Message); return; }
        throw new Exception("Accepted invalid " + field);
    }
    public static void Run()
    {
        var config = Rules().Configuration;
        Check(config.Id == "double-nine-partners" && config.Version == 1 && config.SchemaVersion == 1 && config.RulesetVersion == "1.0", "metadata");
        Check(config.PlayerCount == 4 && config.MaxPip == 9 && config.TotalTiles == 55 && config.TilesPerPlayer == 10 && config.ReserveCount == 15, "bundled deal");
        Check(config.TargetScore == 200 && config.FirstRoundStarting.Seat == 0 && config.FollowingRoundStarting.Seat == 0, "target/start");
        Check(config.TurnOrder.SequenceEqual(new[]{0,3,2,1}) && config.Deal.SeatOrder.SequenceEqual(new[]{0,1,2,3}), "orders");
        Check(config.GetTeamMembers(0).SequenceEqual(new[]{0,2}) && config.GetTeamMembers(1).SequenceEqual(new[]{1,3}), "teams");
        Check(DominoTile.CreateSet(6).Count == 28 && DominoTile.CreateSet(9).Count == 55, "Double-N generation");
        var set = DominoTile.CreateSet(9);
        Check(set.Select(t => t.Id).Distinct().Count() == 55, "unique IDs");
        Check(set.All(t => t.Equals(new DominoTile(t.SideB,t.SideA)) && t.Id == new DominoTile(t.SideB,t.SideA).Id), "orientation-independent identity");
        Reject(d=>d.id=" ", "id"); Reject(d=>d.version=0,"version"); Reject(d=>d.schemaVersion=2,"schemaVersion");
        Reject(d=>d.rulesetVersion="2.0","rulesetVersion");
        foreach(int count in new[]{0,2,3,5}) Reject(d=>d.playerCount=count,"playerCount");
        Reject(d=>d.tilesPerPlayer=0,"tilesPerPlayer"); Reject(d=>d.tilesPerPlayer=int.MaxValue,"tilesPerPlayer");
        Reject(d=>d.tilesPerPlayer=11,"tilesPerPlayer"); Reject(d=>d.targetScore=0,"targetScore");
        foreach(int pip in new[]{-1,0,10,int.MaxValue}) Reject(d=>d.maxPip=pip,"maxPip");
        Reject(d=>d.maxPip=6,"tilesPerPlayer");
        Reject(d=>d.turnOrder=new[]{0,0,2,3},"turnOrder"); Reject(d=>d.turnOrder=new[]{0,1,2},"turnOrder");
        Reject(d=>d.turnOrder=new[]{0,1,2,4},"turnOrder"); Reject(d=>d.turnOrder=null,"turnOrder");
        Reject(d=>d.deal.seatOrder=new[]{0,0,2,3},"deal.seatOrder"); Reject(d=>d.deal.seatOrder=new[]{0,1,2},"deal.seatOrder");
        Reject(d=>d.deal.seatOrder=new[]{0,1,2,-1},"deal.seatOrder");
        Reject(d=>d.teamAssignments[1].members=new[]{0,3},"teamAssignments");
        Reject(d=>d.teamAssignments[1].members=new[]{1,4},"teamAssignments");
        Reject(d=>d.teamAssignments[1].members=new[]{1},"teamAssignments");
        Reject(d=>d.teamAssignments=null,"teamAssignments"); Reject(d=>d.teamMode="UNKNOWN","teamMode");
        Reject(d=>d.deal=null,"deal"); Reject(d=>d.deal.method="RANDOM","deal.method");
        Reject(d=>d.drawPolicy="DRAW","drawPolicy"); Reject(d=>d.firstRoundStarting.mode="HIGHEST_DOUBLE","firstRoundStarting.mode");
        Reject(d=>d.followingRoundStarting.seat=4,"followingRoundStarting.seat");
        Reject(d=>d.openingTilePolicy="HIGHEST","openingTilePolicy"); Reject(d=>d.passPolicy=null,"passPolicy");
        Reject(d=>d.blocked.winner="UNKNOWN","blocked.winner");
        Reject(d=>d.finishScoring.source="UNKNOWN","finishScoring.source");
        Reject(d=>d.finishScoring.bonus=-1,"finishScoring.bonus"); Reject(d=>d.blockedScoring.bonus=int.MaxValue,"blockedScoring.bonus");
        Reject(d=>d.tie.award=1,"tie.award"); Reject(d=>d.tie.nextRoundMultiplier=3,"tie.nextRoundMultiplier");
        Reject(d=>d.tie.repeatedTie="UNKNOWN","tie.repeatedTie");
        try { GameConfigurationValidator.Validate(new GameConfigurationDto()); throw new Exception("Empty DTO accepted"); }
        catch(ArgumentException) { checks++; }

        var dto = Dto(); var snapshot = GameConfigurationValidator.Validate(dto);
        var game = new ClientGame(new GameRules(snapshot)); game.Start(17);
        dto.targetScore=999; dto.teamAssignments[0].members[0]=3; dto.turnOrder[0]=3; dto.deal.seatOrder[0]=3;
        dto.firstRoundStarting.seat=2; dto.finishScoring.bonus=99; dto.tie.nextRoundMultiplier=99;
        Check(snapshot.TargetScore == 200 && snapshot.GetTeamForPlayer(0)==0 && snapshot.TurnOrder[0]==0 && snapshot.Deal.SeatOrder[0]==0, "deep copy arrays and scalars");
        Check(snapshot.FirstRoundStarting.Seat==0 && snapshot.FinishScoring.Bonus==10 && snapshot.Tie.NextRoundMultiplier==2, "deep copy policies");
        Check(ReferenceEquals(game.Match.Configuration,snapshot),"match owns the snapshot");
        try { ((IList<int>)snapshot.GetTeamMembers(0))[0]=3; throw new Exception("Mutable team"); } catch(NotSupportedException) { checks++; }
        try { ((IList<int>)snapshot.TurnOrder)[0]=3; throw new Exception("Mutable turn order"); } catch(NotSupportedException) { checks++; }
        try { ((IList<int>)snapshot.Deal.SeatOrder)[0]=3; throw new Exception("Mutable deal order"); } catch(NotSupportedException) { checks++; }

        var shuffled=DominoTile.CreateSet(9); var rng=new Random(17);
        for(int i=shuffled.Count-1;i>0;i--) { int j=rng.Next(i+1); (shuffled[i],shuffled[j])=(shuffled[j],shuffled[i]); }
        for(int p=0;p<4;p++) Check(game.Hand(p).SequenceEqual(Enumerable.Range(0,10).Select(i=>shuffled[i*4+p])),"original deal seat " + p);
        foreach(var tile in game.Hand(0)) Check(game.CanPlay(tile),"any tile opens");

        var changed=Rules(d=> { d.targetScore=350; d.tilesPerPlayer=7; d.maxPip=6;
            d.deal.seatOrder=new[]{3,2,1,0}; d.turnOrder=new[]{2,1,0,3}; d.firstRoundStarting.seat=2; d.followingRoundStarting.seat=1;
            d.teamAssignments[0].members=new[]{0,1}; d.teamAssignments[1].members=new[]{2,3}; });
        var variant=new ClientGame(changed); variant.Start(17);
        Check(variant.CurrentPlayer==2 && variant.Reserve.Count==0 && variant.Hand(0).Count==7 && variant.Match.Configuration.TargetScore==350,"configured values reach engine");
        var six=DominoTile.CreateSet(6); rng=new Random(17);
        for(int i=six.Count-1;i>0;i--) {int j=rng.Next(i+1);(six[i],six[j])=(six[j],six[i]);}
        Check(variant.Hand(3).SequenceEqual(Enumerable.Range(0,7).Select(i=>six[i*4])),"configured deal order");
        variant.TryPlay(2,variant.Hand(2)[0]); Check(variant.CurrentPlayer==1,"configured turn order");
        while(!variant.Finished) {int p=variant.CurrentPlayer; var legal=variant.Hand(p).Where(t=>variant.CanPlay(t)).ToArray(); if(legal.Length==0)variant.TryPass(p);else variant.TryPlay(p,legal[0]);}
        Check(variant.StartNextRound(18) && variant.CurrentPlayer==1,"following round start");
        var partnerRules=Rules(d=> { d.teamAssignments[0].members=new[]{0,1}; d.teamAssignments[1].members=new[]{2,3}; d.blocked.winner="LOWEST_TEAM_TOTAL"; });
        Check(RoundScoring.Evaluate(partnerRules,new[]{5,100,40,40},-1,true,1).WinnerSide==1,"team total uses assignments");
        var independent=Rules(d=> {d.finishScoring.source="OPPONENTS_ONLY";d.finishScoring.bonus=11;});
        Check(RoundScoring.Evaluate(independent,new[]{0,20,30,40},0,false,1).Award==71,"finish policy independent");
        Check(RoundScoring.Evaluate(independent,new[]{5,20,30,40},-1,true,1).Award==90,"blocked policy unchanged");
        var standard = Rules(); var tiedMatch = new MatchState(standard);
        tiedMatch.Apply(RoundScoring.Evaluate(standard,new[]{5,5,80,90},-1,true,1));
        tiedMatch.NextRound();
        tiedMatch.Apply(RoundScoring.Evaluate(standard,new[]{5,5,80,90},-1,true,2));
        Check(tiedMatch.Multiplier==2 && tiedMatch.Score(0)==0 && tiedMatch.Score(1)==0,"repeated default tie stays at two without awarding points");
        Console.WriteLine($"CONFIGURATION_TESTS=SUCCESS ({checks} checks)");
    }
}
