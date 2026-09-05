using System;
using Domino.Core;

namespace Domino.Configuration
{
    public static class GameConfigurationValidator
    {
        public static GameConfigurationSnapshot Validate(GameConfigurationDto dto)
        {
            Require(dto != null, "configuration", "is required");
            Require(!string.IsNullOrWhiteSpace(dto.id), "id", "must not be empty");
            Require(dto.version > 0, "version", "must be positive");
            Require(dto.schemaVersion == 1, "schemaVersion", "only 1 is supported");
            Policy(dto.rulesetVersion, "rulesetVersion", "1.0");
            Require(dto.playerCount == 4, "playerCount", "this client supports exactly 4 seats");
            Require(dto.maxPip >= 1 && dto.maxPip <= DominoTile.SupportedMaxPip, "maxPip", "supported range is 1..9");
            int total = DominoTile.TotalTilesFor(dto.maxPip);
            Require(dto.tilesPerPlayer > 0, "tilesPerPlayer", "must be positive");
            Require((long)dto.playerCount * dto.tilesPerPlayer <= total, "tilesPerPlayer", "deal exceeds the tile set");
            Require(dto.tilesPerPlayer <= 10, "tilesPerPlayer", "current hand layout supports at most 10");
            Require(dto.targetScore > 0, "targetScore", "must be positive");
            Policy(dto.teamMode, "teamMode", "FIXED_TEAMS", "INDIVIDUAL");
            Require(dto.teamAssignments != null, "teamAssignments", "is required");
            bool fixedTeams = dto.teamMode == "FIXED_TEAMS";
            Require(dto.teamAssignments.Length == (fixedTeams ? 2 : dto.playerCount), "teamAssignments", "unsupported team count");
            var seen = new bool[dto.playerCount];
            for (int team = 0; team < dto.teamAssignments.Length; team++)
            {
                var members = dto.teamAssignments[team]?.members;
                Require(members != null && members.Length == (fixedTeams ? 2 : 1), "teamAssignments", "unsupported team size");
                foreach (int seat in members)
                {
                    Require(seat >= 0 && seat < dto.playerCount, "teamAssignments", "seat out of range");
                    Require(!seen[seat], "teamAssignments", "duplicate seat");
                    seen[seat] = true;
                }
                // Existing individual scoreboard indexes scores by seat.
                Require(fixedTeams || members[0] == team, "teamAssignments", "individual teams must use seat order");
            }
            Require(Array.TrueForAll(seen, x => x), "teamAssignments", "missing seat");
            Order(dto.turnOrder, dto.playerCount, "turnOrder");
            Require(dto.deal != null, "deal", "is required");
            Policy(dto.deal.method, "deal.method", "ROUND_ROBIN");
            Policy(dto.deal.undealtTiles, "deal.undealtTiles", "RESERVE");
            Order(dto.deal.seatOrder, dto.playerCount, "deal.seatOrder");
            Policy(dto.drawPolicy, "drawPolicy", "NONE");
            Start(dto.firstRoundStarting, dto.playerCount, "firstRoundStarting");
            Start(dto.followingRoundStarting, dto.playerCount, "followingRoundStarting");
            Policy(dto.openingTilePolicy, "openingTilePolicy", "ANY");
            Policy(dto.passPolicy, "passPolicy", "NO_LEGAL_MOVE");
            Require(dto.blocked != null, "blocked", "is required");
            Policy(dto.blocked.detection, "blocked.detection", "ALL_PLAYERS_PASS_CONSECUTIVELY");
            Policy(dto.blocked.winner, "blocked.winner", "LOWEST_INDIVIDUAL_PIPS", "LOWEST_TEAM_TOTAL");
            Policy(dto.blocked.opposingTeamsMinimumTie, "blocked.opposingTeamsMinimumTie", "ROUND_TIE");
            Policy(dto.blocked.sameTeamMinimumTie, "blocked.sameTeamMinimumTie", "TEAM_WINS");
            Policy(dto.blocked.winningRepresentative, "blocked.winningRepresentative", "FIRST_SEAT_IN_WINNING_MINIMUM");
            Scoring(dto.finishScoring, "finishScoring", total, dto.maxPip);
            Scoring(dto.blockedScoring, "blockedScoring", total, dto.maxPip);
            Require(dto.tie != null, "tie", "is required");
            Require(dto.tie.award == 0, "tie.award", "only zero is supported");
            Require(dto.tie.nextRoundMultiplier == 2, "tie.nextRoundMultiplier", "only 2 is supported");
            Policy(dto.tie.repeatedTie, "tie.repeatedTie", "KEEP_MULTIPLIER", "MULTIPLY");
            Policy(dto.tie.appliesTo, "tie.appliesTo", "TOTAL_INCLUDING_BONUS");
            Policy(dto.tie.afterAward, "tie.afterAward", "RESET_TO_ONE");
            return new GameConfigurationSnapshot(dto, total);
        }
        static void Order(int[] order, int count, string field)
        {
            Require(order != null && order.Length == count, field, "must contain each seat exactly once");
            var seen = new bool[count];
            foreach (int seat in order)
            {
                Require(seat >= 0 && seat < count, field, "seat out of range");
                Require(!seen[seat], field, "duplicate seat"); seen[seat] = true;
            }
        }
        static void Start(StartingPolicyDto policy, int count, string field)
        {
            Require(policy != null, field, "is required");
            Policy(policy.mode, field + ".mode", "FIXED_SEAT");
            Require(policy.seat >= 0 && policy.seat < count, field + ".seat", "seat out of range");
        }
        static void Scoring(ScoringPolicyDto policy, string field, int total, int maxPip)
        {
            Require(policy != null, field, "is required");
            Policy(policy.source, field + ".source", "ALL_OTHER_PLAYERS", "OPPONENTS_ONLY");
            Require(policy.bonus >= 0 && (long)policy.bonus + (long)total * maxPip <= int.MaxValue / 2,
                field + ".bonus", "must be nonnegative and fit a doubled round score");
        }
        static void Policy(string value, string field, params string[] supported)
            => Require(Array.IndexOf(supported, value) >= 0, field, "unsupported or missing policy: " + (value ?? "null"));
        static void Require(bool valid, string field, string message)
        { if (!valid) throw new ArgumentException("Configuration " + field + ": " + message); }
    }
}
