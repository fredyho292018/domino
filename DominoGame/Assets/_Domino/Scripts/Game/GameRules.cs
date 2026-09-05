using System;

namespace Domino.Game
{
    public enum BlockedWinnerRule { LowestPlayer, LowestTeamTotal }
    public enum PointsSource { OpponentsOnly, AllOtherPlayers }

    /// <summary>Local house rules, independent of presentation and transport.</summary>
    public sealed class GameRules
    {
        public bool Teams { get; }
        public int TargetScore { get; }
        public int FinishBonus { get; }
        public BlockedWinnerRule BlockedWinner { get; }
        public PointsSource Scoring { get; }
        public bool CompoundTies { get; }
        public GameRules(bool teams = true, int targetScore = 200, int finishBonus = 10,
            BlockedWinnerRule blockedWinner = BlockedWinnerRule.LowestPlayer,
            PointsSource scoring = PointsSource.AllOtherPlayers, bool compoundTies = false)
        {
            if (targetScore < 1 || finishBonus < 0) throw new ArgumentOutOfRangeException(nameof(targetScore));
            Teams = teams; TargetScore = targetScore; FinishBonus = finishBonus;
            BlockedWinner = blockedWinner; Scoring = scoring; CompoundTies = compoundTies;
        }
        public int Side(int player) => Teams ? player % 2 : player;
    }
}
