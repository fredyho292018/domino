using System;
using System.Collections.Generic;
using Domino.Configuration;

namespace Domino.Client
{
    public sealed class GameModeDefinition
    {
        public static GameModeDefinition TeamMatch { get; } = new GameModeDefinition();
        public string Id => "offline-team-match";
        public string DisplayNameKey => "mode.team_match.title";
        public string SubtitleKey => "mode.team_match.subtitle";
        public string ConfigurationId => "double-nine-partners";
        public int LocalPlayerCount => 1;
        public int BotCount => 3;
        public int LocalPlayerSeat => 0;
        public IReadOnlyList<int> TeamA { get; } = Array.AsReadOnly(new[] { 0, 2 });
        public IReadOnlyList<int> TeamB { get; } = Array.AsReadOnly(new[] { 1, 3 });
        private GameModeDefinition() { }
    }

    public sealed class SessionSetup
    {
        public GameModeDefinition Mode { get; }
        public GameConfigurationSnapshot Configuration { get; }
        public int LocalPlayerSeat { get; }
        public bool IsBot(int seat) => seat != LocalPlayerSeat;
        public SessionSetup(GameModeDefinition mode, GameConfigurationSnapshot configuration, int? localPlayerSeat = null)
        {
            Mode = mode ?? throw new ArgumentNullException(nameof(mode));
            LocalPlayerSeat = localPlayerSeat ?? mode.LocalPlayerSeat;
            if (LocalPlayerSeat < 0 || LocalPlayerSeat >= 4) throw new ArgumentOutOfRangeException(nameof(localPlayerSeat));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            if (configuration.Id != mode.ConfigurationId || configuration.PlayerCount != mode.LocalPlayerCount + mode.BotCount
                || configuration.TeamMode != TeamMode.FixedTeams || configuration.TeamCount != 2)
                throw new ArgumentException("Configuration is incompatible with the selected mode.");
            foreach (var seat in mode.TeamA) if (configuration.GetTeamForPlayer(seat) != 0) throw new ArgumentException("Team A differs from session.");
            foreach (var seat in mode.TeamB) if (configuration.GetTeamForPlayer(seat) != 1) throw new ArgumentException("Team B differs from session.");
        }
    }
}
