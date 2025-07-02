using FluentValidation;
using HoopGameNight.Core.Constants;
using HoopGameNight.Core.DTOs.Request;

namespace HoopGameNight.Api.Validators
{
    public class SearchPlayerRequestValidator : AbstractValidator<SearchPlayerRequest>
    {
        public SearchPlayerRequestValidator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0)
                .WithMessage(ValidationMessages.Pagination.INVALID_PAGE);

            RuleFor(x => x.PageSize)
                .GreaterThan(0)
                .LessThanOrEqualTo(100)
                .WithMessage(ValidationMessages.Pagination.INVALID_PAGE_SIZE);

            RuleFor(x => x.Search)
                .MinimumLength(2)
                .When(x => !string.IsNullOrWhiteSpace(x.Search))
                .WithMessage(ValidationMessages.Players.SEARCH_TOO_SHORT);

            RuleFor(x => x)
                .Must(HaveAtLeastOneSearchCriteria)
                .WithMessage(ValidationMessages.Players.SEARCH_CRITERIA_REQUIRED);
        }

        private static bool HaveAtLeastOneSearchCriteria(SearchPlayerRequest request)
        {
            return !string.IsNullOrWhiteSpace(request.Search) ||
                   request.TeamId.HasValue ||
                   !string.IsNullOrWhiteSpace(request.Position);
        }
    }
}