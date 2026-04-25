using FluentValidation;
using ZawatSys.MicroLib.Communication.Domain;

namespace ZawatSys.MicroService.Communication.Application.Commands.ReleaseConversation;

public sealed class ReleaseConversationCommandValidator : AbstractValidator<ReleaseConversationCommand>
{
    public ReleaseConversationCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty();

        RuleFor(x => x.ExpectedControlVersion)
            .GreaterThan(0);

        RuleFor(x => x.ReasonCode)
            .Must(reason => string.Equals(reason?.Trim(), ConversationControlReasonCodes.Release, StringComparison.Ordinal))
            .WithMessage("ReasonCode is required and must be a canonical TransitionReasonCode value.");
    }
}
