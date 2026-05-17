using BookFinder.Api.Contracts;
using FluentValidation;

namespace BookFinder.Api.Validation;

public sealed class SearchBookRequestValidator : AbstractValidator<SearchBookRequest>
{
    public SearchBookRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Query must not be empty.")
            .MaximumLength(500).WithMessage("Query must not exceed 500 characters.");
    }
}
