using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domino.Online;
using Newtonsoft.Json.Linq;

namespace Domino.Replay
{
    // Only GET methods are exposed by the replay provider. No realtime channel or gameplay commands.
    public sealed class ReplayClient
    {
        readonly IOnlineMatchApi api;
        public ReplayClient(IOnlineMatchApi api) {this.api=api;}
        public Task<JObject> History(string cursor,CancellationToken token)=>api.SendAsync("GET","players/me/history?limit=20"+(cursor==null?"":"&cursor="+Uri.EscapeDataString(cursor)),null,token);
        public Task<JObject> Manifest(string id,CancellationToken token)=>api.SendAsync("GET","matches/"+Id(id)+"/replay",null,token);
        public async Task<ReplayTimeline> Load(JObject manifest,CancellationToken token)
        {
            if((bool?)manifest["replayAvailable"]!=true)throw new ReplayDataException((string)manifest["replayAvailabilityReason"]??"INCOMPLETE_REPLAY");
            string id=Id((string)manifest["matchId"]);long last=(long)manifest["lastSequence"];
            if(last<1||last>100000)throw new ReplayDataException("INCOMPLETE_REPLAY");
            var events=new List<JObject>();
            while(events.Count<last) {
                string session=(string)manifest["accessSession"];
                var page=await api.SendAsync("GET","matches/"+id+"/replay/events?after="+events.Count+"&limit=250"+
                    (session==null?"":"&session="+Uri.EscapeDataString(session)),null,token);
                var items=page["items"] as JArray;
                if(items==null||items.Count==0||(long?)page["lastSequence"]!=last)throw new ReplayDataException("INCOMPLETE_REPLAY");
                foreach(JObject e in items){if((long?)e["sequence"]!=events.Count+1L)throw new ReplayDataException("INCOMPLETE_REPLAY");events.Add(e);}
                token.ThrowIfCancellationRequested();
            }
            // Reducer validation/checkpoint creation is CPU-only and does not block the Unity render thread.
            return await Task.Run(()=>new ReplayTimeline(manifest,events),token);
        }
        static string Id(string id) {if(string.IsNullOrEmpty(id)||id.Length>128||id.Contains("/")||id.Contains(".."))throw new ArgumentException("Invalid replay id");return Uri.EscapeDataString(id);}
    }
}
