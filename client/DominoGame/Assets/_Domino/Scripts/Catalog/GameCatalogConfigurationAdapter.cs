using System;
using System.Linq;
using Domino.Configuration;

namespace Domino.Catalog
{
    public sealed class MatchRuleSnapshot
    {
        public GameCatalogSource Source { get; }
        public int CatalogVersion { get; }
        public GameModeSnapshot Mode { get; }
        public RuleSetSnapshot RuleSet => Mode.RuleSet;
        public GameConfigurationSnapshot Configuration => RuleSet.Configuration;
        public string SourceLabel => Source==GameCatalogSource.Bundled?"BUNDLED_FALLBACK":Source.ToString().ToUpperInvariant();
        internal MatchRuleSnapshot(GameCatalogSnapshot catalog,GameCatalogSource source,GameModeSnapshot mode)
        { CatalogVersion=catalog.CatalogVersion;Source=source;Mode=mode; }
        public string Diagnostic => "[GAME] match configuration resolved source="+SourceLabel+" catalog="+CatalogVersion+
            " mode="+Mode.Key+" topology="+Mode.TopologyVersion+" ruleset="+RuleSet.Id+
            " rulesetVersion="+RuleSet.Version+" contentHash="+RuleSet.ContentHash;
    }

    // The codec already combines GameMode topology and RuleSet rules into one validated,
    // deeply immutable configuration. No field is filled from legacy JSON here.
    public static class GameCatalogConfigurationAdapter
    {
        public const string SupportedModeKey="PARTNERS_2V2";
        public const string DuelModeKey="DUEL_1V1";
        public static GameModeSnapshot SupportedMode(GameCatalogSnapshot catalog,string key=SupportedModeKey)
        {
            if(catalog==null)throw new ArgumentNullException(nameof(catalog));
            var mode=catalog.Modes.SingleOrDefault(m=>m.Key==key&&m.Active);
            if(mode==null)throw new ArgumentException("SUPPORTED_LOCAL_MODE_UNAVAILABLE");
            if(key==DuelModeKey) {
                if(mode.PlayerCount!=2||mode.TeamMode!=TeamMode.Individual||mode.SeatTeams.Count!=0||mode.BotsAllowed||mode.MinHumans!=2||mode.MaxHumans!=2||mode.ExecutionMode!="LOCAL")
                    throw new ArgumentException("UNSUPPORTED_LOCAL_SESSION");
                return mode;
            }
            if(key!=SupportedModeKey) throw new ArgumentException("UNSUPPORTED_MODE");
            if(mode.PlayerCount!=4||mode.TeamMode!=TeamMode.FixedTeams||mode.SeatTeams.Count!=2||
                mode.SeatTeams.Any(t=>t.Count!=2)||mode.ExecutionMode!="LOCAL"||!mode.BotsAllowed||mode.MinHumans>1||mode.MaxHumans<1)
                throw new ArgumentException("UNSUPPORTED_LOCAL_SESSION");
            return mode;
        }
        public static MatchRuleSnapshot Freeze(GameCatalogSnapshot catalog,GameCatalogSource source,string key=SupportedModeKey)
            => new MatchRuleSnapshot(catalog,source,SupportedMode(catalog,key));
    }
}
