using System;

namespace Domino.Configuration
{
    // Public fields are the portable JSON contract. No gameplay or Unity dependencies.
    // Sentinel values make omitted numeric fields fail validation when overwriting a DTO.
    [Serializable] public sealed class GameConfigurationDto
    {
        public string id;
        public int version = -1;
        public int schemaVersion = -1;
        public string rulesetVersion;
        public int playerCount = -1;
        public string teamMode;
        public TeamAssignmentDto[] teamAssignments;
        public int maxPip = -1;
        public int tilesPerPlayer = -1;
        public DealPolicyDto deal = new();
        public string drawPolicy;
        public int[] turnOrder;
        public StartingPolicyDto firstRoundStarting = new();
        public StartingPolicyDto followingRoundStarting = new();
        public string openingTilePolicy;
        public string passPolicy;
        public BlockedPolicyDto blocked = new();
        public ScoringPolicyDto finishScoring = new();
        public ScoringPolicyDto blockedScoring = new();
        public TiePolicyDto tie = new();
        public int targetScore = -1;
    }
    [Serializable] public sealed class TeamAssignmentDto { public int[] members; }
    [Serializable] public sealed class DealPolicyDto { public string method; public int[] seatOrder; public string undealtTiles; }
    [Serializable] public sealed class StartingPolicyDto { public string mode; public int seat = -1; }
    [Serializable] public sealed class BlockedPolicyDto
    {
        public string detection;
        public string winner;
        public string opposingTeamsMinimumTie;
        public string sameTeamMinimumTie;
        public string winningRepresentative;
    }
    [Serializable] public sealed class ScoringPolicyDto { public string source; public int bonus = -1; }
    [Serializable] public sealed class TiePolicyDto
    {
        public int award = -1;
        public int nextRoundMultiplier = -1;
        public string repeatedTie;
        public string appliesTo;
        public string afterAward;
    }
}
