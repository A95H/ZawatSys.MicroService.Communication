using FluentValidation;
using ZawatSys.MicroLib.Communication.Domain;

namespace ZawatSys.MicroService.Communication.Application.Commands.PauseAIForConversation;

public sealed class PauseAIForConversationCommandValidator : AbstractValidator<PauseAIForConversationCommand>
{
    public PauseAIForConversationCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty();

        RuleFor(x => x.ExpectedControlVersion)
            .GreaterThan(0);

        RuleFor(x => x.ReasonCode)
            .Must(ConversationControlReasonCodes.IsAllowedPauseReason)
            .WithMessage("ReasonCode is required and must be a canonical TransitionReasonCode value.");
    }
}
