using System.Threading;
using System.Threading.Tasks;

namespace Domino.Infrastructure
{
    internal static class CancellableTask
    {
        public static async Task<T> Wait<T>(Task<T> task, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, cancelled.Task) != task)
                {
                    // Firebase SDK operations cannot be aborted; observe a late failure without publishing a result.
                    _ = task.ContinueWith(t => { var ignored = t.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    token.ThrowIfCancellationRequested();
                }
                token.ThrowIfCancellationRequested();
                return await task;
            }
        }
    }
}
