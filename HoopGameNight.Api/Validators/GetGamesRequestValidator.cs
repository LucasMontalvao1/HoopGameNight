using FluentValidation;
using HoopGameNight.Core.Constants;
using HoopGameNight.Core.DTOs.Request;

namespace HoopGameNight.Api.Validators
{
    public class GetGamesRequestValidator : AbstractValidator<GetGamesRequest>
    {
        public GetGamesRequestValidator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0)
                .WithMessage(ValidationMessages.Pagination.INVALID_PAGE);

            RuleFor(x => x.PageSize)
                .GreaterThan(0)
                .LessThanOrEqualTo(100)
                .WithMessage(ValidationMessages.Pagination.INVALID_PAGE_SIZE);

            RuleFor(x => x.StartDate)
                .LessThanOrEqualTo(x => x.EndDate)
                .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
                .WithMessage(ValidationMessages.Games.INVALID_DATE_RANGE);

            RuleFor(x => x.Season)
                .GreaterThan(2000)
                .When(x => x.Season.HasValue)
                .WithMessage(ValidationMessages.Games.INVALID_SEASON);
        }
    }
}