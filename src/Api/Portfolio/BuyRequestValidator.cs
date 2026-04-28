using FluentValidation;
using Shared.Portfolio;

namespace Api.Portfolio;

public class BuyRequestValidator : AbstractValidator<BuyRequest>
{
    public BuyRequestValidator()
    {
        RuleFor(x => x.Ticker).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}