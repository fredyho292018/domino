using System;
using Domino.Configuration;

namespace Domino.Game
{
    public sealed class RoundResult
    {
        public int WinnerPlayer { get; }
        public int WinnerSide { get; }
        public bool Tie => WinnerSide < 0;
        public bool Blocked { get; }
        public int BasePoints { get; }
        public int Bonus { get; }
        public int Multiplier { get; }
        public int Award => checked((BasePoints + Bonus) * Multiplier);
        public int[] HandPoints => (int[])points.Clone();
        readonly int[] points;
        public RoundResult(int player, int side, bool blocked, int basePoints, int bonus, int multiplier, int[] handPoints)
        { WinnerPlayer = player; WinnerSide = side; Blocked = blocked; BasePoints = basePoints; Bonus = bonus; Multiplier = multiplier; points = (int[])handPoints.Clone(); }
    }

    public static class RoundScoring
    {
        public static RoundResult Evaluate(GameRules rules, int[] points, int finisher, bool blocked, int multiplier)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (points == null || points.Length != rules.Configuration.PlayerCount || Array.Exists(points, p => p < 0) || multiplier < 1)
                throw new ArgumentException("One nonnegative total per configured player and a positive multiplier are required.");
            int winner = finisher;
            if (blocked)
            {
                int best = int.MaxValue, side = -1;
                bool tie = false;
                for (int p = 0; p < points.Length; p++)
                {
                    int value = points[p];
                    if (rules.Teams && rules.BlockedWinner == BlockedWinnerRule.LowestTeamTotal)
                    {
                        value = 0;
                        foreach (int member in rules.GetTeamMembers(rules.Side(p))) value += points[member];
                    }
                    if (value < best) { best = value; winner = p; side = rules.Side(p); tie = false; }
                    else if (value == best && rules.Side(p) != side) tie = true;
                }
                if (tie) return new RoundResult(-1, -1, true, 0, 0, multiplier, points);
            }
            if (winner < 0 || winner >= points.Length) throw new ArgumentOutOfRangeException(nameof(finisher));
            int total = 0;
            var scoring = blocked ? rules.Configuration.BlockedScoring : rules.Configuration.FinishScoring;
            for (int p = 0; p < points.Length; p++)
                if (scoring.Source == PointsSource.AllOtherPlayers ? p != winner : rules.Side(p) != rules.Side(winner)) total += points[p];
            return new RoundResult(winner, rules.Side(winner), blocked, total, scoring.Bonus, multiplier, points);
        }
    }

    public sealed class MatchState
    {
        readonly GameRules rules;
        readonly int[] scores;
        public GameConfigurationSnapshot Configuration => rules.Configuration;
        bool settled;
        public int RoundNumber { get; private set; } = 1;
        public int Multiplier { get; private set; } = 1;
        public bool Finished { get; private set; }
        public int WinnerSide { get; private set; } = -1;
        public int Score(int side) => scores[side];
        public MatchState(GameRules rules)
        { this.rules = rules ?? throw new ArgumentNullException(nameof(rules)); scores = new int[Configuration.TeamCount]; }
        public void Reset() { Array.Clear(scores, 0, scores.Length); RoundNumber = 1; Multiplier = 1; Finished = false; WinnerSide = -1; settled = false; }
        public void Apply(RoundResult result)
        {
            if (settled || Finished) throw new InvalidOperationException("Round already scored.");
            if (result.Tie) Multiplier = rules.CompoundTies ? checked(Multiplier * Configuration.Tie.NextRoundMultiplier) : Configuration.Tie.NextRoundMultiplier;
            else
            {
                scores[result.WinnerSide] = checked(scores[result.WinnerSide] + result.Award);
                Multiplier = 1;
                if (scores[result.WinnerSide] >= rules.TargetScore) { Finished = true; WinnerSide = result.WinnerSide; }
            }
            settled = true;
        }
        public bool NextRound()
        {
            if (!settled || Finished) return false;
            RoundNumber++; settled = false; return true;
        }
    }
}
