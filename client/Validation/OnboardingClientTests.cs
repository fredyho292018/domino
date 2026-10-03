using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Api;
using Newtonsoft.Json.Linq;
static class OnboardingClientTests
{
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
    static async Task Fails(Func<Task> action, string code=null){try{await action();throw new Exception("Expected failure");}catch(DominoApiException e){Check(code==null||e.ServerErrorCode==code,"error mapping");}}
    sealed class Tokens:IAuthTokenProvider {public List<bool> Refresh=new List<bool>();public Task<string> GetIdTokenAsync(bool refresh,CancellationToken t){Refresh.Add(refresh);return Task.FromResult("fixture-bearer");}}
    sealed class Wire:IApiTransport {
        public readonly List<(string method,Uri uri,string body)> Calls=new List<(string,Uri,string)>();
        public Func<string,Uri,string,Task<ApiHttpResponse>> Reply;
        public Task<ApiHttpResponse> SendAsync(string m,Uri u,string j,string bearer,int timeout,CancellationToken t){Calls.Add((m,u,j));return Reply(m,u,j);}
    }
    static ApiHttpResponse Ok(string json)=>new ApiHttpResponse(200,json);
    static string State(long revision=0,string status="NOT_STARTED",int? version=null)=>new JObject {
        ["status"]=status,["revision"]=revision,["catalogVersion"]=version,["currentStepKey"]=version.HasValue?"EXPERIENCE_STEP":null,
        ["currentSubstepKey"]=null,["lastCompletedStepKey"]=null,["completedStepKeys"]=new JArray(),["skippedStepKeys"]=new JArray(),
        ["startedAt"]=null,["completedAt"]=null,["updatedAt"]="2026-09-30T00:00:00Z",["completionOrigin"]=null,
        ["requiredFieldsMissing"]=new JArray(),["domainRevisions"]=new JObject{["profile"]=2,["preferences"]=3,["domino"]=4},["answers"]=new JArray()
    }.ToString();
            sealed class HistoryApi:Domino.Online.IOnlineMatchApi {
        public string Path;
        public Task<JObject> SendAsync(string method,string path,JObject body,CancellationToken token){Check(method=="GET"&&body==null,"history read only");Path=path;return Task.FromResult(new JObject{["items"]=new JArray()});}
    }
    sealed class Identity:IPlayerIdentityService {
        public IdentityState State=>IdentityState.Ready; public PlayerIdentity Current=>new PlayerIdentity("fixture-a",true); public Exception Error=>null;
        public Task<PlayerIdentity> InitializeAsync()=>Task.FromResult(Current);
    }
    static void BackendContracts() {
        var root=System.IO.Path.Combine(Environment.CurrentDirectory,"server/domino/src/main/kotlin/com/teamfho/domino");
        var mappings=new (string file,string backend,Type client)[]{
            ("catalog/OnboardingCatalog.kt","OnboardingCatalogResponse",typeof(OnboardingCatalogDto)),
            ("catalog/OnboardingCatalog.kt","LocalizedStep",typeof(OnboardingStepDto)),
            ("catalog/OnboardingCatalog.kt","LocalizedQuestion",typeof(OnboardingQuestionDto)),
            ("catalog/OnboardingCatalog.kt","LocalizedOption",typeof(OnboardingOptionDto)),
            ("catalog/CoachCatalog.kt","CoachCatalogResponse",typeof(CoachCatalogDto)),
            ("catalog/CoachCatalog.kt","LocalizedCoach",typeof(CoachDto)),
            ("catalog/CoachCatalog.kt","CoachAvatar",typeof(CoachAvatarDto)),
            ("catalog/MembershipCatalog.kt","MembershipCatalogResponse",typeof(MembershipCatalogDto)),
            ("catalog/MembershipCatalog.kt","MembershipPlanResponse",typeof(MembershipPlanDto)),
            ("catalog/MembershipCatalog.kt","MembershipFeatureResponse",typeof(MembershipFeatureDto)),
            ("catalog/MembershipCatalog.kt","MembershipPlanFeatureResponse",typeof(MembershipPlanFeatureDto)),
            ("catalog/MembershipCatalog.kt","BillingProductMetadataResponse",typeof(BillingProductDto)),
            ("catalog/MembershipCatalog.kt","MembershipTrialPresentation",typeof(MembershipTrialDto)),
            ("catalog/MembershipCatalog.kt","MembershipFamilyPresentation",typeof(MembershipFamilyDto)),
            ("catalog/MembershipCatalog.kt","MembershipQuota",typeof(MembershipQuotaDto)),
            ("player/OnboardingProgressModels.kt","OnboardingBasicProfile",typeof(BasicProfileDto)),
            ("player/OnboardingProgressModels.kt","OnboardingDomainRevisions",typeof(OnboardingDomainRevisionsDto)),
            ("player/OnboardingProgressModels.kt","OnboardingStartRequest",typeof(OnboardingStartDto)),
            ("player/OnboardingProgressModels.kt","OnboardingCursorRequest",typeof(OnboardingCursorDto)),
            ("player/OnboardingProgressModels.kt","OnboardingCompleteRequest",typeof(OnboardingCompleteDto)),
            ("entitlement/TrialActivation.kt","TrialActivationRequest",typeof(TrialActivationRequestDto)),
            ("entitlement/TrialActivation.kt","TrialActivationResponse",typeof(TrialActivationResponseDto))
        };
        foreach(var map in mappings){
            var source=System.IO.File.ReadAllText(System.IO.Path.Combine(root,map.file));
            var match=System.Text.RegularExpressions.Regex.Match(source,@"data class "+map.backend+@"\((.*?)\)\r?\n",System.Text.RegularExpressions.RegexOptions.Singleline);
            var names=System.Text.RegularExpressions.Regex.Matches(match.Groups[1].Value,@"\bval\s+(\w+)\s*:").Select(m=>m.Groups[1].Value).OrderBy(x=>x);
            Check(match.Success&&names.SequenceEqual(map.client.GetFields().Select(f=>f.Name).OrderBy(x=>x)),"backend DTO field parity "+map.backend);
        }
    }
    static async Task Main(){
                BackendContracts();
        var historyApi=new HistoryApi();var history=new Domino.Replay.ReplayClient(historyApi);
        Check(((JArray)(await history.History(null,default))["items"]).Count==0,"history empty response");
        Check(historyApi.Path=="players/me/history?limit=20","history existing contract");
        await history.History("fixture/cursor",default);Check(historyApi.Path.EndsWith("cursor=fixture%2Fcursor"),"history cursor escaped");
        string owner="fixture-a";var tokens=new Tokens();var wire=new Wire();using var lifetime=new CancellationTokenSource();
        using var session=new OnboardingApiSession(new DominoApiConfiguration(true,"https://example.test"),tokens,wire,()=>owner,lifetime.Token);
        var client=new OnboardingApiClient(session);wire.Reply=(m,u,j)=>Task.FromResult(Ok(State()));await client.LoadAsync(default);
        foreach(var availability in new[]{"AVAILABLE","TAKEN"}) {
            wire.Reply=(m,u,j)=>Task.FromResult(Ok("{\"state\":\""+availability+"\"}"));
            Check(await client.AliasAvailabilityAsync("FixtureAlias",default)==availability,"availability contract");
            Check(wire.Calls.Last().method=="POST"&&wire.Calls.Last().uri.AbsolutePath=="/api/v1/player/display-name/availability"&&wire.Calls.Last().uri.Query=="","availability private request path");
            Check((string)JObject.Parse(wire.Calls.Last().body)["displayName"]=="FixtureAlias","availability request body");
        }
        foreach(var malformed in new[]{"{}","{\"state\":true}","{\"state\":\"UNKNOWN\"}"}){
            wire.Reply=(m,u,j)=>Task.FromResult(Ok(malformed));await Fails(()=>client.AliasAvailabilityAsync("FixtureAlias",default));
        }
        foreach(var code in new[]{"DISPLAY_NAME_TAKEN","DISPLAY_NAME_RESERVATIONS_NOT_READY"}) {
            wire.Reply=(m,u,j)=>Task.FromResult(new ApiHttpResponse(409,"{\"code\":\""+code+"\"}"));
            await Fails(()=>client.AliasAvailabilityAsync("FixtureAlias",default),code);
        }
        Check(client.State.status=="NOT_STARTED","initial state");
        var start=client.PrepareStart();wire.Reply=(m,u,j)=>Task.FromResult(Ok(State(1,"IN_PROGRESS",1)));await client.ExecuteAsync(start,default);
        Check(client.State.catalogVersion==1,"start bare response/pinning");Check(JObject.Parse(wire.Calls.Last().body).Properties().Count()==2,"start exact request");
        foreach(var locale in new[]{"es","es-US","en","en-US"})foreach(int version in new[]{1,2}) {
            wire.Reply=(m,u,j)=>Task.FromResult(Ok(State(2,"IN_PROGRESS",version)));await client.LoadAsync(default);
            wire.Reply=(m,u,j)=>Task.FromResult(Ok(new JObject{["catalogVersion"]=version,["locale"]=locale.Substring(0,2),["requiredCapabilities"]=new JArray("SINGLE_SELECT_V1"),["coachCatalogVersion"]=1,["membershipCatalogVersion"]=1,["steps"]=new JArray(new JObject{["key"]="EXPERIENCE_STEP",["stage"]="EXPERIENCE",["required"]=true,["skippable"]=false,["kind"]="QUESTION",["availability"]="AVAILABLE",["sortOrder"]=0,["nextStepKey"]=null,["title"]="Experience",["description"]="",["questions"]=new JArray(new JObject{["key"]="EXPERIENCE",["type"]="SINGLE_SELECT",["typeVersion"]=1,["sortOrder"]=0,["required"]=true,["title"]="Level",["description"]="",["options"]=new JArray(new JObject{["key"]="BEGINNER",["sortOrder"]=0,["title"]="Beginner",["description"]=""})})})}.ToString()));
            var cat=await client.CatalogAsync(locale,default);Check(cat.catalogVersion==version,"version");Check(wire.Calls.Last().uri.Query=="?locale="+locale.Substring(0,2)+"&version="+version,"locale pinned query");Check(client.State.revision==2,"locale preserves progress");Check(cat.steps[0].questions[0].options[0].key=="BEGINNER","localized public shape");
        }
        var answer=OnboardingApiClient.Experience("EXPERIENCE","STRATEGY");var save=client.PrepareSave("EXPERIENCE_STEP","SAVE",new[]{answer});answer.optionKey="COMPETITIVE";
        int attempts=0;wire.Reply=(m,u,j)=>{if(attempts++==0)throw new Exception("lost response");return Task.FromResult(Ok("{\"onboarding\":"+State(3,"IN_PROGRESS",2)+",\"domino\":null}"));};
        await Fails(()=>client.ExecuteAsync(save,default));await client.ExecuteAsync(save,default);
        Check(wire.Calls[wire.Calls.Count-1].body==wire.Calls[wire.Calls.Count-2].body,"lost save exact retry");Check(client.State.revision==3,"server revision replaces");
        var body=JObject.Parse(wire.Calls.Last().body);Check((string)body["operationId"]==save.OperationId,"stable operation");Check((string)body["answers"][0]["optionKey"]=="STRATEGY","immutable body");Check(((JObject)body["domainRevisions"]).Properties().Single().Name=="domino","exact domain keys");
        wire.Reply=(m,u,j)=>Task.FromResult(new ApiHttpResponse(409,"{\"code\":\"REVISION_MISMATCH\"}"));await Fails(()=>client.ExecuteAsync(client.PrepareSave("EXPERIENCE_STEP","SAVE",new[]{answer}),default),"REVISION_MISMATCH");Check(client.State.revision==3,"no optimistic revision");
        wire.Reply=(m,u,j)=>Task.FromResult(Ok("{\"onboarding\":"+State(4,"IN_PROGRESS",2)+"}"));await client.ExecuteAsync(client.PrepareSave("BASIC_PROFILE_STEP","SAVE",new[]{new OnboardingAnswerDto{questionKey="FIRST_NAME",type="TEXT",textValue="Fixture"}}),default);
        body=JObject.Parse(wire.Calls.Last().body);Check(((JObject)body["domainRevisions"]).Properties().Select(x=>x.Name).SequenceEqual(new[]{"profile","preferences"}),"basic profile revisions");
        await client.ExecuteAsync(client.PrepareSave("CONTACTS_STEP","SKIP",Array.Empty<OnboardingAnswerDto>()),default);Check(!((JObject)JObject.Parse(wire.Calls.Last().body)["domainRevisions"]).HasValues,"skip empty domains");
        wire.Reply=(m,u,j)=>Task.FromResult(Ok(State(5,"IN_PROGRESS",2)));await client.ExecuteAsync(client.PrepareCursor("EXPERIENCE_STEP"),default);Check(client.State.revision==5,"cursor bare response");
        wire.Reply=(m,u,j)=>Task.FromResult(Ok(State(6,"COMPLETED",2)));await client.ExecuteAsync(client.PrepareComplete(),default);Check(client.State.status=="COMPLETED","complete bare response");
        var coach=new CoachCatalogApiClient(session);wire.Reply=(m,u,j)=>Task.FromResult(Ok("{\"catalogVersion\":1,\"resolvedLocale\":\"es\",\"items\":[{\"key\":\"FUTURE_COACH\",\"name\":\"Coach\",\"shortDescription\":\"Short\",\"description\":\"Long\",\"avatar\":{\"key\":\"COACH_FUTURE\",\"assetVersion\":1,\"storagePath\":\"untrusted\"},\"selectable\":true,\"sortOrder\":0}]}"));
        var coaches=await coach.LoadAsync("es-US",1,default);Check(coaches.items[0].key=="FUTURE_COACH","no hardcoded selection authority");Check(CoachAvatarResources.Resolve("COACH_FUTURE",p=>p,"neutral")=="neutral","unknown neutral");
        foreach(var key in new[]{"AMARA","DAVID","ELENA","GABRIEL","LEO","LUCIA","MATEO","MEI","OMAR","SOFIA"})Check(CoachAvatarResources.Path("COACH_"+key)=="AppShellMockCoaches/coach_"+key.ToLowerInvariant(),"approved asset");
        Check(CoachAvatarResources.Resolve<string>("COACH_AMARA",p=>null,"neutral")=="neutral","missing asset neutral");
                var bootstrapWire=new Wire{Reply=(m,u,j)=>Task.FromResult(Ok("{\"player\":{\"uid\":\"fixture-a\",\"accountType\":\"GUEST\",\"displayName\":\"Guest-ABCDEFGH\",\"language\":\"en\",\"status\":\"ACTIVE\"},\"wallet\":{\"coins\":0},\"entitlements\":{\"availability\":\"UNAVAILABLE\"}}"))};
        using var player=new Domino.Player.PlayerService(new Identity(),new DominoApiClient(new DominoApiConfiguration(true,"https://example.test"),tokens,bootstrapWire,new UnityApiJsonCodec()),()=>Task.FromResult("en"),default);
        await player.InitializeAsync();var priorEntitlements=player.Entitlements;Check(priorEntitlements!=null,"existing entitlement owner");
        var membership=new MembershipCatalogApiClient(session);var plans=new JArray();foreach(var key in new[]{"FREE","GOLD","PLATINUM","DIAMOND","FRIENDS_AND_FAMILY"})plans.Add(new JObject{["key"]=key,["active"]=true,["features"]=new JArray(new JObject{["featureKey"]="FEATURE",["included"]=true}),["billingProducts"]=new JArray(new JObject{["planKey"]=key,["active"]=false,["purchasable"]=false,["storeProductId"]=null})});
        wire.Reply=(m,u,j)=>Task.FromResult(Ok(new JObject{["schemaVersion"]=1,["catalogVersion"]=1,["resolvedLocale"]="en",["plans"]=plans,["features"]=new JArray(Enumerable.Range(0,8).Select(i=>new JObject{["key"]="FEATURE_"+i,["kind"]="BOOLEAN_CAPABILITY"})),["trialPresentation"]=new JObject{["policyVersion"]=1,["product"]="PREMIUM_LEGACY",["commercialTrialEnabled"]=false,["familyTrialEnabled"]=false}}.ToString()));
        var catalog=await membership.LoadAsync("en-US",1,default);Check(catalog.plans.Length==5&&catalog.features.Length==8,"membership shape");foreach(var plan in catalog.plans)Check(!MembershipCatalogApiClient.Purchasable(plan,plan.billingProducts[0]),"unconfigured not purchasable");
        Check(ReferenceEquals(priorEntitlements,player.Entitlements),"catalog no entitlement mutation"); var trial=new TrialActivationApiClient(session,player);var op=trial.Prepare(1);attempts=0;wire.Reply=(m,u,j)=>{
            if(u.AbsolutePath.EndsWith("/entitlements"))return Task.FromResult(Ok("{\"availability\":\"UNAVAILABLE\"}"));
            if(attempts++==0)throw new Exception("response lost");return Task.FromResult(Ok("{\"operationId\":\""+op.OperationId+"\",\"outcome\":\"ACTIVATED\",\"entitlements\":{\"availability\":\"AVAILABLE\"}}"));};
        await Fails(()=>trial.ExecuteAsync(op,default));var prior=wire.Calls.Last().body;await trial.ExecuteAsync(op,default);Check(wire.Calls[wire.Calls.Count-2].body==prior,"trial exact retry");Check(JObject.Parse(prior).Properties().Count()==2,"no client dates identity or plan");Check(wire.Calls.Last().uri.AbsolutePath.EndsWith("/entitlements"),"refresh receipt authority");Check(!ReferenceEquals(priorEntitlements,player.Entitlements)&&player.Entitlements.availability=="UNAVAILABLE","single owner current refresh applied");
        foreach(var path in new[]{"/api/v1/player/bootstrap","/api/v1/player/trial/activate"})Check(TrialContractHeader.Required("POST",new Uri("https://example.test"+path)),"header scope");
        Check(!TrialContractHeader.Required("GET",new Uri("https://example.test/api/v1/membership/catalog")),"no unrelated header");Check(TrialContractHeader.Name=="X-Trial-Activation-Contract"&&TrialContractHeader.Value=="1","exact header");
        wire.Reply=(m,u,j)=>Task.FromResult(new ApiHttpResponse(409,"{\"code\":\"CLIENT_UPDATE_REQUIRED\"}"));await Fails(()=>client.LoadAsync(default),"CLIENT_UPDATE_REQUIRED");
        attempts=0;wire.Reply=(m,u,j)=>Task.FromResult(attempts++==0?new ApiHttpResponse(401,"{}"):Ok(State(7,"COMPLETED",2)));await client.LoadAsync(default);Check(tokens.Refresh.Last(),"401 refresh once");
        foreach(var bad in new[]{"{}","null","[]","{\"status\":\"NOT_STARTED\",\"revision\":\"0\"}","{\"revision\":0,\"revision\":1}"}){wire.Reply=(m,u,j)=>Task.FromResult(Ok(bad));await Fails(()=>client.LoadAsync(default));}
        var held=new TaskCompletionSource<ApiHttpResponse>();wire.Reply=(m,u,j)=>held.Task;var pending=client.LoadAsync(default);owner="fixture-b";held.SetResult(Ok(State(999,"COMPLETED",2)));
        try{await pending;throw new Exception("stale response accepted");}catch(OperationCanceledException){Check(true,"A response discarded");}
        using var otherSession=new OnboardingApiSession(new DominoApiConfiguration(true,"https://example.test"),tokens,wire,()=>owner,default);var other=new OnboardingApiClient(otherSession);Check(other.State==null,"B state untouched");
        try{await other.ExecuteAsync(save,default);throw new Exception("cross session operation");}catch(ArgumentException){Check(true,"operation isolated");}
        Console.WriteLine("ONB_CLIENT_CHECKS="+checks+"_PASS");
    }
}


