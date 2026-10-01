using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Domino.Infrastructure.Api
{
    // Domain clients share the production token provider, configuration and transport. No independent HTTP stack.
    public sealed class OnboardingApiSession : IDisposable
    {
        readonly DominoApiConfiguration config; readonly IAuthTokenProvider tokens; readonly IApiTransport transport;
        readonly Func<string> currentIdentity; readonly string owner; readonly CancellationTokenSource lifetime;
        readonly CancellationToken sessionToken;
        bool disposed;
        public OnboardingApiSession(DominoApiConfiguration config, IAuthTokenProvider tokens, IApiTransport transport,
            Func<string> currentIdentity, CancellationToken sessionLifetime)
        {
            this.config=config; this.tokens=tokens; this.currentIdentity=currentIdentity; owner=currentIdentity();
            lifetime=CancellationTokenSource.CreateLinkedTokenSource(sessionLifetime); sessionToken=lifetime.Token;
            this.transport=new SessionApiTransport(transport,sessionToken);
        }
        public void EnsureCurrent() { if(disposed || string.IsNullOrEmpty(owner) || currentIdentity()!=owner) throw new OperationCanceledException(); sessionToken.ThrowIfCancellationRequested(); }
        public void Dispose() { if(disposed)return; disposed=true; lifetime.Cancel(); lifetime.Dispose(); }
        internal string Owner { get { EnsureCurrent(); return owner; } }
        public static string Locale(string locale) => System.Text.RegularExpressions.Regex.IsMatch(locale ?? "", @"^(es|en)(-[a-z]{2})?$",System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? locale.Substring(0,2).ToLowerInvariant() : "en";
        public static string Query(string locale,int? version) {
            if(version.HasValue && version.Value<=0)throw new ArgumentOutOfRangeException(nameof(version));
            return "?locale="+Locale(locale)+(version.HasValue?"&version="+version.Value.ToString(System.Globalization.CultureInfo.InvariantCulture):"");
        }
        internal static readonly JsonSerializerSettings JsonSettings=new JsonSerializerSettings { NullValueHandling=NullValueHandling.Ignore, DateParseHandling=DateParseHandling.None, TypeNameHandling=TypeNameHandling.None, MaxDepth=32 };
        public static string Serialize(object value)=>JsonConvert.SerializeObject(value,JsonSettings);
        public static T Decode<T>(string json) {
            try {
                if(string.IsNullOrWhiteSpace(json)||json.Length>1024*1024)throw new FormatException();
                using var reader=new JsonTextReader(new StringReader(json)){MaxDepth=32,DateParseHandling=DateParseHandling.None};
                var root=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
                if(reader.Read())throw new FormatException();
                ValidateTypes(root,typeof(T));
                string required=typeof(T)==typeof(OnboardingStateDto)?"status,revision,domainRevisions,answers,updatedAt,completedStepKeys,skippedStepKeys,requiredFieldsMissing":
                    typeof(T)==typeof(OnboardingMutationDto)?"onboarding":typeof(T)==typeof(OnboardingCatalogDto)?"catalogVersion,locale,steps,requiredCapabilities":
                    typeof(T)==typeof(CoachCatalogDto)?"catalogVersion,resolvedLocale,items":typeof(T)==typeof(MembershipCatalogDto)?"schemaVersion,catalogVersion,resolvedLocale,plans,features,trialPresentation":
                    typeof(T)==typeof(TrialActivationResponseDto)?"operationId,outcome,entitlements":typeof(T)==typeof(EntitlementSummaryDto)?"availability":"";
                foreach(var field in required.Split(','))if(field.Length>0 && (root[field]==null||root[field].Type==JTokenType.Null))throw new FormatException();
                return root.ToObject<T>(JsonSerializer.Create(JsonSettings)) ?? throw new FormatException();
            } catch { throw new DominoApiException(ApiFailure.Contract); }
        }
        static void ValidateTypes(JToken value,Type type) {
            var nullable=Nullable.GetUnderlyingType(type);
            if(value.Type==JTokenType.Null) { if(type.IsValueType&&nullable==null)throw new FormatException();return; }
            type=nullable??type;
            if(type==typeof(string)){if(value.Type!=JTokenType.String)throw new FormatException();return;}
            if(type==typeof(bool)){if(value.Type!=JTokenType.Boolean)throw new FormatException();return;}
            if(type==typeof(int)||type==typeof(long)){if(value.Type!=JTokenType.Integer)throw new FormatException();return;}
            if(type.IsArray){if(!(value is JArray array))throw new FormatException();foreach(var item in array)ValidateTypes(item,type.GetElementType());return;}
            if(!(value is JObject obj))throw new FormatException();
            foreach(var field in type.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
                if(obj.TryGetValue(field.Name,out var child)) {
                    // Existing entitlement codec explicitly permits maximum:null for unlimited limits.
                    if(type==typeof(EntitlementLimitDto)&&field.Name=="maximum"&&child.Type==JTokenType.Null)continue;
                    ValidateTypes(child,field.FieldType);
                }
        }
        internal async Task<T> Send<T>(string method,string path,string body,CancellationToken cancellation)
        {
            EnsureCurrent(); if(!config.IsAvailable)throw new DominoApiException(ApiFailure.Configuration);
            for(int attempt=0;attempt<2;attempt++) {
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation,sessionToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
                try {
                    string bearer;
                    try { bearer=await CancellableTask.Wait(tokens.GetIdTokenAsync(attempt==1,timeout.Token),timeout.Token); }
                    catch(OperationCanceledException){throw;} catch{throw new DominoApiException(ApiFailure.Authentication);}
                    EnsureCurrent();
                    if(string.IsNullOrWhiteSpace(bearer)||bearer.IndexOfAny(new[]{'\r','\n'})>=0)throw new DominoApiException(ApiFailure.Authentication);
                    var response=await CancellableTask.Wait(transport.SendAsync(method,new Uri(config.Endpoint,"../"+path),body,bearer,config.TimeoutSeconds,timeout.Token),timeout.Token);
                    EnsureCurrent(); timeout.Token.ThrowIfCancellationRequested();
                    if(response.Status==401 && attempt==0)continue;
                    if(response.Status!=200)throw Error(response);
                    return Decode<T>(response.Body);
                } catch(OperationCanceledException) {
                    EnsureCurrent(); cancellation.ThrowIfCancellationRequested(); throw new DominoApiException(ApiFailure.Timeout);
                } catch(DominoApiException){throw;} catch{throw new DominoApiException(ApiFailure.Transport);}
            }
            throw new DominoApiException(ApiFailure.Authentication,401);
        }
        static DominoApiException Error(ApiHttpResponse response) {
            string code=null,id=null;
            try { var error=Decode<ApiErrorDto>(response.Body); if(!string.IsNullOrEmpty(error.code) && KnownErrors.Contains("|"+error.code+"|"))code=error.code;
                if(Guid.TryParse(error.requestId,out var guid))id=guid.ToString(); } catch { }
            return new DominoApiException(response.Status==401?ApiFailure.Authentication:ApiFailure.Server,response.Status,code,id);
        }
        const string KnownErrors="|ONBOARDING_INCOMPLETE|ONBOARDING_INVALID_ANSWER|ONBOARDING_REQUIRED_FIELD_MISSING|ONBOARDING_REQUIRED_STEP_CANNOT_SKIP|ONBOARDING_ROLLOUT_UNRESOLVED|ONBOARDING_STEP_INACTIVE|ONBOARDING_STEP_NOT_FOUND|ONBOARDING_STEP_NOT_REACHABLE|PLAYER_NOT_ACTIVE|PLAYER_NOT_FOUND|UNSUPPORTED_PROFILE_BINDING|ONBOARDING_VERSION_INVALID|COACH_VERSION_INVALID|MEMBERSHIP_VERSION_INVALID|CLIENT_UPDATE_REQUIRED|REQUEST_INVALID|EMAIL_VERIFICATION_REQUIRED|AUTH_CONTEXT_UNSUPPORTED|DEPENDENCY_UNAVAILABLE|FIRESTORE_CONTENTION_EXHAUSTED|IDEMPOTENCY_CONFLICT|REVISION_MISMATCH|ONBOARDING_REVISION_MISMATCH|ONBOARDING_CATALOG_VERSION_MISMATCH|DOMAIN_REVISION_MISMATCH|ONBOARDING_NOT_STARTED|ONBOARDING_ALREADY_COMPLETED|ONBOARDING_REQUIRED_FIELDS_MISSING|ONBOARDING_STEP_INVALID|ONBOARDING_ANSWER_INVALID|ONBOARDING_CATALOG_NOT_FOUND|ONBOARDING_CATALOG_UNAVAILABLE|COACH_CATALOG_NOT_FOUND|COACH_CATALOG_UNAVAILABLE|COACH_NOT_FOUND|COACH_NOT_SELECTABLE|COACH_CATALOG_VERSION_MISMATCH|MEMBERSHIP_CATALOG_NOT_FOUND|MEMBERSHIP_CATALOG_UNAVAILABLE|TRIAL_NOT_ELIGIBLE|TRIAL_DISABLED|TRIAL_ALREADY_CONSUMED|TRIAL_NOT_APPLICABLE|TRIAL_POLICY_VERSION_MISMATCH|";
    }
    // Frozen request body survives response loss. Bound to the originating session/client; no silent new operation ID.
    public sealed class OnboardingOperation
    {
        internal readonly object Owner; internal readonly string Path, Method, Body;
        public string OperationId { get; }
        internal OnboardingOperation(object owner,string method,string path,object request,string operationId) { Owner=owner;Method=method;Path=path;Body=OnboardingApiSession.Serialize(request);OperationId=operationId; }
    }
}

