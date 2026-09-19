using System;
using System.Collections.Generic;
using Domino.Configuration;

namespace Domino.Catalog
{
    public enum RuleCapability { DUEL_TOPOLOGY, RANDOM_START_METHOD, HIGH_TILE_SELECTION, EVEN_ODD_GUESS, PREVIOUS_ROUND_WINNER_START, BLOCKED_TIE_STARTER_WINS, CAPICUA_SCORING_V1 }
    public sealed class DisconnectPolicySnapshot
    {
        public int ReconnectWindowSeconds { get; }
        public bool TurnClockContinuesWhileDisconnected { get; }
        public bool AutoPlayWhileDisconnected { get; }
        internal DisconnectPolicySnapshot(int window,bool clock,bool autoplay) {ReconnectWindowSeconds=window;TurnClockContinuesWhileDisconnected=clock;AutoPlayWhileDisconnected=autoplay;}
    }
    public sealed class RuleSetSnapshot
    {
        public string Id => Configuration.Id;
        public int Version => Configuration.Version;
        public int RuleSchemaVersion => Configuration.SchemaVersion;
        public string ContentHash { get; }
        public IReadOnlyList<RuleCapability> RequiredCapabilities { get; }
        public GameConfigurationSnapshot Configuration { get; }
        internal RuleSetSnapshot(GameConfigurationSnapshot configuration, string hash, RuleCapability[] capabilities=null) { Configuration = configuration; ContentHash = hash; RequiredCapabilities=Array.AsReadOnly((RuleCapability[])(capabilities??Array.Empty<RuleCapability>()).Clone()); }
    }
    public sealed class GameModeSnapshot
    {
        public string Id { get; }
        public string Key { get; }
        public string NameKey { get; }
        public string DescriptionKey { get; }
        public string IconKey { get; }
        public bool Active { get; }
        public int SortOrder { get; }
        public int TopologyVersion { get; }
        public int MinHumans { get; }
        public int MaxHumans { get; }
        public bool BotsAllowed { get; }
        public RuleSetSnapshot RuleSet { get; }
        public int PlayerCount => RuleSet.Configuration.PlayerCount;
        public TeamMode TeamMode => RuleSet.Configuration.TeamMode;
        public IReadOnlyList<System.Collections.ObjectModel.ReadOnlyCollection<int>> SeatTeams => RuleSet.Configuration.TeamAssignments;
        public int? TeamSize => SeatTeams.Count==0?(int?)null:SeatTeams[0].Count;
        public DisconnectPolicySnapshot OnlinePolicy { get; }
        public IReadOnlyList<string> ExecutionModesSupported { get; }
        public string ExecutionMode => ExecutionModesSupported[0];
        public string Availability => "ALL";
        internal GameModeSnapshot(string id,string key,string name,string description,string icon,bool active,int sort,int topology,
            int min,int max,bool bots,RuleSetSnapshot rules,DisconnectPolicySnapshot onlinePolicy=null,string[] executions=null)
        { Id=id; Key=key; NameKey=name; DescriptionKey=description; IconKey=icon; Active=active; SortOrder=sort;
            TopologyVersion=topology; MinHumans=min; MaxHumans=max; BotsAllowed=bots; RuleSet=rules;OnlinePolicy=onlinePolicy;ExecutionModesSupported=Array.AsReadOnly((string[])(executions??new[]{"LOCAL"}).Clone()); }
    }
    public sealed class GameCatalogSnapshot
    {
        public int CatalogSchemaVersion => 1;
        public int CatalogVersion { get; }
        public IReadOnlyList<GameModeSnapshot> Modes { get; }
        internal GameCatalogSnapshot(int version,List<GameModeSnapshot> modes)
        { CatalogVersion=version; Modes=modes.AsReadOnly(); }
    }
}
