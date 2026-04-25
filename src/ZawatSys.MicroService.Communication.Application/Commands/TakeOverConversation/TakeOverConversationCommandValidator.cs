using FluentValidation;

namespace ZawatSys.MicroService.Communication.Application.Commands.TakeOverConversation;

public sealed class TakeOverConversationCommandValidator : AbstractValidator<TakeOverConversationCommand>
{
    public TakeOverConversationCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty();

        RuleFor(x => x.ExpectedControlVersion)
            .GreaterThan(0);

        RuleFor(x => x.AssigneeUserId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("AssigneeUserId cannot be empty when provided.");

        RuleFor(x => x.AssignedQueueCode)
            .MaximumLength(64)
            .When(x => !string.IsNullOrWhiteSpace(x.AssignedQueueCode));
    }
}
