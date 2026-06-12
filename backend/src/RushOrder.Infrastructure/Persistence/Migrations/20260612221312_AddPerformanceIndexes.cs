using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RushOrder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        // CREATE INDEX CONCURRENTLY cannot run inside a transaction.
        // Each Sql() call uses suppressTransaction: true so EF does not wrap it.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Orders: cross-tenant reporting / active-orders list
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_orders_tenant_restaurant_created " +
                "ON orders (tenant_id, restaurant_id, created_at DESC);",
                suppressTransaction: true);

            // Orders: kitchen display filtered by active status
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_orders_table_status_active " +
                "ON orders (table_id, status) " +
                "WHERE status NOT IN ('Paid','Cancelled');",
                suppressTransaction: true);

            // Products: menu listing sorted by sort_order
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_products_restaurant_available_sort " +
                "ON products (restaurant_id, is_available, sort_order);",
                suppressTransaction: true);

            // Tables: floor-plan status view
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_tables_restaurant_status " +
                "ON tables (restaurant_id, status);",
                suppressTransaction: true);
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
