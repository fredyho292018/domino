using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;

namespace Domino.Catalog
{
    public interface IGameCatalogApi { Task<string> FetchAsync(CancellationToken token); }
    public sealed class GameCatalogApi : IGameCatalogApi
    {
        readonly DominoApiConfiguration config;
        readonly IAuthTokenProvider tokens;
        readonly IApiTransport transport;
        public GameCatalogApi(DominoApiConfiguration config,IAuthTokenProvider tokens,IApiTransport transport)
        { this.config=config;this.tokens=tokens;this.transport=transport; }
        public async Task<string> FetchAsync(CancellationToken cancellation)
        {
            if(!config.IsAvailable)throw new DominoApiException(ApiFailure.Configuration);
            var endpoint=new Uri(config.Endpoint,"../game-modes");
            for(int attempt=0;attempt<2;attempt++)
            {
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
                try {
                    var bearer=await CancellableTask.Wait(tokens.GetIdTokenAsync(attempt==1,timeout.Token),timeout.Token);
                    if(string.IsNullOrWhiteSpace(bearer)||bearer.IndexOfAny(new[]{'\r','\n'})>=0)throw new DominoApiException(ApiFailure.Authentication);
                    var response=await CancellableTask.Wait(transport.SendAsync("GET",endpoint,null,bearer,config.TimeoutSeconds,timeout.Token),timeout.Token);
                    timeout.Token.ThrowIfCancellationRequested();
                    if(response.Status==401&&attempt==0)continue;
                    if(response.Status!=200)throw new DominoApiException(response.Status==401?ApiFailure.Authentication:ApiFailure.Server,response.Status);
                    return response.Body;
                } catch(OperationCanceledException) { if(cancellation.IsCancellationRequested)throw;throw new DominoApiException(ApiFailure.Timeout); }
            }
            throw new DominoApiException(ApiFailure.Authentication);
        }
    }
}
