namespace Domino.Core
{
    public enum ChainEnd { Auto, Left, Right }
    // Local events have no dependency on Unity or a future transport.
    public enum GameEventType
    {
        GAME_STARTED, TILE_PLAYED, TURN_CHANGED, PLAYER_PASSED, ROUND_FINISHED, GAME_FINISHED
    }

    public readonly struct GameEvent
    {
        public GameEventType Type { get; }
        public int Player { get; }
        public DominoTile Tile { get; }
        public int ChainIndex { get; }
        public GameEvent(GameEventType type, int player = 0, DominoTile tile = default, int chainIndex = -1)
        { Type = type; Player = player; Tile = tile; ChainIndex = chainIndex; }
    }
}
