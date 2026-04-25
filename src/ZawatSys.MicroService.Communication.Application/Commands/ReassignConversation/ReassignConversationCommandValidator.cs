using FluentValidation;
using ZawatSys.MicroLib.Communication.Domain;

namespace ZawatSys.MicroService.Communication.Application.Commands.ReassignConversation;

public sealed class ReassignConversationCommandValidator : AbstractValidator<ReassignConversationCommand>
{
    public ReassignConversationCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty();

        RuleFor(x => x.ExpectedControlVersion)
            .GreaterThan(0);

        RuleFor(x => x.AssigneeUserId)
            .NotEmpty();

        RuleFor(x => x.AssignedQueueCode)
            .MaximumLength(64)
            .When(x => !string.IsNullOrWhiteSpace(x.AssignedQueueCode));

        RuleFor(x => x.ReasonCode)
            .Must(reason => string.Equals(reason?.Trim(), ConversationControlReasonCodes.Reassign, StringComparison.Ordinal))
            .WithMessage("ReasonCode is required and must be a canonical TransitionReasonCode value.");
    }
}
