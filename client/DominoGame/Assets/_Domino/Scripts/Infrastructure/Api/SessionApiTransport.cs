using System;
using System.Threading;
using System.Threading.Tasks;
namespace Domino.Infrastructure.Api {
    // Captures a session generation; old API objects cannot issue requests for the next user.
    public sealed class SessionApiTransport : IApiTransport {
        readonly IApiTransport inner;readonly CancellationToken session;
        public SessionApiTransport(IApiTransport inner,CancellationToken session){this.inner=inner;this.session=session;}
        public async Task<ApiHttpResponse> SendAsync(string method,Uri uri,string json,string token,int timeoutSeconds,CancellationToken cancellationToken){
            using var linked=CancellationTokenSource.CreateLinkedTokenSource(session,cancellationToken);
            linked.Token.ThrowIfCancellationRequested();
            var result=await inner.SendAsync(method,uri,json,token,timeoutSeconds,linked.Token);
            linked.Token.ThrowIfCancellationRequested();return result;
        }
    }
}
