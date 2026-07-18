using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RushOrder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the application role (noop if it already exists)
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'rushorder_app') THEN
    CREATE ROLE rushorder_app LOGIN PASSWORD 'change_me_in_production' NOSUPERUSER NOCREATEDB NOCREATEROLE;
  END IF;
END
$$;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO rushorder_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO rushorder_app;
");

            // Enable RLS on every tenant-scoped table
            migrationBuilder.Sql(@"
ALTER TABLE restaurants ENABLE ROW LEVEL SECURITY;
ALTER TABLE tables      ENABLE ROW LEVEL SECURITY;
ALTER TABLE users       ENABLE ROW LEVEL SECURITY;
ALTER TABLE customers   ENABLE ROW LEVEL SECURITY;
ALTER TABLE orders      ENABLE ROW LEVEL SECURITY;
ALTER TABLE products    ENABLE ROW LEVEL SECURITY;
ALTER TABLE payments    ENABLE ROW LEVEL SECURITY;
");

            // Create isolation policies.
            // The app sets app.current_tenant_id via TenantDbCommandInterceptor before each command.
            // The 'true' flag in current_setting makes it return NULL (not an error) when unset,
            // which causes the filter to reject all rows — safe default.
            migrationBuilder.Sql(@"
CREATE POLICY tenant_isolation ON restaurants
    USING (""TenantId"" = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation ON tables
    USING (""TenantId"" = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation ON users
    USING (""TenantId"" = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation ON customers
    USING (""TenantId"" = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation ON orders
    USING (""TenantId"" = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation ON products
    USING (""TenantId"" = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation ON payments
    USING (""TenantId"" = current_setting('app.current_tenant_id', true)::uuid);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP POLICY IF EXISTS tenant_isolation ON restaurants;
DROP POLICY IF EXISTS tenant_isolation ON tables;
DROP POLICY IF EXISTS tenant_isolation ON users;
DROP POLICY IF EXISTS tenant_isolation ON customers;
DROP POLICY IF EXISTS tenant_isolation ON orders;
DROP POLICY IF EXISTS tenant_isolation ON products;
DROP POLICY IF EXISTS tenant_isolation ON payments;

ALTER TABLE restaurants DISABLE ROW LEVEL SECURITY;
ALTER TABLE tables      DISABLE ROW LEVEL SECURITY;
ALTER TABLE users       DISABLE ROW LEVEL SECURITY;
ALTER TABLE customers   DISABLE ROW LEVEL SECURITY;
ALTER TABLE orders      DISABLE ROW LEVEL SECURITY;
ALTER TABLE products    DISABLE ROW LEVEL SECURITY;
ALTER TABLE payments    DISABLE ROW LEVEL SECURITY;
");
        }
    }
}
