using FluentValidation;
using ZawatSys.MicroLib.Communication.Licensing;
using ZawatSys.MicroLib.Communication.MessageCodes;
using ZawatSys.MicroLib.Shared.Common;
using ZawatSys.MicroService.Communication.Application.Webhooks;

namespace ZawatSys.MicroService.Communication.Api.Exceptions;

/// <summary>
/// Maps known exception families to stable HTTP status + MsgCode.
/// </summary>
public sealed class DeterministicExceptionMapper : IExceptionMapper
{
    private readonly IReadOnlyList<Func<Exception, ExceptionMappingResult?>> _rules;

    public DeterministicExceptionMapper()
    {
        _rules = new List<Func<Exception, ExceptionMappingResult?>>
        {
            TryMapValidation,
            TryMapNotFound,
            TryMapReplayRejected,
            TryMapStaleControlConflict,
            TryMapIdempotencyConflict,
            TryMapConflict,
            TryMapUnauthorized
        };
    }

    public ExceptionMappingResult Map(Exception exception)
    {
        foreach (var rule in _rules)
        {
            var mapped = rule(exception);
            if (mapped is not null)
            {
                return mapped;
            }
        }

        return new ExceptionMappingResult(
            StatusCode: StatusCodes.Status500InternalServerError,
            MsgCode: MessageCodes.INTERNAL_ERROR,
            Title: "Internal Server Error",
            Details: "An unexpected error occurred.",
            Errors: Array.Empty<string>());
    }

    private static ExceptionMappingResult? TryMapValidation(Exception exception)
    {
        if (exception is ValidationException validationException)
        {
            return new ExceptionMappingResult(
                StatusCode: StatusCodes.Status400BadRequest,
                MsgCode: MessageCodes.VALIDATION_FAILED,
                Title: "Validation Failed",
                Details: "One or more validation errors occurred.",
                Errors: validationException.Errors
                    .Select(error => $"{error.PropertyName}: {error.ErrorMessage}")
                    .ToArray());
        }

        if (exception is ArgumentException or FormatException)
        {
            return new ExceptionMappingResult(
                StatusCode: StatusCodes.Status400BadRequest,
                MsgCode: MessageCodes.VALIDATION_FAILED,
                Title: "Validation Failed",
                Details: exception.Message,
                Errors: Array.Empty<string>());
        }

        if (exception.GetType().Name.Contains("Validation", StringComparison.OrdinalIgnoreCase))
        {
            return new ExceptionMappingResult(
                StatusCode: StatusCodes.Status400BadRequest,
                MsgCode: MessageCodes.VALIDATION_FAILED,
                Title: "Validation Failed",
                Details: exception.Message,
                Errors: Array.Empty<string>());
        }

        return null;
    }

    private static ExceptionMappingResult? TryMapNotFound(Exception exception)
    {
        if (exception is KeyNotFoundException || exception.GetType().Name.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
        {
            return new ExceptionMappingResult(
                StatusCode: StatusCodes.Status404NotFound,
                MsgCode: MessageCodes.NOT_FOUND,
                Title: "Not Found",
                Details: exception.Message,
                Errors: Array.Empty<string>());
        }

        return null;
    }

    private static ExceptionMappingResult? TryMapConflict(Exception exception)
    {
        if (exception.GetType().Name.Contains("Conflict", StringComparison.OrdinalIgnoreCase)
            || exception is InvalidOperationException)
        {
            return new ExceptionMappingResult(
                StatusCode: StatusCodes.Status409Conflict,
                MsgCode: MessageCodes.CONFLICT,
                Title: "Conflict",
                Details: exception.Message,
                Errors: Array.Empty<string>());
        }

        return null;
    }

    private static ExceptionMappingResult? TryMapStaleControlConflict(Exception exception)
    {
        if (exception is StaleControlConflictException
            || string.Equals(exception.GetType().Name, "StaleControlConflictException", StringComparison.Ordinal))
        {
            return new ExceptionMappingResult(
                StatusCode: StatusCodes.Status409Conflict,
                MsgCode: CommunicationMessageCodes.STALE_CONTROL_CONFLICT,
                Title: "Stale Control Conflict",
                Details: exception.Message,
                Errors: Array.Empty<string>());
        }

        return null;
    }

    private static ExceptionMappingResult? TryMapReplayRejected(Exception exception)
    {
        if (exception is ProviderWebhookReplayRejectedException
            || string.Equals(exception.GetType().Name, nameof(ProviderWebhookReplayRejectedException), StringComparison.Ordinal))
        {
            return new ExceptionMappingResult(
                StatusCode: StatusCodes.Status409Conflict,
                MsgCode: CommunicationMessageCodes.REPLAY_REJECTED,
                Title: "Replay Rejected",
                Details: exception.Message,
                Errors: Array.Empty<string>());
        }

        return null;
    }

    private static ExceptionMappingResult? TryMapIdempotencyConflict(Exception exception)
    {
        if (exception is IdempotencyConflictException)
        {
            return new ExceptionMappingResult(
                StatusCode: StatusCodes.Status409Conflict,
                MsgCode: CommunicationMessageCodes.IDEMPOTENCY_CONFLICT,
                Title: "Idempotency Conflict",
                Details: exception.Message,
                Errors: Array.Empty<string>());
        }

        return null;
    }

    private static ExceptionMappingResult? TryMapUnauthorized(Exception exception)
    {
        if (exception is UnauthorizedAccessException or LicenseException)
        {
            return new ExceptionMappingResult(
                StatusCode: StatusCodes.Status401Unauthorized,
                MsgCode: CommunicationMessageCodes.UNAUTHORIZED,
                Title: "Unauthorized",
                Details: exception.Message,
                Errors: Array.Empty<string>());
        }

        return null;
    }
}
