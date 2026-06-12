-- Row-Level Security for all tenant-scoped tables.
-- Run this AFTER EF Core migrations have created the tables.
-- The app role (rushorder_app) must be the owner or have RLS bypass disabled.

-- Enable RLS
ALTER TABLE restaurants ENABLE ROW LEVEL SECURITY;
ALTER TABLE tables ENABLE ROW LEVEL SECURITY;
ALTER TABLE users ENABLE ROW LEVEL SECURITY;
ALTER TABLE customers ENABLE ROW LEVEL SECURITY;
ALTER TABLE orders ENABLE ROW LEVEL SECURITY;
ALTER TABLE products ENABLE ROW LEVEL SECURITY;
ALTER TABLE payments ENABLE ROW LEVEL SECURITY;

-- Create per-table isolation policies
-- The app sets app.current_tenant_id via TenantDbCommandInterceptor before each command.

CREATE POLICY tenant_isolation ON restaurants
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation ON tables
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation ON users
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation ON customers
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation ON orders
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation ON products
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

CREATE POLICY tenant_isolation ON payments
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

-- Admin bypass: superuser and the migration role are exempt from RLS.
-- Application role must NOT be superuser; create it with:
--   CREATE ROLE rushorder_app LOGIN PASSWORD '...' NOSUPERUSER;
--   GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA public TO rushorder_app;
