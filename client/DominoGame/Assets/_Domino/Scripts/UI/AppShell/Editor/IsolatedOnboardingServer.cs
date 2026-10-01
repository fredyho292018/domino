using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Api;
using Domino.UI.AppShell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Domino.Editor
{
    // Editor/test transport only. Never constructs an HTTP client or reads a real session.
    // Mirrors progress/cursor/required-step semantics of OnboardingProgressService.
    public sealed class IsolatedOnboardingServer : IApiTransport, IAuthTokenProvider
    {
        readonly int version;
        readonly Func<int,string,OnboardingCatalogDto> catalog;
        readonly Func<string,CoachCatalogDto> coaches;
        readonly Func<string,MembershipCatalogDto> membership;
        readonly Dictionary<string,ApiHttpResponse> receipts=new Dictionary<string,ApiHttpResponse>();
        public readonly List<string> Requests=new List<string>();
        public readonly List<string> OperationIds=new List<string>();
        public OnboardingStateDto State=new OnboardingStateDto{status="NOT_STARTED",revision=7,answers=Array.Empty<OnboardingAnswerDto>(),domainRevisions=new OnboardingDomainRevisionsDto(),completedStepKeys=Array.Empty<string>(),skippedStepKeys=Array.Empty<string>(),requiredFieldsMissing=Array.Empty<string>(),updatedAt="2030-01-01T00:00:00Z"};
        public TrialEligibilityDto Eligibility=new TrialEligibilityDto{eligible=true,activationMode="EXPLICIT",policyVersion=1,periodDays=7};
        public EntitlementSummaryDto Access=new EntitlementSummaryDto{availability="AVAILABLE",snapshot=new EffectiveEntitlementsDto{plan="FREE",revision=1}};
        public string LoseResponseAt,TrialError;
        public TaskCompletionSource<bool> Hold;
        public int Applied,TrialApplied;
        public IsolatedOnboardingServer(int version,Func<int,string,OnboardingCatalogDto> catalog,Func<string,CoachCatalogDto> coaches,Func<string,MembershipCatalogDto> membership)
        {this.version=version;this.catalog=catalog;this.coaches=coaches;this.membership=membership;}
        public Task<string> GetIdTokenAsync(bool force,CancellationToken token)=>Task.FromResult("isolated-fixture-only");
        static ApiHttpResponse Ok(object value)=>new ApiHttpResponse(200,JsonConvert.SerializeObject(value));
        static ApiHttpResponse Error(int status,string code)=>new ApiHttpResponse(status,JsonConvert.SerializeObject(new{code=code}));
        string[] Steps=>version==2?new[]{"BASIC_PROFILE_STEP","EXPERIENCE_STEP","COACH_STEP","CONTACTS_STEP","MEMBERSHIP_STEP"}:new[]{"EXPERIENCE_STEP","COACH_STEP","CONTACTS_STEP","MEMBERSHIP_STEP"};
        public async Task<ApiHttpResponse> SendAsync(string method,Uri url,string body,string bearer,int timeout,CancellationToken token)
        {
            string path=url.AbsolutePath.Split('/').Last();Requests.Add(method+" "+path);
            if(Hold!=null)await Hold.Task;
            var locale=url.Query.Contains("locale=es")?"es":"en";
            if(method=="GET"){
                if(path=="onboarding")return Ok(State);
                if(path=="coaches")return Ok(coaches(locale));
                if(path=="entitlements")return Ok(Access);
                if(url.AbsolutePath.Contains("membership/catalog"))return Ok(membership(locale));
                return Ok(catalog(version,locale));
            }
            var request=JObject.Parse(body);string id=(string)request["operationId"];OperationIds.Add(id);
            if(receipts.TryGetValue(id,out var replay))return replay;
            if(path=="activate"){
                if(TrialError!=null)return Error(409,TrialError);
                Access=new EntitlementSummaryDto{availability="AVAILABLE",snapshot=new EffectiveEntitlementsDto{plan="PREMIUM_TRIAL",trialActive=true,trialConsumed=true,trialEndsAt="2030-01-08T00:00:00Z",revision=23}};TrialApplied++;
                replay=Ok(new TrialActivationResponseDto{operationId=id,outcome="ACTIVATED",entitlements=Access});
            }else{
                if((long)request["expectedRevision"]!=State.revision)return Error(409,"REVISION_MISMATCH");
                if(path!="start"&&(int?)request["catalogVersion"]!=State.catalogVersion)return Error(409,"ONBOARDING_CATALOG_VERSION_MISMATCH");
                if(path=="start"){State.status="IN_PROGRESS";State.catalogVersion=version;State.currentStepKey=Steps[0];}
                else if(path=="complete"){
                    if(Steps.Where(s=>s!="CONTACTS_STEP"&&s!="MEMBERSHIP_STEP").Any(s=>!State.completedStepKeys.Contains(s)))return Error(400,"ONBOARDING_INCOMPLETE");
                    State.status="COMPLETED";State.currentStepKey=null;State.completedAt="2030-01-01T01:00:00Z";
                }else if(path=="cursor"){
                    string target=(string)request["targetStepKey"];int index=Array.IndexOf(Steps,target);
                    if(index<0||Steps.Take(index).Any(s=>!State.completedStepKeys.Contains(s)&&!State.skippedStepKeys.Contains(s)))return Error(409,"ONBOARDING_STEP_NOT_REACHABLE");
                    State.currentStepKey=target;
                }else{
                    int index=Array.IndexOf(Steps,path);
                    if(index<0||Steps.Take(index).Any(s=>!State.completedStepKeys.Contains(s)&&!State.skippedStepKeys.Contains(s)))return Error(409,"ONBOARDING_STEP_NOT_REACHABLE");
                    if((string)request["action"]=="SKIP"){
                        if(path!="CONTACTS_STEP"&&path!="MEMBERSHIP_STEP")return Error(400,"ONBOARDING_REQUIRED_STEP_CANNOT_SKIP");
                        State.skippedStepKeys=State.skippedStepKeys.Concat(new[]{path}).Distinct().ToArray();
                    }else{
                        var answers=request["answers"].ToObject<OnboardingAnswerDto[]>();
                        State.answers=State.answers.Where(a=>!answers.Any(b=>b.questionKey==a.questionKey)).Concat(answers).ToArray();
                        if(path=="BASIC_PROFILE_STEP"){
                            string Text(string key)=>answers.Single(a=>a.questionKey==key).textValue;
                            string Key(string key)=>answers.Single(a=>a.questionKey==key).optionKey;
                            State.basicProfile=new BasicProfileDto{firstName=Text("FIRST_NAME"),lastName=Text("LAST_NAME"),displayName=Text("DISPLAY_NAME"),countryCode=Key("COUNTRY"),preferredLocale=Key("PREFERRED_LANGUAGE")};
                            State.domainRevisions.profile+=2;State.domainRevisions.preferences+=3;
                        }else State.domainRevisions.domino+=4;
                        State.completedStepKeys=State.completedStepKeys.Concat(new[]{path}).Distinct().ToArray();
                    }
                    // Saving after Back follows backend's first-unresolved rule, not local next-page order.
                    State.currentStepKey=Steps.FirstOrDefault(s=>!State.completedStepKeys.Contains(s)&&!State.skippedStepKeys.Contains(s))??Steps.Last();
                }
                State.revision+=7;Applied++;
                replay=path.EndsWith("_STEP")?Ok(new OnboardingMutationDto{onboarding=State}):Ok(State);
            }
            receipts.Add(id,replay);
            if(LoseResponseAt==path){LoseResponseAt=null;throw new InvalidOperationException("Simulated lost response after commit");}
            return replay;
        }
        public OnboardingShellController Controller(out OnboardingApiSession session,Func<string> identity=null)
        {
            session=new OnboardingApiSession(new DominoApiConfiguration(true,"https://example.test"),this,this,identity??(()=>"isolated-owner"),default);
            return new OnboardingShellController(new OnboardingShellApiSource(new OnboardingApiClient(session),new CoachCatalogApiClient(session),new MembershipCatalogApiClient(session),new TrialActivationApiClient(session,null),()=>Eligibility,()=>Access));
        }
    }
}
