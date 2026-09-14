using System;
using System.Diagnostics;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Domino.Online
{
    // Receipt-anchored monotonic estimate. Wall-clock changes cannot grant client authority.
    public sealed class OnlineTurnClock
    {
        readonly Func<double> seconds;double received,remaining;
        public bool HasDeadline {get;private set;}
        public OnlineTurnClock(Func<double> seconds=null) {this.seconds=seconds??(()=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency);}
        static DateTimeOffset? Date(JToken token) {
            if(token==null||token.Type==JTokenType.Null)return null;
            if(token.Type==JTokenType.Date) {
                var value=((JValue)token).Value;
                if(value is DateTimeOffset offset)return offset;
                if(value is DateTime date)return new DateTimeOffset(date.Kind==DateTimeKind.Unspecified?DateTime.SpecifyKind(date,DateTimeKind.Utc):date);
            }
            return DateTimeOffset.TryParse((string)token,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var result)?result:(DateTimeOffset?)null;
        }
        public void Apply(JObject snapshot) {
            var deadline=Date(snapshot["turnDeadlineAt"]??snapshot["publicState"]?["turnDeadline"]);
            var server=Date(snapshot["serverNow"]);
            HasDeadline=deadline.HasValue&&server.HasValue;received=seconds();
            remaining=HasDeadline?(deadline.Value-server.Value).TotalSeconds:0;
        }
        public double RemainingSeconds=>HasDeadline?Math.Max(0,remaining-(seconds()-received)):0;
        public bool Expired=>HasDeadline&&RemainingSeconds<=0;
    }
}
