using System;

namespace Domino.Infrastructure.Api
{
    [Serializable] public sealed class PlayerBootstrapRequestDto { public string language; }
    [Serializable] public sealed class PlayerBootstrapResponseDto { public PlayerResponseDto player; public WalletResponseDto wallet; public EntitlementSummaryDto entitlements; }
    [Serializable] public sealed class EntitlementSummaryDto { public string availability; public EffectiveEntitlementsDto snapshot; public bool trialGranted; }
    [Serializable] public sealed class EffectiveEntitlementsDto {
        public string plan, status, validUntil, trialEndsAt, serverTime, nextTransitionAt;
        public string[] sources, features;
        public bool trialActive, trialConsumed;
        public long policyVersion, revision;
        public EntitlementLimitsDto limits;
    }
    [Serializable] public sealed class EntitlementLimitsDto { public EntitlementLimitDto FRIENDS_MAX, HISTORY_MAX, REPLAY_MAX; }
    [Serializable] public sealed class EntitlementLimitDto { public bool unlimited; public int maximum; }
    [Serializable] public sealed class PlayerResponseDto
    { public string uid; public string accountType; public string displayName; public string language; public string status; }
    [Serializable] public sealed class WalletResponseDto { public long coins = -1; }
    [Serializable] public sealed class ApiErrorDto { public string code; public string message; public string requestId; }
}
