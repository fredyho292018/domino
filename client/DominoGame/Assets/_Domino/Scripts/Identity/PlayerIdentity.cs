using System;
using System.Threading.Tasks;

namespace Domino.Identity
{
    public sealed class PlayerIdentity
    {
        public string Uid { get; }
        public bool IsAnonymous { get; }
        public PlayerIdentity(string uid, bool isAnonymous)
        {
            if (string.IsNullOrWhiteSpace(uid)) throw new ArgumentException("Firebase user UID is empty.", nameof(uid));
            Uid = uid; IsAnonymous = isAnonymous;
        }
    }
    public enum IdentityState { NotStarted, Initializing, Authenticating, Ready, Failed }
    public interface IPlayerIdentityService
    {
        IdentityState State { get; }
        PlayerIdentity Current { get; }
        Exception Error { get; }
        Task<PlayerIdentity> InitializeAsync();
    }
}
