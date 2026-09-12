using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domino.Infrastructure.Firebase
{
    public enum FirebaseState { NotStarted, Initializing, Ready, Failed }
    public sealed class FirebaseBootstrap
    {
        readonly IFirebaseClient client;
        readonly Action<string> log;
        readonly CancellationToken lifetime;
        readonly object gate = new object();
        Task initialization;
        public FirebaseState State { get; private set; }
        public string DependencyStatus { get; private set; }
        public Exception Error { get; private set; }
        public FirebaseBootstrap(IFirebaseClient client, Action<string> log, CancellationToken lifetime = default)
        { this.client = client ?? throw new ArgumentNullException(nameof(client)); this.log = log; this.lifetime = lifetime; }
        public Task InitializeAsync()
        {
            lock (gate) return initialization ??= InitializeCoreAsync();
        }
        async Task InitializeCoreAsync()
        {
            State = FirebaseState.Initializing;
            try
            {
                lifetime.ThrowIfCancellationRequested();
                DependencyStatus = await client.CheckDependenciesAsync();
                lifetime.ThrowIfCancellationRequested();
                if (DependencyStatus != "Available") throw new InvalidOperationException("Firebase dependency unavailable: " + DependencyStatus);
                log?.Invoke("[FIREBASE] Dependencies available");
                client.InitializeApp();
                State = FirebaseState.Ready;
                log?.Invoke("[FIREBASE] App initialized");
            }
            catch (Exception error) { Error = error; State = FirebaseState.Failed; throw; }
        }
    }
}
