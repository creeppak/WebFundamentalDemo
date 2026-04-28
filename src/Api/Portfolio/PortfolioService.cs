using Api.Mappers;
using Infrastructure.Data;
using Infrastructure.Domain;
using Microsoft.EntityFrameworkCore;
using Shared.Portfolio;

namespace Api.Portfolio;

public class PortfolioService(AppDbContext db, PortfolioMapper mapper)
{
    public async Task<PortfolioDto> GetPortfolioAsync(Guid userId, CancellationToken ct)
    {
        var transactions = await db.Transactions
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        var cashBalance = ComputeCashBalance(transactions);

        var holdingTickers = transactions
            .Where(t => t.Ticker is not null)
            .Select(t => t.Ticker!)
            .Distinct()
            .ToHashSet();

        if (holdingTickers.Count == 0)
            return new PortfolioDto([], cashBalance, cashBalance, null);

        var stocks = await db.Stocks
            .Where(s => holdingTickers.Contains(s.Ticker))
            .ToListAsync(ct);
        var stockByTicker = stocks.ToDictionary(s => s.Ticker);

        var latestPrices = await db.Prices
            .Where(p => holdingTickers.Contains(p.Ticker))
            .GroupBy(p => p.Ticker)
            .Select(g => g.OrderByDescending(p => p.Date).First())
            .ToListAsync(ct);
        var priceByTicker = latestPrices.ToDictionary(p => p.Ticker, p => p.Close);

        var holdings = BuildHoldings(transactions, holdingTickers, stockByTicker, priceByTicker);

        var allPricesAvailable = holdings.Count == 0 || holdings.All(h => h.MarketValue.HasValue);
        var totalMarketValue = allPricesAvailable
            ? cashBalance + holdings.Sum(h => h.MarketValue ?? 0m)
            : (decimal?)null;
        var totalUnrealizedPnL = allPricesAvailable
            ? holdings.Sum(h => h.UnrealizedPnL ?? 0m)
            : (decimal?)null;

        return new PortfolioDto(holdings, cashBalance, totalMarketValue, totalUnrealizedPnL);
    }

    public async Task<IReadOnlyList<TransactionDto>> GetTransactionsAsync(Guid userId, CancellationToken ct)
    {
        var transactions = await db.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return transactions.Select(mapper.ToTransactionDto).ToList();
    }

