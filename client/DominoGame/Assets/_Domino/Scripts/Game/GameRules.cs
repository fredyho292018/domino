using System;
using System.Collections.Generic;
using Domino.Configuration;

namespace Domino.Game
{
    /// <summary>Local house rules, independent of presentation and transport.</summary>
    public sealed class GameRules
    {
        public GameConfigurationSnapshot Configuration { get; }
        public bool Teams => Configuration.TeamMode == TeamMode.FixedTeams;
        public int TargetScore => Configuration.TargetScore;
        public int FinishBonus => Configuration.FinishScoring.Bonus;
        public BlockedWinnerRule BlockedWinner => Configuration.Blocked.Winner;
        public bool CompoundTies => Configuration.Tie.Compound;
        public GameRules(GameConfigurationSnapshot configuration)
            => Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        public int GetTeamForPlayer(int player) => Configuration.GetTeamForPlayer(player);
        public IReadOnlyList<int> GetTeamMembers(int team) => Configuration.GetTeamMembers(team);
        public int Side(int player) => Configuration.GetScoreOwner(player);
    }
}
