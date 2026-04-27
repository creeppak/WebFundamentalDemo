using Infrastructure.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<User, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Price> Prices => Set<Price>();
    public DbSet<Fundamental> Fundamentals => Set<Fundamental>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);  // sets up Identity tables first
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}