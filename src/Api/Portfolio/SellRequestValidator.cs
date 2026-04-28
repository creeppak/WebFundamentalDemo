using FluentValidation;
using Shared.Portfolio;

namespace Api.Portfolio;

public class SellRequestValidator : AbstractValidator<SellRequest>
{
    public SellRequestValidator()
    {
        RuleFor(x => x.Ticker).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
