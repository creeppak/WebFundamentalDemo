using System.Security.Claims;
using Api.Portfolio;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Portfolio;

namespace Api.Controllers;

[ApiController]
[Route("api/portfolio")]
[Authorize]
public class PortfolioController(
    PortfolioService portfolioService,
    IValidator<BuyRequest> buyValidator,
    IValidator<SellRequest> sellValidator) : ControllerBase
{
    /// <summary>Get the current user's portfolio: holdings, cash balance, and unrealized P&amp;L.</summary>
    /// <response code="200">Portfolio summary derived from transaction history.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PortfolioDto>> GetPortfolio(CancellationToken ct) =>
        Ok(await portfolioService.GetPortfolioAsync(GetUserId(), ct));

    /// <summary>Get the current user's transaction history, newest first.</summary>
    /// <response code="200">List of transactions (deposits, buys, sells).</response>
    [HttpGet("transactions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> GetTransactions(CancellationToken ct) =>
        Ok(await portfolioService.GetTransactionsAsync(GetUserId(), ct));

    /// <summary>Buy shares of a stock at the most recent available price.</summary>
    /// <response code="200">Updated portfolio state after the purchase.</response>
    /// <response code="400">Invalid request (missing ticker, non-positive quantity).</response>
    /// <response code="404">Ticker not found in tracked stock list.</response>
    /// <response code="422">Insufficient cash balance.</response>
    [HttpPost("buy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PortfolioDto>> Buy([FromBody] BuyRequest request, CancellationToken ct)
    {
        var validation = await buyValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem();
        }

        var result = await portfolioService.BuyAsync(GetUserId(), request.Ticker, request.Quantity, ct);
        return result.Error switch
        {
            TradeError.TickerNotFound => Problem("Ticker not found.", statusCode: StatusCodes.Status404NotFound),
            TradeError.InsufficientFunds => Problem("Insufficient funds.", statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Ok(result.Portfolio),
        };
    }

    /// <summary>Sell shares of a held stock at the most recent available price.</summary>
    /// <response code="200">Updated portfolio state after the sale.</response>
    /// <response code="400">Invalid request (missing ticker, non-positive quantity).</response>
    /// <response code="404">Ticker not found in tracked stock list.</response>
    /// <response code="422">Insufficient holdings — cannot sell more than you own.</response>
    [HttpPost("sell")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PortfolioDto>> Sell([FromBody] SellRequest request, CancellationToken ct)
    {
        var validation = await sellValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem();
        }

        var result = await portfolioService.SellAsync(GetUserId(), request.Ticker, request.Quantity, ct);
        return result.Error switch
        {
            TradeError.TickerNotFound => Problem("Ticker not found.", statusCode: StatusCodes.Status404NotFound),
            TradeError.InsufficientHoldings => Problem("Insufficient holdings.", statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Ok(result.Portfolio),
        };
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("User ID claim not found.");
        return Guid.Parse(value);
    }
}
