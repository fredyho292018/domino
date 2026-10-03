using Domino.Player;
using System;
using System.Globalization;

namespace Domino.Infrastructure.Api
{
    internal static class PlayerSnapshotMapper
    {
        internal static PlayerSnapshot Player(PlayerResponseDto dto)
        {
            if (dto == null || (dto.accountType != "GUEST" && dto.accountType != "REGISTERED") || dto.status != "ACTIVE")
                throw new DominoApiException(ApiFailure.Contract);
            try { return new PlayerSnapshot(dto.uid, dto.accountType == "GUEST" ? PlayerAccountType.Guest : PlayerAccountType.Registered,
                dto.displayName, dto.language, PlayerStatus.Active, CreatedAt(dto.createdAt)); }
            catch { throw new DominoApiException(ApiFailure.Contract); }
        }
        internal static DateTimeOffset? CreatedAt(string value)
        {
            if(value==null)return null; // Older backend / historical Player.
            // Firestore supports nanoseconds; DateTimeOffset supports seven decimal places.
            // Truncate excess precision so rounding cannot move Joined into the next UTC day.
            value=System.Text.RegularExpressions.Regex.Replace(value,@"(\.\d{7})\d{1,2}(?=Z|[+-])","$1");
            // Require a wire instant with explicit offset; never infer this machine's local time zone.
            if(!System.Text.RegularExpressions.Regex.IsMatch(value,@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,9})?(?:Z|[+-]\d{2}:\d{2})$") ||
                !DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out var result))
                return null; // Invalid optional presentation field cannot manufacture a Joined date.
            return result;
        }
        internal static WalletSnapshot Wallet(WalletResponseDto dto)
        {
            if (dto == null) throw new DominoApiException(ApiFailure.Contract);
            try { return new WalletSnapshot(dto.coins); }
            catch { throw new DominoApiException(ApiFailure.Contract); }
        }
    }
}
