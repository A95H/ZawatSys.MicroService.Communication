using FluentValidation;

namespace ZawatSys.MicroService.Communication.Application.Queries.GetConversationDetails;

public sealed class GetConversationDetailsQueryValidator : AbstractValidator<GetConversationDetailsQuery>
{
    public GetConversationDetailsQueryValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty();
    }
}
