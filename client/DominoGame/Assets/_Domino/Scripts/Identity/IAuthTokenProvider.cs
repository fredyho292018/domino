using System.Threading;
using System.Threading.Tasks;

namespace Domino.Identity
{
    public interface IAuthTokenProvider
    {
        Task<string> GetIdTokenAsync(bool forceRefresh, CancellationToken cancellationToken);
    }
}
