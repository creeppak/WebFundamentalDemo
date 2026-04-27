using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStocksAndPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stocks",
                columns: table => new
                {
                    Ticker = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stocks", x => x.Ticker);
                });

            migrationBuilder.CreateTable(
                name: "prices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ticker = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Open = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    High = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Low = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Close = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prices_stocks_Ticker",
                        column: x => x.Ticker,
                        principalTable: "stocks",
                        principalColumn: "Ticker",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "stocks",
                columns: new[] { "Ticker", "CompanyName" },
                values: new object[,]
                {
                    { "AAPL", "Apple Inc." },
                    { "AMZN", "Amazon.com Inc." },
                    { "GOOGL", "Alphabet Inc." },
                    { "JPM", "JPMorgan Chase & Co." },
                    { "META", "Meta Platforms Inc." },
                    { "MSFT", "Microsoft Corporation" },
                    { "NVDA", "NVIDIA Corporation" },
                    { "TSLA", "Tesla Inc." }
                });

            migrationBuilder.CreateIndex(
                name: "IX_prices_Ticker_Date",
                table: "prices",
                columns: new[] { "Ticker", "Date" },
                unique: true,
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prices");

            migrationBuilder.DropTable(
                name: "stocks");
        }
    }
}
