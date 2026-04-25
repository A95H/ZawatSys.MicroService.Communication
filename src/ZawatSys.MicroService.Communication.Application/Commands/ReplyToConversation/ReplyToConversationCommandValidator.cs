using FluentValidation;

namespace ZawatSys.MicroService.Communication.Application.Commands.ReplyToConversation;

public sealed class ReplyToConversationCommandValidator : AbstractValidator<ReplyToConversationCommand>
{
    public ReplyToConversationCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty();

        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(4000);

        RuleFor(x => x.ReplyToMessageId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("ReplyToMessageId cannot be empty when provided.");
    }
}
