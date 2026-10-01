using System;
using System.Collections.Generic;
namespace Domino.Infrastructure.Api
{
    // Asset allowlist only, never Coach selection authority. Unknown keys get a neutral silhouette/fallback from the caller.
    public static class CoachAvatarResources
    {
        static readonly Dictionary<string,string> Paths=new Dictionary<string,string>(StringComparer.Ordinal) {
            {"COACH_AMARA","AppShellMockCoaches/coach_amara"},{"COACH_DAVID","AppShellMockCoaches/coach_david"},
            {"COACH_ELENA","AppShellMockCoaches/coach_elena"},{"COACH_GABRIEL","AppShellMockCoaches/coach_gabriel"},
            {"COACH_LEO","AppShellMockCoaches/coach_leo"},{"COACH_LUCIA","AppShellMockCoaches/coach_lucia"},
            {"COACH_MATEO","AppShellMockCoaches/coach_mateo"},{"COACH_MEI","AppShellMockCoaches/coach_mei"},
            {"COACH_OMAR","AppShellMockCoaches/coach_omar"},{"COACH_SOFIA","AppShellMockCoaches/coach_sofia"}
        };
        public static string Path(string avatarKey)=>avatarKey!=null && Paths.TryGetValue(avatarKey,out var path)?path:null;
        public static T Resolve<T>(string avatarKey,Func<string,T> load,T neutralFallback) where T:class {
            var path=Path(avatarKey);if(path==null)return neutralFallback;
            try{return load(path)??neutralFallback;}catch{return neutralFallback;}
        }
    }
}
