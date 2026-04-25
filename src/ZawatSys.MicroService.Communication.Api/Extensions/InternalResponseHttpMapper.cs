using ZawatSys.MicroLib.Communication.MessageCodes;
using ZawatSys.MicroLib.Shared.Common;

namespace ZawatSys.MicroService.Communication.Api.Extensions;

/// <summary>
/// Deterministic MsgCode -> HTTP status mapper.
/// </summary>
public static class InternalResponseHttpMapper
{
    public static int ToStatusCode(string? msgCode)
    {
        if (string.IsNullOrWhiteSpace(msgCode))
        {
            return StatusCodes.Status500InternalServerError;
        }

        return msgCode switch
        {
            MessageCodes.VALIDATION_FAILED => StatusCodes.Status400BadRequest,
            MessageCodes.NOT_FOUND => StatusCodes.Status404NotFound,
            MessageCodes.CONFLICT => StatusCodes.Status409Conflict,
            MessageCodes.INTERNAL_ERROR => StatusCodes.Status500InternalServerError,

            CommunicationMessageCodes.STALE_CONTROL_CONFLICT => StatusCodes.Status409Conflict,
            CommunicationMessageCodes.IDEMPOTENCY_CONFLICT => StatusCodes.Status409Conflict,
            CommunicationMessageCodes.REPLAY_REJECTED => StatusCodes.Status409Conflict,
            CommunicationMessageCodes.THROTTLED => StatusCodes.Status429TooManyRequests,
            CommunicationMessageCodes.UNAUTHORIZED => StatusCodes.Status401Unauthorized,

            _ when msgCode.Contains("VALIDATION", StringComparison.OrdinalIgnoreCase)
                => StatusCodes.Status400BadRequest,
            _ when msgCode.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase)
                => StatusCodes.Status404NotFound,
            _ when msgCode.Contains("STALE_CONTROL", StringComparison.OrdinalIgnoreCase)
                => StatusCodes.Status409Conflict,
            _ when msgCode.Contains("IDEMPOTENCY", StringComparison.OrdinalIgnoreCase)
                => StatusCodes.Status409Conflict,
            _ when msgCode.Contains("REPLAY", StringComparison.OrdinalIgnoreCase)
                => StatusCodes.Status409Conflict,
            _ when msgCode.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase)
                => StatusCodes.Status409Conflict,
            _ when msgCode.Contains("THROTTLED", StringComparison.OrdinalIgnoreCase)
                => StatusCodes.Status429TooManyRequests,
            _ when msgCode.Contains("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase)
                => StatusCodes.Status401Unauthorized,

            _ => StatusCodes.Status500InternalServerError
        };
    }
}
