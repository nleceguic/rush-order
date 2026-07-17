using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RushOrder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandForecastingAndKitchenTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_status_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_status_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "order_ratings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodRating = table.Column<int>(type: "integer", nullable: false),
                    SpeedRating = table.Column<int>(type: "integer", nullable: false),
                    ServiceRating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_ratings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "demand_forecasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ForecastDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ForecastHour = table.Column<int>(type: "integer", nullable: false),
                    PredictedQuantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    ConfidenceLevel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demand_forecasts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_order_changed",
                table: "order_status_history",
                columns: new[] { "OrderId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_lookup",
                table: "order_status_history",
                columns: new[] { "TenantId", "RestaurantId", "ToStatus", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_order_ratings_order_unique",
                table: "order_ratings",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_ratings_lookup",
                table: "order_ratings",
                columns: new[] { "TenantId", "RestaurantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_demand_forecasts_lookup",
                table: "demand_forecasts",
                columns: new[] { "TenantId", "RestaurantId", "ForecastDate", "ProductId", "ForecastHour" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_status_history");

            migrationBuilder.DropTable(
                name: "order_ratings");

            migrationBuilder.DropTable(
                name: "demand_forecasts");
        }
    }
}
