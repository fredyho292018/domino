using System;
using System.Collections.Generic;
using Domino.Configuration;

namespace Domino.Catalog
{
    public sealed class RuleSetSnapshot
    {
        public string Id => Configuration.Id;
        public int Version => Configuration.Version;
        public int RuleSchemaVersion => Configuration.SchemaVersion;
        public string ContentHash { get; }
        public GameConfigurationSnapshot Configuration { get; }
        internal RuleSetSnapshot(GameConfigurationSnapshot configuration, string hash) { Configuration = configuration; ContentHash = hash; }
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
        public string ExecutionMode => "LOCAL";
        public string Availability => "ALL";
        internal GameModeSnapshot(string id,string key,string name,string description,string icon,bool active,int sort,int topology,
            int min,int max,bool bots,RuleSetSnapshot rules)
        { Id=id; Key=key; NameKey=name; DescriptionKey=description; IconKey=icon; Active=active; SortOrder=sort;
            TopologyVersion=topology; MinHumans=min; MaxHumans=max; BotsAllowed=bots; RuleSet=rules; }
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
