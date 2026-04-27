using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFundamentals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fundamentals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ticker = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MarketCap = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    PeRatio = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    EpsAnnual = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    WeekHigh52 = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    WeekLow52 = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    DividendYield = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Sector = table.Column<string>(type: "text", nullable: true),
                    Industry = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fundamentals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fundamentals_stocks_Ticker",
                        column: x => x.Ticker,
                        principalTable: "stocks",
                        principalColumn: "Ticker",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fundamentals_Ticker_Date",
                table: "fundamentals",
                columns: new[] { "Ticker", "Date" },
                unique: true,
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fundamentals");
        }
    }
}
