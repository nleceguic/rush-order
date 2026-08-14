using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RushOrder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameOrderTrackingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_order_status_history_order_changed",
                table: "order_status_history",
                newName: "IX_order_status_history_OrderId_ChangedAt");

            migrationBuilder.RenameIndex(
                name: "IX_order_status_history_lookup",
                table: "order_status_history",
                newName: "IX_order_status_history_TenantId_RestaurantId_ToStatus_Changed~");

            migrationBuilder.RenameIndex(
                name: "IX_order_ratings_order_unique",
                table: "order_ratings",
                newName: "IX_order_ratings_OrderId");

            migrationBuilder.RenameIndex(
                name: "IX_order_ratings_lookup",
                table: "order_ratings",
                newName: "IX_order_ratings_TenantId_RestaurantId_CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_order_ratings_TenantId_RestaurantId_CreatedAt",
                table: "order_ratings",
                newName: "IX_order_ratings_lookup");

            migrationBuilder.RenameIndex(
                name: "IX_order_ratings_OrderId",
                table: "order_ratings",
                newName: "IX_order_ratings_order_unique");

            migrationBuilder.RenameIndex(
                name: "IX_order_status_history_TenantId_RestaurantId_ToStatus_Changed~",
                table: "order_status_history",
                newName: "IX_order_status_history_lookup");

            migrationBuilder.RenameIndex(
                name: "IX_order_status_history_OrderId_ChangedAt",
                table: "order_status_history",
                newName: "IX_order_status_history_order_changed");
        }
    }
}
