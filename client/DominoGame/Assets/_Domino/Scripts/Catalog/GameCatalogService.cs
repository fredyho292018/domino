using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domino.Catalog
{
    public enum GameCatalogSource { Bundled, Cache, Remote }
    public sealed class GameCatalogService
    {
        readonly IGameCatalogApi api;
        readonly IGameCatalogCache cache;
        readonly GameCatalogCodec codec;
        readonly Func<DateTimeOffset> now;
        readonly Action<string> log;
        readonly CancellationToken lifetime;
        readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        DateTimeOffset freshUntil=DateTimeOffset.MinValue;
        DateTimeOffset retryAfter=DateTimeOffset.MinValue;
        string acceptedJson;
        public GameCatalogSnapshot Current { get; private set; }
        public GameCatalogSource Source { get; private set; }
        public const int CacheTtlSeconds=300;
        public GameCatalogService(IGameCatalogApi api,IGameCatalogCache cache,string bundled,
            CancellationToken lifetime=default,Action<string> log=null,Func<DateTimeOffset> now=null)
        {
            this.api=api;this.cache=cache;this.lifetime=lifetime;this.log=log??(_=>{});this.now=now??(()=>DateTimeOffset.UtcNow);codec=new GameCatalogCodec();
            Current=codec.Read(bundled);GameCatalogConfigurationAdapter.SupportedMode(Current);acceptedJson=bundled;Source=GameCatalogSource.Bundled;
            try {
                var saved=cache.Read();
                if(saved!=null) {
                    var snapshot=codec.Read(saved.Json);
                    GameCatalogConfigurationAdapter.SupportedMode(snapshot);
                    if(saved.DownloadedAt>this.now().AddMinutes(1))throw new FormatException();
                    Current=snapshot;acceptedJson=saved.Json;Source=GameCatalogSource.Cache;freshUntil=saved.DownloadedAt.AddSeconds(CacheTtlSeconds);
                    this.log("[GAME-CATALOG] cache hit version="+Current.CatalogVersion);
                }
            } catch { this.log("[GAME-CATALOG] cached snapshot rejected category=CONTRACT_OR_STORAGE"); }
            if(Source==GameCatalogSource.Bundled)this.log("[GAME-CATALOG] fallback bundled");
        }
        public async Task RefreshAsync(bool force=false)
        {
            try { await gate.WaitAsync(lifetime); } catch(OperationCanceledException) { return; }
            try {
                if(!force&&(now()<freshUntil||now()<retryAfter))return;
                log("[GAME-CATALOG] remote load started");
                var json=await api.FetchAsync(lifetime);lifetime.ThrowIfCancellationRequested();
                var next=codec.Read(json);
                GameCatalogConfigurationAdapter.SupportedMode(next);
                // Never replace a valid publication with different rules under the same version.
                if(next.CatalogVersion==Current.CatalogVersion&& !Newtonsoft.Json.Linq.JToken.DeepEquals(
                    Newtonsoft.Json.Linq.JObject.Parse(json),Newtonsoft.Json.Linq.JObject.Parse(acceptedJson)))throw new FormatException();
                var at=now();
                try { cache.Write(new CatalogCacheEntry(json,at));log("[GAME-CATALOG] cached snapshot persisted"); }
                catch { log("[GAME-CATALOG] cache persistence unavailable"); }
                Current=next;acceptedJson=json;Source=GameCatalogSource.Remote;freshUntil=at.AddSeconds(CacheTtlSeconds);
                log("[GAME-CATALOG] remote catalog ready version="+next.CatalogVersion);
            } catch(OperationCanceledException) { }
            catch(Exception error) {
                retryAfter=now().AddSeconds(CacheTtlSeconds);
                string category=error is Domino.Infrastructure.Api.DominoApiException apiError ? apiError.Category.ToString()
                    : error is FormatException || error is ArgumentException ? "CONTRACT" : "TRANSPORT";
                log("[GAME-CATALOG] remote rejected category="+category);
            }
            finally { gate.Release(); }
        }
        // Synchronous main-thread resolution; does not fetch or wait for identity/network.
        // Existing sessions retain this immutable object when Current is replaced.
        public MatchRuleSnapshot ResolveMatch(string key=GameCatalogConfigurationAdapter.SupportedModeKey) => GameCatalogConfigurationAdapter.Freeze(Current,Source,key);
    }
}
