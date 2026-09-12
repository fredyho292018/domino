using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Firebase;

static class GuestAuthTests
{
    static int checks;
    static void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
    sealed class Fake : IFirebaseClient
    {
        public int Checks, Apps, Reads, Signs;
        public string Status = "Available";
        public PlayerIdentity User, Result = new PlayerIdentity("guest-123", true);
        public Exception DependencyError, AppError, AuthError, SignError;
        public TaskCompletionSource<string> Dependencies;
        public TaskCompletionSource<PlayerIdentity> Sign;
        public Task<string> CheckDependenciesAsync()
        { Checks++; return DependencyError != null ? Task.FromException<string>(DependencyError) : Dependencies?.Task ?? Task.FromResult(Status); }
        public void InitializeApp() { Apps++; if (AppError != null) throw AppError; }
        public PlayerIdentity GetCurrentUser() { Reads++; if (AuthError != null) throw AuthError; return User; }
        public Task<PlayerIdentity> SignInAnonymouslyAsync()
        { Signs++; return SignError != null ? Task.FromException<PlayerIdentity>(SignError) : Sign?.Task ?? Task.FromResult(Result); }
    }
    static (FirebaseBootstrap, FirebaseAuthService) Service(Fake fake, CancellationToken token = default)
    {
        var bootstrap = new FirebaseBootstrap(fake, null, token);
        return (bootstrap, new FirebaseAuthService(bootstrap, fake, null, token));
    }
    static async Task Failed(Fake fake, bool firebaseReady, string message)
    {
        var (bootstrap, auth) = Service(fake);
        try { await auth.InitializeAsync(); throw new Exception("Expected failure"); }
        catch (InvalidOperationException e) { Check(e.ToString().Contains(message), "Useful error"); }
        Check(auth.State == IdentityState.Failed && auth.Error != null && auth.Current == null, "Failed identity state");
        Check((bootstrap.State == FirebaseState.Ready) == firebaseReady, "Failure stage distinguished");
        var calls = fake.Signs;
        try { await auth.InitializeAsync(); } catch (InvalidOperationException) { }
        Check(fake.Checks == 1 && fake.Signs == calls, "Failure does not repeat requests");
        if (!firebaseReady) Check(fake.Reads == 0 && fake.Signs == 0, "No auth before Firebase");
    }
    public static async Task Main()
    {
        foreach (bool anonymous in new[] { true, false })
        {
            var fake = new Fake { User = new PlayerIdentity("existing", anonymous) };
            var (bootstrap, auth) = Service(fake);
            var result = await auth.InitializeAsync();
            Check(result.Uid == "existing" && result.IsAnonymous == anonymous, "Preserve existing identity/provider");
            Check(fake.Signs == 0 && auth.ReusedExistingUser, "Reuse without sign-in");
            Check(auth.State == IdentityState.Ready && bootstrap.State == FirebaseState.Ready, "Ready states");
        }
        var pending = new Fake { Dependencies = new TaskCompletionSource<string>(), Sign = new TaskCompletionSource<PlayerIdentity>() };
        var (boot, service) = Service(pending);
        var first = service.InitializeAsync();
        var second = service.InitializeAsync();
        Check(ReferenceEquals(first, second), "Shared initialization task");
        Check(pending.Checks == 1 && pending.Reads == 0 && service.State == IdentityState.Initializing, "Dependency gate");
        pending.Dependencies.SetResult("Available");
        await Task.Yield();
        Check(service.State == IdentityState.Authenticating && pending.Signs == 1, "Authenticating state");
        Check(ReferenceEquals(first, service.InitializeAsync()), "Deduplicate during sign-in");
        pending.Sign.SetResult(pending.Result);
        var identity = await first;
        Check(identity.Uid == "guest-123" && identity.IsAnonymous, "Guest UID propagation");
        Check(ReferenceEquals(identity, await second) && pending.Signs == 1, "One sign-in");
        Check(ReferenceEquals(first, service.InitializeAsync()), "Idempotent after ready");
        Check(ReferenceEquals(boot.InitializeAsync(), boot.InitializeAsync()), "Idempotent bootstrap");
        await Failed(new Fake { Status = "UnavailableOther" }, false, "dependency unavailable");
        await Failed(new Fake { DependencyError = new InvalidOperationException("dependency fault") }, false, "dependency fault");
        await Failed(new Fake { AppError = new InvalidOperationException("app fault") }, false, "app fault");
        await Failed(new Fake { AuthError = new InvalidOperationException("FirebaseAuth unavailable") }, true, "FirebaseAuth unavailable");
        await Failed(new Fake { SignError = new InvalidOperationException("network fault") }, true, "Anonymous sign-in failed");
        await Failed(new Fake { Result = null }, true, "user returned null");
        var cancelled = new Fake { Dependencies = new TaskCompletionSource<string>() };
        using var stop = new CancellationTokenSource();
        var (_, stopped) = Service(cancelled, stop.Token);
        var task = stopped.InitializeAsync(); stop.Cancel(); cancelled.Dependencies.SetResult("Available");
        try { await task; throw new Exception("Expected cancellation"); } catch (OperationCanceledException) { checks++; }
        Check(cancelled.Apps == 0 && cancelled.Signs == 0, "No SDK work after lifetime ends");
        Console.WriteLine($"GUEST_AUTH_UNIT_TESTS=PASS CHECKS={checks}");
    }
}
