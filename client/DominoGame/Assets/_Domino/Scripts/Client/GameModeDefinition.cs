using System;
using System.Collections.Generic;
using Domino.Configuration;
using Domino.Catalog;

namespace Domino.Client
{
    public sealed class GameModeDefinition
    {
        // Compatibility selection token for older validation callers. Never used to build a session.
        public static GameModeDefinition TeamMatch { get; } = new GameModeDefinition();
        public string Key => snapshot?.Key ?? GameCatalogConfigurationAdapter.SupportedModeKey;
        public string Id => snapshot?.Id ?? "offline-team-match";
        public string DisplayNameKey => snapshot?.NameKey ?? "mode.team_match.title";
        public string SubtitleKey => snapshot?.DescriptionKey ?? "mode.team_match.subtitle";
        public string ConfigurationId => snapshot?.RuleSet.Id ?? "double-nine-partners";
        public int LocalPlayerCount => 1;
        public int BotCount => (snapshot?.PlayerCount ?? 4)-LocalPlayerCount;
        public int LocalPlayerSeat => 0;
        public IReadOnlyList<int> TeamA => snapshot?.SeatTeams[0] ?? throw new InvalidOperationException("Resolve catalog metadata first.");
        public IReadOnlyList<int> TeamB => snapshot?.SeatTeams[1] ?? throw new InvalidOperationException("Resolve catalog metadata first.");
        readonly GameModeSnapshot snapshot;
        private GameModeDefinition() { }
        public GameModeDefinition(GameModeSnapshot snapshot) { this.snapshot=snapshot??throw new ArgumentNullException(nameof(snapshot)); }
    }

    public enum ParticipantControl { LOCAL_HUMAN, BOT }

    public sealed class SessionSetup
    {
        public GameModeDefinition Mode { get; }
        public MatchRuleSnapshot Rules { get; }
        public GameConfigurationSnapshot Configuration { get; }
        public int LocalPlayerSeat { get; }
        public IReadOnlyList<ParticipantControl> Controls { get; }
        public bool IsBot(int seat) => Controls[seat]==ParticipantControl.BOT;
        public SessionSetup(MatchRuleSnapshot rules, int localPlayerSeat = 0)
        {
            Rules=rules??throw new ArgumentNullException(nameof(rules));
            Configuration=rules.Configuration;Mode=new GameModeDefinition(rules.Mode);LocalPlayerSeat=localPlayerSeat;
            if(localPlayerSeat<0||localPlayerSeat>=Configuration.PlayerCount)throw new ArgumentOutOfRangeException(nameof(localPlayerSeat));
            if(!rules.Mode.BotsAllowed||rules.Mode.MinHumans>1||rules.Mode.MaxHumans<1)throw new ArgumentException("Local session needs one human and enabled bots.");
            var controls=new ParticipantControl[Configuration.PlayerCount];
            for(int seat=0;seat<controls.Length;seat++)controls[seat]=ParticipantControl.BOT;
            controls[localPlayerSeat]=ParticipantControl.LOCAL_HUMAN;
            Controls=Array.AsReadOnly(controls);
        }
    }
}
