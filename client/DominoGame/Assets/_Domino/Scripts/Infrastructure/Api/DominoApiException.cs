using System;

namespace Domino.Infrastructure.Api
{
    public enum ApiFailure { Configuration, Authentication, Transport, Timeout, Cancelled, Contract, Server }
    public sealed class DominoApiException : Exception
    {
        public long HttpStatus { get; }
        public string ServerErrorCode { get; }
        public string RequestId { get; }
        public ApiFailure Category { get; }
        public DominoApiException(ApiFailure category, long httpStatus = 0, string serverErrorCode = null, string requestId = null)
            : base("Player bootstrap failed: " + category)
        { Category = category; HttpStatus = httpStatus; ServerErrorCode = serverErrorCode; RequestId = requestId; }
    }
}
