using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using RushOrder.Desktop.Models;

namespace RushOrder.Desktop.Data;

public sealed class LocalDatabase : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly object _lock = new();

    public LocalDatabase()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RushOrder");
        Directory.CreateDirectory(dir);

        _conn = new SqliteConnection($"Data Source={Path.Combine(dir, "local.db")}");
        _conn.Open();
        InitSchema();
    }

    private void InitSchema()
    {
        var statements = new[]
        {
            "PRAGMA journal_mode=WAL;",
            """
            CREATE TABLE IF NOT EXISTS local_orders (
                id         TEXT PRIMARY KEY,
                status     TEXT NOT NULL,
                is_offline INTEGER NOT NULL DEFAULT 1,
                placed_at  TEXT NOT NULL,
                order_json TEXT NOT NULL,
                updated_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS local_products (
                id           TEXT PRIMARY KEY,
                is_available INTEGER NOT NULL DEFAULT 1,
                product_json TEXT NOT NULL,
                updated_at   TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS local_tables (
                id         TEXT PRIMARY KEY,
                state      TEXT NOT NULL DEFAULT 'Free',
                table_json TEXT NOT NULL,
                updated_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS pending_sync_queue (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                operation_type TEXT NOT NULL,
                endpoint       TEXT NOT NULL,
                http_method    TEXT NOT NULL DEFAULT 'POST',
                payload_json   TEXT NOT NULL,
                local_ref_id   TEXT,
                created_at     TEXT NOT NULL DEFAULT (datetime('now')),
                retry_count    INTEGER NOT NULL DEFAULT 0,
                last_error     TEXT
            );
            """,
        };

        lock (_lock)
        {
            foreach (var sql in statements)
            {
                using var cmd = _conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }
    }

    // ── Orders ────────────────────────────────────────────────────────────────

    public void UpsertOrder(OrderDto order)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO local_orders (id, status, is_offline, placed_at, order_json, updated_at)
                VALUES ($id, $status, $offline, $placed, $json, datetime('now'))
                ON CONFLICT(id) DO UPDATE SET
                    status     = excluded.status,
                    is_offline = excluded.is_offline,
                    order_json = excluded.order_json,
                    updated_at = datetime('now');
                """;
            cmd.Parameters.AddWithValue("$id",     order.Id.ToString());
            cmd.Parameters.AddWithValue("$status", order.Status.ToString());
            cmd.Parameters.AddWithValue("$offline", order.IsOffline ? 1 : 0);
            cmd.Parameters.AddWithValue("$placed", order.PlacedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$json",   JsonConvert.SerializeObject(order));
            cmd.ExecuteNonQuery();
        }
    }

    public List<OrderDto> GetOrders()
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT order_json FROM local_orders ORDER BY placed_at DESC LIMIT 200;";
            using var reader = cmd.ExecuteReader();
            var list = new List<OrderDto>();
            while (reader.Read())
            {
                var o = JsonConvert.DeserializeObject<OrderDto>(reader.GetString(0));
                if (o is not null) list.Add(o);
            }
            return list;
        }
    }

    public void UpdateOrderStatus(string orderId, string status)
    {
        lock (_lock)
        {
            using var sel = _conn.CreateCommand();
            sel.CommandText = "SELECT order_json FROM local_orders WHERE id = $id;";
            sel.Parameters.AddWithValue("$id", orderId);
            var json = sel.ExecuteScalar() as string;
            if (json is null) return;

            var order = JsonConvert.DeserializeObject<OrderDto>(json);
            if (order is null || !Enum.TryParse<OrderStatus>(status, out var newStatus)) return;
            var updated = order with { Status = newStatus };

            using var upd = _conn.CreateCommand();
            upd.CommandText = """
                UPDATE local_orders
                SET status = $status, order_json = $json, updated_at = datetime('now')
                WHERE id = $id;
                """;
            upd.Parameters.AddWithValue("$id",     orderId);
            upd.Parameters.AddWithValue("$status", status);
            upd.Parameters.AddWithValue("$json",   JsonConvert.SerializeObject(updated));
            upd.ExecuteNonQuery();
        }
    }

    // Replaces a local offline record with the server-assigned ID after sync.
    public void UpdateOrderServerId(string localId, Guid serverGuid, OrderDto serverOrder)
    {
        lock (_lock)
        {
            using var tx = _conn.BeginTransaction();

            using var ins = _conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = """
                INSERT INTO local_orders (id, status, is_offline, placed_at, order_json, updated_at)
                VALUES ($id, $status, 0, $placed, $json, datetime('now'))
                ON CONFLICT(id) DO UPDATE SET
                    status     = excluded.status,
                    is_offline = 0,
                    order_json = excluded.order_json,
                    updated_at = datetime('now');
                """;
            ins.Parameters.AddWithValue("$id",     serverGuid.ToString());
            ins.Parameters.AddWithValue("$status", serverOrder.Status.ToString());
            ins.Parameters.AddWithValue("$placed", serverOrder.PlacedAt.ToString("O"));
            ins.Parameters.AddWithValue("$json",   JsonConvert.SerializeObject(serverOrder with { IsOffline = false }));
            ins.ExecuteNonQuery();

            using var del = _conn.CreateCommand();
            del.Transaction = tx;
            del.CommandText = "DELETE FROM local_orders WHERE id = $id;";
            del.Parameters.AddWithValue("$id", localId);
            del.ExecuteNonQuery();

            tx.Commit();
        }
    }

    // ── Products snapshot ─────────────────────────────────────────────────────

    public void SnapshotProducts(IEnumerable<MenuProductDto> products)
    {
        lock (_lock)
        {
            using var tx = _conn.BeginTransaction();
            foreach (var p in products)
            {
                using var cmd = _conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO local_products (id, is_available, product_json, updated_at)
                    VALUES ($id, $avail, $json, datetime('now'))
                    ON CONFLICT(id) DO UPDATE SET
                        is_available = excluded.is_available,
                        product_json = excluded.product_json,
                        updated_at   = datetime('now');
                    """;
                cmd.Parameters.AddWithValue("$id",    p.Id.ToString());
                cmd.Parameters.AddWithValue("$avail", p.IsAvailable ? 1 : 0);
                cmd.Parameters.AddWithValue("$json",  JsonConvert.SerializeObject(p));
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    public List<MenuProductDto> GetProducts()
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT product_json FROM local_products ORDER BY rowid;";
            using var reader = cmd.ExecuteReader();
            var list = new List<MenuProductDto>();
            while (reader.Read())
            {
                var p = JsonConvert.DeserializeObject<MenuProductDto>(reader.GetString(0));
                if (p is not null) list.Add(p);
            }
            return list;
        }
    }

    // ── Sync queue ────────────────────────────────────────────────────────────

    public void EnqueueOperation(string operationType, string endpoint, string httpMethod,
        string payloadJson, string? localRefId)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO pending_sync_queue
                    (operation_type, endpoint, http_method, payload_json, local_ref_id, created_at)
                VALUES ($type, $ep, $method, $payload, $ref, datetime('now'));
                """;
            cmd.Parameters.AddWithValue("$type",    operationType);
            cmd.Parameters.AddWithValue("$ep",      endpoint);
            cmd.Parameters.AddWithValue("$method",  httpMethod);
            cmd.Parameters.AddWithValue("$payload", payloadJson);
            cmd.Parameters.AddWithValue("$ref",     (object?)localRefId ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public List<PendingSyncOperation> GetPendingOperations()
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, operation_type, endpoint, http_method, payload_json,
                       local_ref_id, created_at, retry_count, last_error
                FROM pending_sync_queue
                WHERE retry_count < 5
                ORDER BY created_at ASC;
                """;
            using var reader = cmd.ExecuteReader();
            var list = new List<PendingSyncOperation>();
            while (reader.Read())
            {
                list.Add(new PendingSyncOperation(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    DateTimeOffset.Parse(reader.GetString(6)),
                    reader.GetInt32(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8)));
            }
            return list;
        }
    }

    public void DeleteQueueItem(long id)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM pending_sync_queue WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public void IncrementRetryCount(long id, string error)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                UPDATE pending_sync_queue
                SET retry_count = retry_count + 1, last_error = $error
                WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$id",    id);
            cmd.Parameters.AddWithValue("$error", error);
            cmd.ExecuteNonQuery();
        }
    }

    public int GetPendingCount()
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pending_sync_queue WHERE retry_count < 5;";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public void Dispose() => _conn.Dispose();
}
