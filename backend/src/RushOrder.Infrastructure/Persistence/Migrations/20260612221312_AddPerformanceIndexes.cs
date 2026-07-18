using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RushOrder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Orders: cross-tenant reporting / active-orders list
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_orders_tenant_restaurant_created " +
                "ON orders (\"TenantId\", \"RestaurantId\", \"CreatedAt\" DESC);");

            // Orders: kitchen display filtered by active status
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_orders_table_status_active " +
                "ON orders (\"TableId\", \"Status\") " +
                "WHERE \"Status\" NOT IN ('Paid','Cancelled');");

            // Products: menu listing sorted by sort_order
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_products_restaurant_available_sort " +
                "ON products (\"RestaurantId\", \"IsAvailable\", \"SortOrder\");");

            // Tables: floor-plan status view
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_tables_restaurant_status " +
                "ON tables (\"RestaurantId\", \"Status\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_orders_tenant_restaurant_created;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_orders_table_status_active;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_products_restaurant_available_sort;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_tables_restaurant_status;");
        }
    }
}
