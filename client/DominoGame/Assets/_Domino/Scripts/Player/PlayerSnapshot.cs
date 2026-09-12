using System;

namespace Domino.Player
{
    public enum PlayerAccountType { Guest, Registered }
    public enum PlayerStatus { Active }
    public sealed class PlayerSnapshot
    {
        public string Uid { get; }
        public PlayerAccountType AccountType { get; }
        public string DisplayName { get; }
        public string Language { get; }
        public PlayerStatus Status { get; }
        public PlayerSnapshot(string uid, PlayerAccountType accountType, string displayName, string language, PlayerStatus status)
        {
            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(displayName) ||
                (language != "en" && language != "es") || status != PlayerStatus.Active ||
                (accountType != PlayerAccountType.Guest && accountType != PlayerAccountType.Registered))
                throw new ArgumentException("Invalid player snapshot.");
            Uid = uid; AccountType = accountType; DisplayName = displayName; Language = language; Status = status;
        }
    }
}
