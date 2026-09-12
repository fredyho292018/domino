using System;

namespace Domino.Infrastructure.Api
{
    [Serializable] public sealed class PlayerBootstrapRequestDto { public string language; }
    [Serializable] public sealed class PlayerBootstrapResponseDto { public PlayerResponseDto player; public WalletResponseDto wallet; }
    [Serializable] public sealed class PlayerResponseDto
    { public string uid; public string accountType; public string displayName; public string language; public string status; }
    [Serializable] public sealed class WalletResponseDto { public long coins = -1; }
    [Serializable] public sealed class ApiErrorDto { public string code; public string message; public string requestId; }
}
