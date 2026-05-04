using Api.Stocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Stocks;

namespace Api.Controllers;

[ApiController]
[Route("api/stocks")]
[Authorize]
public class StocksController(StockService stockService) : ControllerBase
{
    /// <summary>List all tracked stocks with their latest closing price and day-over-day change.</summary>
    /// <response code="200">Array of stock summaries, ordered by ticker.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StockSummaryDto>>> GetAll(CancellationToken ct) =>
        Ok(await stockService.GetAllAsync(ct));

    /// <summary>Get fundamentals, latest AI analysis, and latest price for a single stock.</summary>
    /// <param name="ticker">Stock ticker symbol, e.g. AAPL.</param>
    /// <response code="200">Stock detail including fundamentals and AI-generated analysis.</response>
    /// <response code="404">Ticker not found in the tracked stock list.</response>
    [HttpGet("{ticker}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockDetailDto>> GetByTicker(string ticker, CancellationToken ct)
    {
        var result = await stockService.GetByTickerAsync(ticker.ToUpperInvariant(), ct);
        return result is not null ? Ok(result) : NotFound();
    }

    /// <summary>Get daily close price and volume for the last N calendar days, ordered oldest-first.</summary>
    /// <param name="ticker">Stock ticker symbol, e.g. AAPL.</param>
    /// <param name="days">Number of calendar days to look back (1–90). Defaults to 14.</param>
    /// <response code="200">Array of price points (date, close, volume) suitable for charting.</response>
    /// <response code="400">days is outside the 1–90 range.</response>
    [HttpGet("{ticker}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PricePointDto>>> GetHistory(
        string ticker,
        [FromQuery] int days = 14,
        CancellationToken ct = default)
    {
        if (days is < 1 or > 90)
            return BadRequest("days must be between 1 and 90.");

        return Ok(await stockService.GetHistoryAsync(ticker.ToUpperInvariant(), days, ct));
    }

    /// <summary>Get recent news headlines for a stock, ordered newest-first.</summary>
    /// <param name="ticker">Stock ticker symbol, e.g. AAPL.</param>
    /// <response code="200">Array of news articles with headline, URL, source, and publish time.</response>
    [HttpGet("{ticker}/news")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NewsArticleDto>>> GetNews(string ticker, CancellationToken ct) =>
        Ok(await stockService.GetNewsAsync(ticker.ToUpperInvariant(), ct));
}