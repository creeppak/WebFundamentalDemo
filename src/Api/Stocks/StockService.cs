using Api.Mappers;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Stocks;

namespace Api.Stocks;

public class StockService(AppDbContext db, StockMapper mapper)
{
    public async Task<IReadOnlyList<StockSummaryDto>> GetAllAsync(CancellationToken ct)
    {
        var stocks = await db.Stocks.OrderBy(s => s.Ticker).ToListAsync(ct);
        var tickers = stocks.Select(s => s.Ticker).ToList();

        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);
        var recentPrices = await db.Prices
            .Where(p => tickers.Contains(p.Ticker) && p.Date >= cutoff)
            .ToListAsync(ct);

        var pricesByTicker = recentPrices
            .GroupBy(p => p.Ticker)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Date).ToList());

        return stocks.Select(s =>
        {
            var prices = pricesByTicker.GetValueOrDefault(s.Ticker, []);
            var latest = prices.Count > 0 ? prices[0] : null;
            var previous = prices.Count > 1 ? prices[1] : null;
            var dayChange = latest is not null && previous is not null && previous.Close != 0
                ? Math.Round((latest.Close - previous.Close) / previous.Close * 100, 2)
                : (decimal?)null;

            return new StockSummaryDto(s.Ticker, s.CompanyName, latest?.Close, dayChange);
        }).ToList();
    }

    public async Task<StockDetailDto?> GetByTickerAsync(string ticker, CancellationToken ct)
    {
        var stock = await db.Stocks.SingleOrDefaultAsync(s => s.Ticker == ticker, ct);
        if (stock is null) return null;

        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);
        var recentPrices = await db.Prices
            .Where(p => p.Ticker == ticker && p.Date >= cutoff)
            .OrderByDescending(p => p.Date)
            .Take(2)
            .ToListAsync(ct);

        var latest = recentPrices.Count > 0 ? recentPrices[0] : null;
        var previous = recentPrices.Count > 1 ? recentPrices[1] : null;
        var dayChange = latest is not null && previous is not null && previous.Close != 0
            ? Math.Round((latest.Close - previous.Close) / previous.Close * 100, 2)
            : (decimal?)null;

        var fundamentals = await db.Fundamentals
            .Where(f => f.Ticker == ticker)
            .OrderByDescending(f => f.Date)
            .FirstOrDefaultAsync(ct);

        var analysis = await db.Analyses
            .Where(a => a.Ticker == ticker)
            .OrderByDescending(a => a.Date)
            .FirstOrDefaultAsync(ct);

        return new StockDetailDto(
            stock.Ticker,
            stock.CompanyName,
            latest?.Close,
            dayChange,
            fundamentals is not null ? mapper.ToFundamentalsDto(fundamentals) : null,
            analysis is not null ? mapper.ToAnalysisDto(analysis) : null);
    }

    public async Task<IReadOnlyList<PricePointDto>> GetHistoryAsync(string ticker, int days, CancellationToken ct)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-days);
        var prices = await db.Prices
            .Where(p => p.Ticker == ticker && p.Date >= cutoff)
            .OrderBy(p => p.Date)
            .ToListAsync(ct);

        return prices.Select(mapper.ToPricePointDto).ToList();
    }

    public async Task<IReadOnlyList<NewsArticleDto>> GetNewsAsync(string ticker, CancellationToken ct)
    {
        var articles = await db.NewsArticles
            .Where(a => a.Ticker == ticker)
            .OrderByDescending(a => a.PublishedAt)
            .ToListAsync(ct);

        return articles.Select(mapper.ToNewsArticleDto).ToList();
    }
}
