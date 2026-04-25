using FluentValidation;
using ZawatSys.MicroLib.Communication.Domain;

namespace ZawatSys.MicroService.Communication.Application.Commands.ResumeAIForConversation;

public sealed class ResumeAIForConversationCommandValidator : AbstractValidator<ResumeAIForConversationCommand>
{
    public ResumeAIForConversationCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty();

        RuleFor(x => x.ExpectedControlVersion)
            .GreaterThan(0);

        RuleFor(x => x.ReasonCode)
            .Must(reason => string.Equals(reason?.Trim(), ConversationControlReasonCodes.Resume, StringComparison.Ordinal))
            .WithMessage("ReasonCode is required and must be a canonical TransitionReasonCode value.");
    }
}
