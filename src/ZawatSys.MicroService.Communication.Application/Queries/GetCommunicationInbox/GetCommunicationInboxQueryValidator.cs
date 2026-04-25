using FluentValidation;

namespace ZawatSys.MicroService.Communication.Application.Queries.GetCommunicationInbox;

public sealed class GetCommunicationInboxQueryValidator : AbstractValidator<GetCommunicationInboxQuery>
{
    public GetCommunicationInboxQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 200);

        RuleFor(x => x.Search)
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Search));
    }
}
