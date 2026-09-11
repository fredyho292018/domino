using System;
using System.Collections.Generic;
using Domino.Core;
using Domino.Configuration;

namespace Domino.Game
{
    /// <summary>Double Nine, ten per player; fifteen reserved tiles are not drawn.</summary>
    public sealed class ClientGame
    {
        readonly List<DominoTile>[] hands;
        readonly List<DominoTile> chain = new(), reserve = new();
        int consecutivePasses;
        public event Action<GameEvent> Changed;
        public int CurrentPlayer { get; private set; }
        public bool Finished { get; private set; }
        public int Winner { get; private set; } = -1;
        public bool Blocked { get; private set; }
        public GameRules Rules { get; }
        public MatchState Match { get; }
        public RoundResult Result { get; private set; }
        public GameConfigurationSnapshot Configuration => Rules.Configuration;
        public ClientGame(GameRules rules)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            hands = new List<DominoTile>[Configuration.PlayerCount];
            for (int p = 0; p < hands.Length; p++) hands[p] = new List<DominoTile>();
            Match = new MatchState(Rules);
        }
        public int LeftEnd => chain.Count == 0 ? -1 : chain[0].SideA;
        public int RightEnd => chain.Count == 0 ? -1 : chain[chain.Count - 1].SideB;
        public IReadOnlyList<DominoTile> Hand(int player) => hands[player].AsReadOnly();
        public IReadOnlyList<DominoTile> Chain => chain.AsReadOnly();
        public IReadOnlyList<DominoTile> Reserve => reserve.AsReadOnly();
        public void Start(int seed)
        { Match.Reset(); Deal(seed); }
        public bool StartNextRound(int seed)
        {
            if (!Finished || !Match.NextRound()) return false;
            Deal(seed); return true;
        }
        void Deal(int seed)
        {
            var tiles = DominoTile.CreateSet(Configuration.MaxPip);
            var random = new Random(seed);
            for (int i = tiles.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); (tiles[i], tiles[j]) = (tiles[j], tiles[i]); }
            foreach (var hand in hands) hand.Clear();
            chain.Clear(); reserve.Clear();
            int dealt = Configuration.TilesPerPlayer * Configuration.PlayerCount;
            for (int i = 0; i < dealt; i++) hands[Configuration.Deal.SeatOrder[i % hands.Length]].Add(tiles[i]);
            reserve.AddRange(tiles.GetRange(dealt, tiles.Count - dealt));
            CurrentPlayer = Match.RoundNumber == 1 ? Configuration.FirstRoundStarting.Seat : Configuration.FollowingRoundStarting.Seat;
            Finished = false; Blocked = false; Winner = -1; consecutivePasses = 0; Result = null;
            Changed?.Invoke(new GameEvent(GameEventType.GAME_STARTED));
            Changed?.Invoke(new GameEvent(GameEventType.TURN_CHANGED, CurrentPlayer));
        }
        public bool CanPlay(DominoTile tile, ChainEnd end = ChainEnd.Auto)
        {
            if (Finished || tile.SideA > Configuration.MaxPip || tile.SideB > Configuration.MaxPip) return false;
            if (chain.Count == 0) return Configuration.OpeningTile == OpeningTilePolicy.Any;
            bool left = tile.SideA == LeftEnd || tile.SideB == LeftEnd;
            bool right = tile.SideA == RightEnd || tile.SideB == RightEnd;
            return end == ChainEnd.Left ? left : end == ChainEnd.Right ? right : left || right;
        }
        public bool HasLegalMove(int player) => hands[player].Exists(t => CanPlay(t));
        public bool TryPlay(int player, DominoTile tile, ChainEnd end = ChainEnd.Auto)
        {
            if (Finished || player != CurrentPlayer || !hands[player].Contains(tile) || !CanPlay(tile, end)) return false;
            if (end == ChainEnd.Auto) end = CanPlay(tile, ChainEnd.Right) ? ChainEnd.Right : ChainEnd.Left;
            DominoTile oriented = tile;
            if (chain.Count > 0 && ((end == ChainEnd.Left && tile.SideB != LeftEnd) || (end == ChainEnd.Right && tile.SideA != RightEnd)))
                oriented = new DominoTile(tile.SideB, tile.SideA);
            int index = end == ChainEnd.Left ? 0 : chain.Count;
            hands[player].Remove(tile); chain.Insert(index, oriented); consecutivePasses = 0;
            Changed?.Invoke(new GameEvent(GameEventType.TILE_PLAYED, player, oriented, index));
            if (hands[player].Count == 0) Finish(player, false); else AdvanceTurn();
            return true;
        }
        public bool TryPass(int player)
        {
            if (Finished || player != CurrentPlayer || HasLegalMove(player)) return false;
            consecutivePasses++;
            Changed?.Invoke(new GameEvent(GameEventType.PLAYER_PASSED, player));
            if (consecutivePasses == Configuration.PlayerCount) Finish(-1, true); else AdvanceTurn();
            return true;
        }
        void Finish(int player, bool blocked)
        {
            var points = new int[Configuration.PlayerCount];
            for (int p = 0; p < hands.Length; p++) foreach (var tile in hands[p]) points[p] += tile.SideA + tile.SideB;
            Result = RoundScoring.Evaluate(Rules, points, player, blocked, Match.Multiplier);
            Match.Apply(Result);
            Finished = true; Winner = Result.WinnerPlayer; Blocked = blocked;
            Changed?.Invoke(new GameEvent(GameEventType.ROUND_FINISHED, Winner));
            if (Match.Finished) Changed?.Invoke(new GameEvent(GameEventType.GAME_FINISHED, Winner));
        }
        void AdvanceTurn()
        {
            CurrentPlayer = Configuration.GetNextPlayer(CurrentPlayer);
            Changed?.Invoke(new GameEvent(GameEventType.TURN_CHANGED, CurrentPlayer));
        }
    }
}
