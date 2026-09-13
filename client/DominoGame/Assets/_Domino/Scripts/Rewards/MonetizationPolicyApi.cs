using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Domino.Ads;
using Newtonsoft.Json.Linq;

namespace Domino.Rewards
{
    public sealed class MonetizationPolicyApi : IMonetizationPolicyApi
    {
        readonly RewardIntentApiClient api;
        public MonetizationPolicyApi(RewardIntentApiClient api){this.api=api;}
        public Task<MonetizationPolicySnapshot> ConfigAsync(CancellationToken token)=>api.GetResourceAsync("../../monetization/config",token,ReadPolicy);
        public Task<RewardEligibilitySnapshot> EligibilityAsync(string opportunityId,CancellationToken token)=>api.GetResourceAsync(
            "eligibility"+(opportunityId==null?"":"?opportunityId="+Uri.EscapeDataString(opportunityId)),token,ReadEligibility);
        public Task<string> OpportunityAsync(CancellationToken token)=>api.PostResourceAsync("opportunities",token,body=>{
            var id=(string)Parse(body)["opportunityId"];if(!Guid.TryParseExact(id,"D",out var guid)||guid==Guid.Empty)throw new FormatException();return id;});
        static JObject Parse(string json)
        {
            if(string.IsNullOrWhiteSpace(json)||json.Length>8192)throw new FormatException();
            using var reader=new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(json)){MaxDepth=8,DateParseHandling=Newtonsoft.Json.DateParseHandling.None};
            var root=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            if(reader.Read())throw new FormatException();return root;
        }
        static long Number(JToken root,string key)=>root?[key]?.Type==JTokenType.Integer?root[key].Value<long>():throw new FormatException();
        static bool Boolean(JToken root,string key)=>root?[key]?.Type==JTokenType.Boolean?root[key].Value<bool>():throw new FormatException();
        static DateTimeOffset Time(JToken value)=>value?.Type==JTokenType.String&&DateTimeOffset.TryParse((string)value,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var time)?time:throw new FormatException();
        public static MonetizationPolicySnapshot ReadPolicy(string json)
        {
            var root=Parse(json);var r=root["rewarded"];var limits=r?["limits"];
            return new MonetizationPolicySnapshot(Number(root,"version"),Boolean(root,"adsEnabled"),Boolean(r,"enabled"),Number(r,"rewardCoins"),
                checked((int)Number(limits,"perRound")),Number(limits,"cooldownSeconds"),checked((int)Number(limits,"perHour")),checked((int)Number(limits,"perDay")),Number(limits,"maxCoinsPerDay"));
        }
        public static RewardEligibilitySnapshot ReadEligibility(string json)
        {
            var root=Parse(json);var remain=root["remaining"];var next=root["nextEligibleAt"];
            return new RewardEligibilitySnapshot(Boolean(root,"eligible"),Number(root,"rewardCoins"),(string)root["reason"],
                next==null||next.Type==JTokenType.Null?(DateTimeOffset?)null:Time(next),Time(root["serverTime"]),
                checked((int)Number(remain,"hour")),checked((int)Number(remain,"day")),Number(remain,"coinsToday"));
        }
    }
}
