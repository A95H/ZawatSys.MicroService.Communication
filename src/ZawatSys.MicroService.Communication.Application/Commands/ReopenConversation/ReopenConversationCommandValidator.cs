using FluentValidation;
using ZawatSys.MicroLib.Communication.Domain;

namespace ZawatSys.MicroService.Communication.Application.Commands.ReopenConversation;

public sealed class ReopenConversationCommandValidator : AbstractValidator<ReopenConversationCommand>
{
    public ReopenConversationCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty();

        RuleFor(x => x.ExpectedControlVersion)
            .GreaterThan(0);

        RuleFor(x => x.ReasonCode)
            .Must(reason => string.Equals(reason?.Trim(), ConversationControlReasonCodes.Reopen, StringComparison.Ordinal))
            .WithMessage("ReasonCode is required and must be a canonical TransitionReasonCode value.");
    }
}
