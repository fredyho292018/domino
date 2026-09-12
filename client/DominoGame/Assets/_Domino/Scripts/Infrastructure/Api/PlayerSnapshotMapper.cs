using Domino.Player;

namespace Domino.Infrastructure.Api
{
    internal static class PlayerSnapshotMapper
    {
        internal static PlayerSnapshot Player(PlayerResponseDto dto)
        {
            if (dto == null || (dto.accountType != "GUEST" && dto.accountType != "REGISTERED") || dto.status != "ACTIVE")
                throw new DominoApiException(ApiFailure.Contract);
            try { return new PlayerSnapshot(dto.uid, dto.accountType == "GUEST" ? PlayerAccountType.Guest : PlayerAccountType.Registered,
                dto.displayName, dto.language, PlayerStatus.Active); }
            catch { throw new DominoApiException(ApiFailure.Contract); }
        }
        internal static WalletSnapshot Wallet(WalletResponseDto dto)
        {
            if (dto == null) throw new DominoApiException(ApiFailure.Contract);
            try { return new WalletSnapshot(dto.coins); }
            catch { throw new DominoApiException(ApiFailure.Contract); }
        }
    }
}
