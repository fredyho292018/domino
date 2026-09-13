using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Domino.Configuration
{
    public enum TeamMode { FixedTeams, Individual }
    public enum StartingPolicy { FixedSeat, RandomStartMethod, PreviousRoundWinner }
    public enum ScoreOwnerKind { TEAM, PLAYER }
    public enum StarterMethod { HIGH_TILE_SELECTION, EVEN_ODD_GUESS }
    public enum AutoPlayPolicy { FIRST_VALID_MOVE }
    public enum ParticipantConnectionState { CONNECTED, DISCONNECTED, RECONNECTED, ABANDONED }
    public enum OpeningTilePolicy { Any }
    public enum PointsSource { AllOtherPlayers, OpponentsOnly }
    public enum BlockedWinnerRule { LowestPlayer, LowestTeamTotal }

    public sealed class StartingPolicySnapshot
    {
        public StartingPolicy Mode { get; }
        public IReadOnlyList<StarterMethod> Methods { get; }
        public int Seat { get; }
        internal StartingPolicySnapshot(StartingPolicyDto dto) {
            Seat=dto.seat; Mode=dto.mode=="FIXED_SEAT"?StartingPolicy.FixedSeat:dto.mode=="RANDOM_START_METHOD"?StartingPolicy.RandomStartMethod:StartingPolicy.PreviousRoundWinner;
            Methods=Array.AsReadOnly(Array.ConvertAll(dto.methods??Array.Empty<string>(), x=>(StarterMethod)Enum.Parse(typeof(StarterMethod),x)));
        }
    }
    public sealed class DealPolicySnapshot
    {
        public string Method => "ROUND_ROBIN";
        public string UndealtTiles => "RESERVE";
        public IReadOnlyList<int> SeatOrder { get; }
        internal DealPolicySnapshot(int[] seats) => SeatOrder = Array.AsReadOnly((int[])seats.Clone());
    }
    public sealed class ScoringPolicySnapshot
    {
        public PointsSource Source { get; }
        public int Bonus { get; }
        internal ScoringPolicySnapshot(ScoringPolicyDto dto)
        { Source = dto.source == "ALL_OTHER_PLAYERS" ? PointsSource.AllOtherPlayers : PointsSource.OpponentsOnly; Bonus = dto.bonus; }
    }
    public sealed class TiePolicySnapshot
    {
        public int Award => 0;
        public int NextRoundMultiplier { get; }
        public bool Compound { get; }
        public string AppliesTo => "TOTAL_INCLUDING_BONUS";
        public string AfterAward => "RESET_TO_ONE";
        internal TiePolicySnapshot(TiePolicyDto dto)
        { NextRoundMultiplier = dto.nextRoundMultiplier; Compound = dto.repeatedTie == "MULTIPLY"; }
    }
    public sealed class BlockedPolicySnapshot
    {
        public string Detection => "ALL_PLAYERS_PASS_CONSECUTIVELY";
        public BlockedWinnerRule Winner { get; }
        public string OpposingTeamsMinimumTie { get; }
        public string SameTeamMinimumTie => "TEAM_WINS";
        public string WinningRepresentative => "FIRST_SEAT_IN_WINNING_MINIMUM";
        internal BlockedPolicySnapshot(BlockedPolicyDto dto)
            { Winner = dto.winner == "LOWEST_INDIVIDUAL_PIPS" ? BlockedWinnerRule.LowestPlayer : BlockedWinnerRule.LowestTeamTotal; OpposingTeamsMinimumTie=dto.opposingTeamsMinimumTie; }
    }

    public sealed class TurnPolicySnapshot
    {
        public int TimeLimitSeconds { get; }
        public bool AutoPlayOnTimeout { get; }
        public AutoPlayPolicy AutoPlayPolicy => AutoPlayPolicy.FIRST_VALID_MOVE;
        internal TurnPolicySnapshot(TurnPolicyDto dto) { TimeLimitSeconds=dto.timeLimitSeconds;AutoPlayOnTimeout=dto.autoPlayOnTimeout; }
    }
    public sealed class CapicuaPolicySnapshot
    {
        public int PipMultiplier { get; }
        public bool MultiplyBonus => false;
        internal CapicuaPolicySnapshot(CapicuaPolicyDto dto) { PipMultiplier=dto.pipMultiplier; }
    }
    /// <summary>Only the validator can build this deep copy. No DTO arrays escape.</summary>
    public sealed class GameConfigurationSnapshot
    {
        readonly ReadOnlyCollection<ReadOnlyCollection<int>> teams;
        readonly int[] teamForPlayer;
        public string Id { get; }
        public int Version { get; }
        public int SchemaVersion { get; }
        public string RulesetVersion { get; }
        public int PlayerCount { get; }
        public TeamMode TeamMode { get; }
        public int TeamCount => teams.Count;
        public ScoreOwnerKind ScoreOwner => TeamMode==TeamMode.FixedTeams?ScoreOwnerKind.TEAM:ScoreOwnerKind.PLAYER;
        public int ScoreOwnerCount => ScoreOwner==ScoreOwnerKind.TEAM?TeamCount:PlayerCount;
        public int GetScoreOwner(int player) => ScoreOwner==ScoreOwnerKind.TEAM?GetTeamForPlayer(player):player;
        public TurnPolicySnapshot TurnPolicy { get; }
        public CapicuaPolicySnapshot Capicua { get; }
        public IReadOnlyList<ReadOnlyCollection<int>> TeamAssignments => teams;
        public int MaxPip { get; }
        public int TotalTiles { get; }
        public int TilesPerPlayer { get; }
        public int ReserveCount => TotalTiles - PlayerCount * TilesPerPlayer;
        public DealPolicySnapshot Deal { get; }
        public string DrawPolicy => "NONE";
        public IReadOnlyList<int> TurnOrder { get; }
        public StartingPolicySnapshot FirstRoundStarting { get; }
        public StartingPolicySnapshot FollowingRoundStarting { get; }
        public OpeningTilePolicy OpeningTile => OpeningTilePolicy.Any;
        public string PassPolicy => "NO_LEGAL_MOVE";
        public BlockedPolicySnapshot Blocked { get; }
        public ScoringPolicySnapshot FinishScoring { get; }
        public ScoringPolicySnapshot BlockedScoring { get; }
        public TiePolicySnapshot Tie { get; }
        public int TargetScore { get; }
        internal GameConfigurationSnapshot(GameConfigurationDto dto, int totalTiles)
        {
            Id = dto.id; Version = dto.version; SchemaVersion = dto.schemaVersion; RulesetVersion = dto.rulesetVersion;
            PlayerCount = dto.playerCount; TeamMode = dto.teamMode == "FIXED_TEAMS" ? TeamMode.FixedTeams : TeamMode.Individual;
            var copies = new ReadOnlyCollection<int>[dto.teamAssignments.Length];
            teamForPlayer = new int[PlayerCount];
            for (int team = 0; team < copies.Length; team++)
            {
                copies[team] = Array.AsReadOnly((int[])dto.teamAssignments[team].members.Clone());
                foreach (int player in copies[team]) teamForPlayer[player] = team;
            }
            teams = Array.AsReadOnly(copies);
            MaxPip = dto.maxPip; TotalTiles = totalTiles; TilesPerPlayer = dto.tilesPerPlayer;
            Deal = new DealPolicySnapshot(dto.deal.seatOrder); TurnOrder = Array.AsReadOnly((int[])dto.turnOrder.Clone());
            FirstRoundStarting = new StartingPolicySnapshot(dto.firstRoundStarting);
            FollowingRoundStarting = new StartingPolicySnapshot(dto.followingRoundStarting);
            Blocked = new BlockedPolicySnapshot(dto.blocked);
            FinishScoring = new ScoringPolicySnapshot(dto.finishScoring); BlockedScoring = new ScoringPolicySnapshot(dto.blockedScoring);
            Tie = new TiePolicySnapshot(dto.tie); TargetScore = dto.targetScore;
            TurnPolicy=dto.turnPolicy==null?null:new TurnPolicySnapshot(dto.turnPolicy); Capicua=dto.capicuaPolicy==null?null:new CapicuaPolicySnapshot(dto.capicuaPolicy);
        }
        public int GetTeamForPlayer(int player)
        {
            if (player < 0 || player >= PlayerCount) throw new ArgumentOutOfRangeException(nameof(player));
            return TeamMode==TeamMode.Individual?player:teamForPlayer[player];
        }
        public IReadOnlyList<int> GetTeamMembers(int team) => teams[team];
        public int GetNextPlayer(int player)
        {
            for (int i = 0; i < TurnOrder.Count; i++)
                if (TurnOrder[i] == player) return TurnOrder[(i + 1) % TurnOrder.Count];
            throw new ArgumentOutOfRangeException(nameof(player));
        }
    }
}
