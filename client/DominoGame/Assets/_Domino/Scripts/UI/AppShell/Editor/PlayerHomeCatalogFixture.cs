using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.UI.AppShell;
using Newtonsoft.Json;

namespace Domino.Editor
{
    // Fictional transport for the real CLIENT-01 catalog adapters. No SDK/HTTP credentials or live state.
    public sealed class PlayerHomeCatalogFixture : IApiTransport, IDisposable
    {
        public static readonly string[] Keys={"AMARA","DAVID","ELENA","GABRIEL","LEO","LUCIA","MATEO","MEI","OMAR","SOFIA"};
        public readonly List<string> Requests=new List<string>();
        public int OnboardingVersion=2, CoachVersion=7;
        public string InactiveKey,MissingKey;
        public bool MissingReference,WrongOnboardingVersion,WrongCoachVersion,Fail,DuplicateKey,UnknownAvatar;
        public TaskCompletionSource<bool> Hold;
        public readonly OnboardingShellController Controller;
        readonly OnboardingApiSession session;
        public PlayerHomeCatalogFixture(RoutingCompositionFixture owner)
        {
            session=new OnboardingApiSession(new DominoApiConfiguration(true,"https://example.test"),owner,this,()=>owner.Session?.Uid,default);
            Controller=new OnboardingShellController(new OnboardingShellApiSource(new OnboardingApiClient(session),new CoachCatalogApiClient(session)));
        }
        public async Task<ApiHttpResponse> SendAsync(string method,Uri url,string body,string bearer,int timeout,CancellationToken token)
        {
            Requests.Add(method+" "+url.AbsolutePath+url.Query);
            if(method!="GET" || !url.Query.Contains("version="))throw new InvalidOperationException("Only explicit versioned catalog reads are permitted");
            if(Hold!=null)await Hold.Task; // Intentionally ignores cancellation to test late-response rejection.
            if(Fail)return new ApiHttpResponse(503,"{\"code\":\"DEPENDENCY_UNAVAILABLE\"}");
            var locale=url.Query.Contains("locale=es")?"es":"en";
            if(url.AbsolutePath.EndsWith("onboarding/catalog"))
                return Ok(new OnboardingCatalogDto {catalogVersion=WrongOnboardingVersion?99:OnboardingVersion,locale=locale,requiredCapabilities=Array.Empty<string>(),
                    coachCatalogVersion=MissingReference?(int?)null:CoachVersion,steps=new[]{new OnboardingStepDto{
                        key="COACH_STEP",questions=new[]{new OnboardingQuestionDto{key="COACH_SELECTION",type="COACH_SELECT"}}}}});
            if(!url.AbsolutePath.EndsWith("/coaches"))throw new InvalidOperationException("Unexpected read");
            var entries=Keys.Where(k=>k!=MissingKey).Select(k=>new CoachDto{key=k,name=k.Substring(0,1)+k.Substring(1).ToLowerInvariant(),
                shortDescription=locale=="es"?"Practica y mejora tu estrategia.":"Practise and improve your strategy.",selectable=k!=InactiveKey,
                avatar=new CoachAvatarDto{key="COACH_"+k,assetVersion=UnknownAvatar?99:1,storagePath="AppShellMockCoaches/coach_"+k.ToLowerInvariant()+".png"}}).ToList();
            if(DuplicateKey)entries.Add(entries[0]);
            return Ok(new CoachCatalogDto{catalogVersion=WrongCoachVersion?99:CoachVersion,resolvedLocale=locale,items=entries.ToArray()});
        }
        static ApiHttpResponse Ok(object value)=>new ApiHttpResponse(200,JsonConvert.SerializeObject(value));
        public static void Select(RoutingCompositionFixture owner,string key="SOFIA",string name="HomePlayer",string locale="en")
        {
            owner.State("COMPLETED",2);
            owner.Server.State.basicProfile=new BasicProfileDto{displayName=name,preferredLocale=locale};
            owner.Server.State.domainRevisions=new OnboardingDomainRevisionsDto{profile=2,preferences=2,domino=4};
            owner.Server.State.answers=key==null?Array.Empty<OnboardingAnswerDto>():new[]{new OnboardingAnswerDto{questionKey="COACH_SELECTION",type="COACH_SELECT",optionKey=key}};
            owner.Server.Access=new EntitlementSummaryDto{availability="AVAILABLE",snapshot=new EffectiveEntitlementsDto{plan="FREE",status="FREE",revision=1}};
        }
        public void Dispose(){Controller.Dispose();session.Dispose();}
    }
}