    public async Task<TradeResult> BuyAsync(Guid userId, string ticker, decimal quantity, CancellationToken ct)
    {
        ticker = ticker.ToUpperInvariant();

        var latestPrice = await db.Prices
            .Where(p => p.Ticker == ticker)
            .OrderByDescending(p => p.Date)
            .FirstOrDefaultAsync(ct);

        if (latestPrice is null)
            return TradeResult.Fail(TradeError.TickerNotFound);

        var totalCost = latestPrice.Close * quantity;
        var cashBalance = await ComputeCashBalanceFromDbAsync(userId, ct);

        if (cashBalance < totalCost)
            return TradeResult.Fail(TradeError.InsufficientFunds);

        await using var dbTx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TransactionType = TransactionType.Buy,
                Ticker = ticker,
                Price = latestPrice.Close,
                Quantity = quantity,
                CreatedAt = DateTime.UtcNow,
            });

            await db.SaveChangesAsync(ct);
            await dbTx.CommitAsync(ct);
        }
        catch
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }

        return TradeResult.Success(await GetPortfolioAsync(userId, ct));
    }

    public async Task<TradeResult> SellAsync(Guid userId, string ticker, decimal quantity, CancellationToken ct)
    {
        ticker = ticker.ToUpperInvariant();

        var latestPrice = await db.Prices
            .Where(p => p.Ticker == ticker)
            .OrderByDescending(p => p.Date)
            .FirstOrDefaultAsync(ct);

        if (latestPrice is null)
            return TradeResult.Fail(TradeError.TickerNotFound);

        var heldQuantity = await ComputeHeldQuantityAsync(userId, ticker, ct);

        if (heldQuantity < quantity)
            return TradeResult.Fail(TradeError.InsufficientHoldings);

        await using var dbTx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TransactionType = TransactionType.Sell,
                Ticker = ticker,
                Price = latestPrice.Close,
                Quantity = quantity,
                CreatedAt = DateTime.UtcNow,
            });

            await db.SaveChangesAsync(ct);
            await dbTx.CommitAsync(ct);
        }
        catch
        {
            await dbTx.RollbackAsync(ct);
            throw;
        }

        return TradeResult.Success(await GetPortfolioAsync(userId, ct));
    }

    private async Task<decimal> ComputeCashBalanceFromDbAsync(Guid userId, CancellationToken ct)
    {
        var transactions = await db.Transactions
            .Where(t => t.UserId == userId)
            .ToListAsync(ct);
        return ComputeCashBalance(transactions);
    }

    private async Task<decimal> ComputeHeldQuantityAsync(Guid userId, string ticker, CancellationToken ct)
    {
        var buys = await db.Transactions
            .Where(t => t.UserId == userId && t.Ticker == ticker && t.TransactionType == TransactionType.Buy)
            .SumAsync(t => t.Quantity, ct);

        var sells = await db.Transactions
            .Where(t => t.UserId == userId && t.Ticker == ticker && t.TransactionType == TransactionType.Sell)
            .SumAsync(t => t.Quantity, ct);

        return buys - sells;
    }

    private static decimal ComputeCashBalance(IEnumerable<Transaction> transactions) =>
        transactions.Sum(t => t.TransactionType switch
        {
            TransactionType.Deposit => t.Quantity,
            TransactionType.Buy => -(t.Price * t.Quantity),
            TransactionType.Sell => t.Price * t.Quantity,
            _ => 0m
        });

    private static List<HoldingDto> BuildHoldings(
        IEnumerable<Transaction> transactions,
        HashSet<string> holdingTickers,
        Dictionary<string, Stock> stockByTicker,
        Dictionary<string, decimal> priceByTicker)
    {
        var buysByTicker = transactions
            .Where(t => t.TransactionType == TransactionType.Buy && t.Ticker is not null)
            .GroupBy(t => t.Ticker!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sellsByTicker = transactions
            .Where(t => t.TransactionType == TransactionType.Sell && t.Ticker is not null)
            .GroupBy(t => t.Ticker!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var holdings = new List<HoldingDto>();

        foreach (var ticker in holdingTickers)
        {
            if (!buysByTicker.TryGetValue(ticker, out var buys))
                continue;

            var sells = sellsByTicker.GetValueOrDefault(ticker, []);
            var totalBought = buys.Sum(t => t.Quantity);
            var netQuantity = totalBought - sells.Sum(t => t.Quantity);

            if (netQuantity <= 0)
                continue;

            var avgCostBasis = totalBought > 0 ? buys.Sum(t => t.Price * t.Quantity) / totalBought : 0m;

            var currentPrice = priceByTicker.TryGetValue(ticker, out var price) ? price : (decimal?)null;
            var marketValue = currentPrice.HasValue ? currentPrice.Value * netQuantity : (decimal?)null;
            var unrealizedPnL = currentPrice.HasValue ? (currentPrice.Value - avgCostBasis) * netQuantity : (decimal?)null;
            var unrealizedPnLPercent = avgCostBasis != 0 && currentPrice.HasValue
                ? Math.Round((currentPrice.Value - avgCostBasis) / avgCostBasis * 100, 2)
                : (decimal?)null;

            var companyName = stockByTicker.TryGetValue(ticker, out var stock) ? stock.CompanyName : ticker;

            holdings.Add(new HoldingDto(
                ticker,
                companyName,
                netQuantity,
                avgCostBasis,
                currentPrice,
                marketValue,
                unrealizedPnL,
                unrealizedPnLPercent));
        }

        return holdings;
    }
}
